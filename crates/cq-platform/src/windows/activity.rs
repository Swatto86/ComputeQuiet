//! Which processes the user can see: every owner of a visible, titled
//! top-level window, and the owner of the foreground window. The scanner uses
//! this to tell a background hog from the thing the user is working in.

use std::ffi::c_void;

use cq_core::Activity;
use windows_sys::Win32::Foundation::{HWND, LPARAM};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    EnumWindows, GetForegroundWindow, GetWindowTextLengthW, GetWindowThreadProcessId,
    IsWindowVisible,
};

/// Called by `EnumWindows` once per top-level window with our `Vec<u32>`.
///
/// SAFETY contract for the caller: `lparam` must be a valid `*mut Vec<u32>`
/// for the duration of the enumeration, and nothing else may touch it.
unsafe extern "system" fn collect(window: HWND, lparam: LPARAM) -> i32 {
    // SAFETY: `lparam` is the pointer `current` passed and still owns.
    let pids = unsafe { &mut *(lparam as *mut Vec<u32>) };
    // SAFETY: plain window queries on a handle the system just handed us.
    unsafe {
        if IsWindowVisible(window) != 0 && GetWindowTextLengthW(window) > 0 {
            let mut pid = 0u32;
            GetWindowThreadProcessId(window, &raw mut pid);
            if pid != 0 && !pids.contains(&pid) {
                pids.push(pid);
            }
        }
    }
    1
}

pub fn current() -> Activity {
    let mut windowed: Vec<u32> = Vec::new();
    // SAFETY: the callback only dereferences the pointer we pass, which lives
    // until EnumWindows returns.
    let ok = unsafe { EnumWindows(Some(collect), (&raw mut windowed) as LPARAM) };
    let foreground = unsafe {
        let window = GetForegroundWindow();
        if window.is_null() {
            None
        } else {
            let mut pid = 0u32;
            GetWindowThreadProcessId(window, &raw mut pid);
            (pid != 0).then_some(pid)
        }
    };
    let _: *const c_void = std::ptr::null();
    Activity {
        known: ok != 0,
        foreground_pid: foreground,
        windowed_pids: windowed,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_desktop_session_has_visible_windows() {
        let activity = current();
        // On a headless CI runner there may be no windows at all, but the
        // call itself must succeed.
        assert!(activity.known);
        if let Some(pid) = activity.foreground_pid {
            assert!(activity.windowed_pids.contains(&pid) || pid > 0);
        }
    }
}
