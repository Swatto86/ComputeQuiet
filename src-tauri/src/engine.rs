use crate::{ai, model::*, process};
use anyhow::{bail, Context, Result};
use std::{
    io::Write,
    path::PathBuf,
    time::{SystemTime, UNIX_EPOCH},
};

pub struct Engine {
    pub root: PathBuf,
    pub disk: DiskState,
    saved: DiskState,
    pub snapshot: Snapshot,
    pub sampled_at: u64,
    pub advice: Vec<Advice>,
    pub summary: String,
    pub status: String,
}

pub fn now() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_or(0, |d| d.as_secs())
}

impl Engine {
    pub fn open(root: PathBuf) -> Result<Self> {
        std::fs::create_dir_all(&root)?;
        let file = root.join("state.json");
        let mut disk: DiskState = if file.exists() {
            serde_json::from_slice(&std::fs::read(file)?).context("Saved state is unreadable. It has been preserved; restore from your backup before continuing.")?
        } else {
            DiskState::default()
        };
        if disk.version != 1 {
            bail!("Unsupported state version. Update GameQuiet; existing state was preserved.");
        }
        disk.active |= !disk.recovery.is_empty();
        let status = if disk.active {
            active_status(&disk.recovery)
        } else {
            "Ready. Scan to see what is using resources.".into()
        };
        Ok(Self {
            root,
            saved: disk.clone(),
            disk,
            snapshot: Snapshot::default(),
            sampled_at: 0,
            advice: vec![],
            summary: String::new(),
            status,
        })
    }
    pub fn view(&self) -> View {
        View {
            process_id: std::process::id(),
            state: self.disk.clone(),
            snapshot: self.snapshot.clone(),
            sampled_at: self.sampled_at,
            session_label: session_label(&self.disk).into(),
            advice: self.advice.clone(),
            summary: self.summary.clone(),
            busy: false,
            status: self.status.clone(),
        }
    }
    pub fn save(&mut self) -> Result<()> {
        let result = (|| -> Result<()> {
            let mut file = tempfile::NamedTempFile::new_in(&self.root)?;
            file.write_all(&serde_json::to_vec_pretty(&self.disk)?)?;
            file.as_file().sync_all()?;
            let mut retries = 0;
            loop {
                match file.persist(self.root.join("state.json")) {
                    Ok(_) => break,
                    Err(error)
                        if cfg!(windows)
                            && matches!(error.error.raw_os_error(), Some(5 | 32 | 33))
                            && retries < 5 =>
                    {
                        // Antivirus/indexers can briefly hold the destination without delete sharing.
                        file = error.file;
                        std::thread::sleep(std::time::Duration::from_millis(25 << retries));
                        retries += 1;
                    }
                    Err(error) => return Err(error.error.into()),
                }
            }
            Ok(())
        })();
        match result {
            Ok(()) => {
                self.saved = self.disk.clone();
                Ok(())
            }
            Err(error) => {
                self.disk = self.saved.clone();
                Err(error).context("Could not save recovery; the last saved state was retained and no further actions were applied")
            }
        }
    }
    fn log(&mut self, message: String) {
        self.disk
            .history
            .insert(0, format!("{} | {}", now(), message));
        self.disk.history.truncate(100);
    }
    pub fn scan(&mut self) -> Result<()> {
        let raw = process::powershell(include_str!("../windows.ps1"), "scan", "", &self.root)?;
        let snapshot: Snapshot = serde_json::from_str(&raw)?;
        if snapshot.workloads.len() > 100 {
            bail!("Unexpectedly large process snapshot");
        }
        self.advice = snapshot
            .workloads
            .iter()
            .filter_map(|w| {
                self.disk
                    .cache
                    .iter()
                    .find(|c| c.key == w.key() && now().saturating_sub(c.at) < 86400)
                    .map(|c| {
                        let mut advice = c.advice.clone();
                        advice.id = w.id.clone();
                        advice
                    })
            })
            .collect();
        self.snapshot = snapshot;
        self.sampled_at = now();
        self.summary.clear();
        self.status = if self.disk.active {
            format!("Scan complete. {}", active_status(&self.disk.recovery))
        } else {
            "Scan complete. Preferences are tied to the exact executable; updates need a new review.".into()
        };
        Ok(())
    }
    pub fn assess(&mut self) -> Result<()> {
        if self.disk.active {
            bail!("End the quiet session before running a cloud assessment");
        }
        self.scan()?;
        let result = ai::assess(&self.disk.settings, &self.snapshot, &self.root)?;
        self.disk.cache = result
            .workloads
            .iter()
            .filter_map(|advice| {
                self.snapshot
                    .workloads
                    .iter()
                    .find(|w| w.id == advice.id && !w.hash.is_empty())
                    .map(|w| SavedAdvice {
                        key: w.key(),
                        at: now(),
                        advice: advice.clone(),
                    })
            })
            .collect();
        self.summary = result.summary;
        self.advice = result.workloads;
        self.status = "Cloud assessment complete. Review suggested actions below.".into();
        self.log("Cloud assessment completed; no workloads changed.".into());
        self.save()
    }
    pub fn settings(&mut self, settings: Settings) -> Result<()> {
        if self.disk.active {
            bail!("Restore the current session before changing settings");
        }
        if settings.model.len() > 128
            || !settings
                .model
                .chars()
                .all(|c| c.is_ascii_alphanumeric() || "._:/-".contains(c))
        {
            bail!("Invalid model name");
        }
        let old = self.disk.settings.clone();
        self.disk.settings = settings;
        if let Err(e) = self.save() {
            self.disk.settings = old;
            return Err(e);
        }
        self.status = "Settings saved.".into();
        Ok(())
    }
    pub fn preference(&mut self, id: &str, preference: Preference) -> Result<()> {
        if self.disk.active {
            bail!("Restore the current session before changing preferences");
        }
        let w = self
            .snapshot
            .workloads
            .iter()
            .find(|w| w.id == id)
            .context("Workload is no longer in the snapshot")?;
        if !w.actionable() {
            bail!("This workload is protected or cannot be restored safely");
        }
        let key = w.key();
        let old = self.disk.rules.clone();
        self.disk.rules.retain(|r| r.key != key);
        self.disk.rules.push(Rule { key, preference });
        if let Err(e) = self.save() {
            self.disk.rules = old;
            return Err(e);
        }
        self.status = "Preference saved.".into();
        Ok(())
    }
    pub fn enable(&mut self) -> Result<()> {
        if self.disk.active || !self.disk.recovery.is_empty() {
            bail!("Restore the previous session first");
        }
        self.scan()?;
        let chosen: Vec<_> = self
            .snapshot
            .workloads
            .iter()
            .filter(|w| eligible(w, &self.disk))
            .cloned()
            .collect();
        if chosen.is_empty() {
            bail!("No approved workloads are running. Choose \"Close for session\" for at least one workload; for Ollama also enable interruption permission in Settings.");
        }
        self.disk.active = true;
        self.save()?;
        for workload in chosen {
            self.disk.recovery.push(Recovery {
                workload: workload.clone(),
                status: "pending".into(),
                error: String::new(),
            });
            self.save()?; // Write-ahead: even a crash in the stop operation leaves a restore target.
            let result = process::powershell(
                include_str!("../windows.ps1"),
                "stop",
                &serde_json::to_string(&workload)?,
                &self.root,
            )
            .and_then(|raw| {
                let value: serde_json::Value = serde_json::from_str(&raw)?;
                value["changed"]
                    .as_bool()
                    .context("Windows did not confirm whether the workload was stopped")
            });
            if let Some(entry) = self.disk.recovery.last_mut() {
                match &result {
                    Ok(true) => entry.status = "stopped".into(),
                    Ok(false) => {}
                    Err(e) => {
                        entry.status = "needs_attention".into();
                        entry.error = e.to_string();
                    }
                }
            }
            if matches!(result, Ok(false)) {
                self.disk.recovery.pop();
            }
            if result.is_ok() {
                self.snapshot.workloads.retain(|w| w.id != workload.id);
            }
            self.log(format!(
                "{}: {}",
                workload.name,
                match result {
                    Ok(true) => "stopped",
                    Ok(false) => "already exited; no restart scheduled",
                    Err(_) => "needs attention; recovery retained",
                }
            ));
            self.save()?;
        }
        self.disk.active = !self.disk.recovery.is_empty();
        self.status = if self.disk.active {
            active_status(&self.disk.recovery)
        } else {
            "All selected workloads had already exited. No restoration is needed.".into()
        };
        self.save()
    }
    pub fn restore(&mut self) -> Result<()> {
        self.clear_snapshot();
        // Reverse application order, retaining every failed entry for retry and crash recovery.
        for index in (0..self.disk.recovery.len()).rev() {
            let workload = self.disk.recovery[index].workload.clone();
            match process::powershell(
                include_str!("../windows.ps1"),
                "restore",
                &serde_json::to_string(&workload)?,
                &self.root,
            ) {
                Ok(_) => {
                    self.disk.recovery.remove(index);
                    self.log(format!("{}: restored", workload.name));
                }
                Err(e) => {
                    self.disk.recovery[index].error = e.to_string();
                    self.disk.recovery[index].status = "restore_failed".into();
                }
            }
            self.save()?;
        }
        self.disk.active = !self.disk.recovery.is_empty();
        self.status = self.recovery_status();
        self.save()
    }
    pub fn forget_restored(&mut self, id: &str) -> Result<()> {
        if !self.disk.recovery.iter().any(|r| r.workload.id == id) {
            bail!("Recovery entry is no longer present");
        }
        self.disk.recovery.retain(|r| r.workload.id != id);
        self.disk.active = !self.disk.recovery.is_empty();
        self.log("User confirmed a workload was restored manually.".into());
        self.clear_snapshot();
        self.status = self.recovery_status();
        self.save()
    }
    fn clear_snapshot(&mut self) {
        self.snapshot = Snapshot::default();
        self.sampled_at = 0;
        self.advice.clear();
        self.summary.clear();
    }
    fn recovery_status(&self) -> String {
        if self.disk.active {
            active_status(&self.disk.recovery)
        } else {
            "Session ended. No recovery entries remain. Scan for a fresh snapshot.".into()
        }
    }
}

