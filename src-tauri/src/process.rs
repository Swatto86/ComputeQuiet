use anyhow::{bail, Context, Result};
use std::{
    io::{Read, Write},
    path::Path,
    process::{Command, Stdio},
    thread,
    time::{Duration, Instant},
};

pub fn command(exe: impl AsRef<std::ffi::OsStr>) -> Command {
    let mut cmd = Command::new(exe);
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        cmd.creation_flags(0x08000000); // CREATE_NO_WINDOW, including the first console frame.
    }
    cmd
}

pub fn run(mut cmd: Command, input: &str, timeout: Duration) -> Result<String> {
    // Files avoid pipe deadlocks when a CLI delays reading stdin or a restored app
    // inherits a stream. They contain only this request/result and are removed on return.
    let mut input_file = tempfile::tempfile()?;
    input_file.write_all(input.as_bytes())?;
    use std::io::{Seek, SeekFrom};
    input_file.seek(SeekFrom::Start(0))?;
    let mut output_file = tempfile::tempfile()?;
    let error_file = tempfile::tempfile()?;
    let mut child = cmd
        .stdin(Stdio::from(input_file))
        .stdout(Stdio::from(output_file.try_clone()?))
        .stderr(Stdio::from(error_file.try_clone()?))
        .spawn()
        .context("Could not start the required executable")?;
    let started = Instant::now();
    let status = loop {
        if let Some(status) = child.try_wait()? {
            break status;
        }
        if started.elapsed() > timeout
            || output_file.metadata()?.len() > 2 * 1024 * 1024
            || error_file.metadata()?.len() > 2 * 1024 * 1024
        {
            #[cfg(windows)]
            {
                let _ = command("taskkill.exe")
                    .args(["/PID", &child.id().to_string(), "/T", "/F"])
                    .output();
            }
            let _ = child.kill();
            child.wait()?;
            bail!("Operation exceeded its time or output limit; no further actions were applied. Check the recovery list.");
        }
        thread::sleep(Duration::from_millis(50));
    };
    output_file.seek(SeekFrom::Start(0))?;
    let mut output = Vec::new();
    output_file.take(2 * 1024 * 1024).read_to_end(&mut output)?;
    if !status.success() {
        // Provider output can contain account details: never echo it into logs or the UI.
        bail!(
            "The external command failed (exit {}). Check its login or availability. {}",
            status.code().unwrap_or(-1),
            if error_file.metadata()?.len() == 0 {
                "No error details were returned."
            } else {
                "Diagnostic output was withheld for privacy."
            }
        );
    }
    String::from_utf8(output).context("Command returned invalid UTF-8")
}

pub fn powershell(script: &str, operation: &str, input: &str, root: &Path) -> Result<String> {
    let temp = tempfile::Builder::new()
        .prefix("operation-")
        .tempdir_in(root)?;
    let file = temp.path().join("operation.ps1");
    std::fs::write(&file, script)?;
    let mut cmd = command("pwsh.exe");
    cmd.current_dir(temp.path())
        .args(["-NoProfile", "-NonInteractive", "-File"])
        .arg(file)
        .arg(operation);
    let output = run(cmd, input, Duration::from_secs(180))?;
    let envelope: serde_json::Value =
        serde_json::from_str(output.trim_start_matches('\u{feff}').trim())
            .context("Windows helper returned invalid data")?;
    if let Some(error) = envelope.get("error").and_then(|v| v.as_str()) {
        bail!("{error}");
    }
    Ok(serde_json::to_string(&envelope)?)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn a_child_that_never_reads_input_still_times_out() {
        let mut cmd = command("pwsh.exe");
        cmd.args([
            "-NoProfile",
            "-NonInteractive",
            "-Command",
            "Start-Sleep -Seconds 30",
        ]);
        let start = Instant::now();
        let result = run(cmd, &"x".repeat(200_000), Duration::from_millis(500));
        assert!(result.is_err());
        assert!(start.elapsed() < Duration::from_secs(8));
    }
}
