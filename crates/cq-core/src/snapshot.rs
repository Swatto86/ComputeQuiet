//! What the platform reports about the machine: the process table, the
//! services a profile cares about, the active power plan, and headline
//! resource figures for the dashboard.

use std::path::PathBuf;

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize)]
pub struct ProcessInfo {
    pub pid: u32,
    pub name: String,
    pub exe: Option<PathBuf>,
    /// Full argument vector including the program itself.
    pub args: Vec<String>,
    pub cwd: Option<PathBuf>,
    pub memory_bytes: u64,
    pub cpu_percent: f32,
    /// Seconds since the epoch. Identifies a PID across reuse.
    pub start_time: u64,
}

impl ProcessInfo {
    pub fn exe_stem(&self) -> Option<String> {
        self.exe
            .as_ref()
            .and_then(|exe| exe.file_stem())
            .map(|stem| stem.to_string_lossy().into_owned())
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ServiceState {
    Running,
    Stopped,
    /// Starting, stopping or paused: left alone until it settles.
    Transitioning,
    NotInstalled,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct ServiceInfo {
    pub name: String,
    pub display_name: String,
    pub state: ServiceState,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
pub struct PowerPlan {
    pub id: String,
    pub name: String,
}

#[derive(Debug, Clone, PartialEq, Serialize, Deserialize, Default)]
pub struct Snapshot {
    pub processes: Vec<ProcessInfo>,
    pub services: Vec<ServiceInfo>,
    pub power_plan: Option<PowerPlan>,
}

#[derive(Debug, Clone, Copy, PartialEq, Serialize, Deserialize, Default)]
pub struct SystemStats {
    pub cpu_percent: f32,
    pub memory_total: u64,
    pub memory_used: u64,
    pub memory_available: u64,
    pub process_count: usize,
}