pub fn session_label(disk: &DiskState) -> &'static str {
    if !disk.active && disk.recovery.is_empty() {
        return "READY";
    }
    let stopped = disk
        .recovery
        .iter()
        .filter(|r| r.status == "stopped")
        .count();
    if stopped == 0 {
        "NEEDS ATTENTION"
    } else if stopped < disk.recovery.len() {
        "PARTIALLY QUIET"
    } else {
        "QUIET SESSION"
    }
}

// Journal outcomes are historical. Errors (including a timeout or partial stop) do not
// establish whether a process is currently running; only a fresh scan can show that.
fn active_status(recovery: &[Recovery]) -> String {
    let stopped = recovery.iter().filter(|r| r.status == "stopped").count();
    let attention = recovery.len() - stopped;
    format!(
        "Saved session: {stopped} confirmed stops, {attention} unconfirmed or failed actions. Review Activity & recovery; restore to finish. No continuous monitoring runs."
    )
}

pub fn eligible(w: &Workload, disk: &DiskState) -> bool {
    if !w.actionable() || (w.kind == "ollama" && !disk.settings.interrupt_ollama) {
        return false;
    }
    // Cloud advice informs the user's review; it never vetoes an explicit, hash-bound Allow.
    disk.rules
        .iter()
        .any(|r| r.key == w.key() && r.preference == Preference::Allow)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn session_labels_describe_results_not_just_the_active_flag() {
        for (statuses, expected) in [
            (vec![], "NEEDS ATTENTION"),
            (vec!["pending"], "NEEDS ATTENTION"),
            (vec!["needs_attention"], "NEEDS ATTENTION"),
            (vec!["restore_failed"], "NEEDS ATTENTION"),
            (vec!["stopped"], "QUIET SESSION"),
            (vec!["stopped", "needs_attention"], "PARTIALLY QUIET"),
        ] {
            let mut disk = DiskState {
                active: true,
                ..Default::default()
            };
            disk.recovery = statuses
                .into_iter()
                .map(|status| Recovery {
                    workload: Workload::default(),
                    status: status.into(),
                    error: String::new(),
                })
                .collect();
            assert_eq!(session_label(&disk), expected);
        }
        assert_eq!(session_label(&DiskState::default()), "READY");
    }
    #[test]
    fn approval_and_ollama_permission_decide_eligibility_not_cloud_advice() {
        // Regression: a hash-bound Allow used to be vetoed when cached advice said "ask".
        // Eligibility no longer consults advice at all; only the user's choices count.
        let w = Workload {
            id: "ollama".into(),
            exe: "ollama.exe".into(),
            hash: "abc".into(),
            kind: "ollama".into(),
            ..Default::default()
        };
        let mut disk = DiskState::default();
        assert!(!eligible(&w, &disk));
        disk.rules.push(Rule {
            key: w.key(),
            preference: Preference::Allow,
        });
        assert!(!eligible(&w, &disk));
        disk.settings.interrupt_ollama = true;
        assert!(eligible(&w, &disk));
        let mut updated = w.clone();
        updated.hash = "changed".into();
        assert!(!eligible(&updated, &disk));
        updated = w;
        updated.blocked = "security".into();
        assert!(!eligible(&updated, &disk));
    }
    #[test]
    fn a_workload_that_would_not_close_is_reported_not_hidden() {
        let entry = |status: &str| Recovery {
            workload: Workload::default(),
            status: status.into(),
            error: String::new(),
        };
        assert!(active_status(&[entry("stopped")]).contains("1 confirmed stop"));
        let mixed = active_status(&[entry("stopped"), entry("needs_attention")]);
        assert!(mixed.contains("1 unconfirmed"), "{mixed}");
        let both = active_status(&[entry("needs_attention"), entry("needs_attention")]);
        assert!(both.contains("0 confirmed stops"), "{both}");
        assert!(
            !both.contains("still running"),
            "A failed command cannot establish current process state"
        );
        assert!(active_status(&[entry("restore_failed")]).contains("1 unconfirmed"));
    }
    #[test]
    fn clearing_last_recovery_updates_status_and_invalidates_old_measurements() {
        let dir = tempfile::tempdir().unwrap();
        let mut engine = Engine::open(dir.path().into()).unwrap();
        engine.disk.active = true;
        engine.disk.recovery.push(Recovery {
            workload: Workload {
                id: "test".into(),
                ..Default::default()
            },
            status: "needs_attention".into(),
            error: String::new(),
        });
        engine.snapshot.workloads.push(Workload::default());
        engine.status = "Game Mode is on".into();
        engine.save().unwrap();
        engine.forget_restored("test").unwrap();
        assert!(!engine.disk.active);
        assert!(!engine.status.contains("is on"));
        assert!(engine.snapshot.workloads.is_empty());
    }
    #[test]
    fn successful_edits_replace_previous_errors() {
        let dir = tempfile::tempdir().unwrap();
        let mut engine = Engine::open(dir.path().into()).unwrap();
        engine.status = "Previous operation failed".into();
        engine.settings(Settings::default()).unwrap();
        assert_eq!(engine.status, "Settings saved.");
        engine.snapshot.workloads.push(Workload {
            id: "test".into(),
            kind: "close".into(),
            hash: "abc".into(),
            ..Default::default()
        });
        engine.preference("test", Preference::Keep).unwrap();
        assert_eq!(engine.status, "Preference saved.");
    }
    #[test]
    fn journals_from_the_advice_filter_era_still_load() {
        let dir = tempfile::tempdir().unwrap();
        std::fs::write(
            dir.path().join("state.json"),
            br#"{"version":1,"settings":{"provider":"codex","model":"","cli_path":"","automatic":true,"interrupt_ollama":true},"rules":[],"cache":[],"active":false,"recovery":[],"history":[]}"#,
        )
        .unwrap();
        let engine = Engine::open(dir.path().into()).unwrap();
        assert!(engine.disk.settings.interrupt_ollama);
    }
    #[test]
    fn journal_survives_restart_and_corrupt_state_is_not_overwritten() {
        let dir = tempfile::tempdir().unwrap();
        let mut engine = Engine::open(dir.path().into()).unwrap();
        engine.disk.active = true;
        engine.disk.recovery.push(Recovery {
            workload: Workload::default(),
            status: "pending".into(),
            error: String::new(),
        });
        engine.save().unwrap();
        assert_eq!(
            Engine::open(dir.path().into()).unwrap().disk.recovery.len(),
            1
        );
        std::fs::write(dir.path().join("state.json"), b"broken").unwrap();
        assert!(Engine::open(dir.path().into()).is_err());
        assert_eq!(
            std::fs::read(dir.path().join("state.json")).unwrap(),
            b"broken"
        );
    }

    #[test]
    fn failed_journal_write_rolls_memory_back_and_retains_the_last_file() {
        let dir = tempfile::tempdir().unwrap();
        let mut engine = Engine::open(dir.path().into()).unwrap();
        engine.save().unwrap();
        let state = dir.path().join("state.json");
        let backup = dir.path().join("saved.json");
        let previous = std::fs::read(&state).unwrap();
        std::fs::rename(&state, &backup).unwrap();
        std::fs::create_dir(&state).unwrap(); // An unreplaceable destination simulates a filesystem failure.
        engine.disk.active = true;
        assert!(engine.save().is_err());
        assert!(!engine.disk.active);
        assert_eq!(std::fs::read(&backup).unwrap(), previous);
        std::fs::remove_dir(&state).unwrap();
        std::fs::rename(backup, state).unwrap();
        assert!(!Engine::open(dir.path().into()).unwrap().disk.active);
    }

    #[test]
    fn successive_atomic_journal_updates_remain_readable() {
        let dir = tempfile::tempdir().unwrap();
        let mut engine = Engine::open(dir.path().into()).unwrap();
        for number in 0..25 {
            engine.disk.history.push(number.to_string());
            engine.save().unwrap();
            assert_eq!(
                Engine::open(dir.path().into()).unwrap().disk.history.len(),
                number + 1
            );
        }
    }

    #[cfg(windows)]
    #[test]
    fn a_short_scanner_lock_does_not_abort_a_journal_update() {
        use std::os::windows::fs::OpenOptionsExt;
        let dir = tempfile::tempdir().unwrap();
        let mut engine = Engine::open(dir.path().into()).unwrap();
        engine.save().unwrap();
        let held = std::fs::OpenOptions::new()
            .read(true)
            .share_mode(1)
            .open(dir.path().join("state.json"))
            .unwrap();
        let release = std::thread::spawn(move || {
            std::thread::sleep(std::time::Duration::from_millis(100));
            drop(held);
        });
        engine.disk.active = true;
        let result = engine.save();
        release.join().unwrap();
        assert!(result.is_ok(), "{result:?}");
        assert!(Engine::open(dir.path().into()).unwrap().disk.active);
    }
}
