//! The tray icon: the app lives here while a game or a model run has the
//! machine. The icon and tooltip say whether Quiet Mode is on, the menu
//! toggles it, and Quit is the one real exit (the window's close hides).

use tauri::menu::{Menu, MenuItem, PredefinedMenuItem};
use tauri::tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent};
use tauri::{AppHandle, Emitter, Manager};
use tauri_plugin_notification::NotificationExt;

use crate::engine::EngineState;

const ICON_IDLE: &[u8] = include_bytes!("../icons/tray-idle.png");
const ICON_QUIET: &[u8] = include_bytes!("../icons/tray-quiet.png");

pub struct TrayHandles {
    toggle: MenuItem<tauri::Wry>,
}

pub fn install(app: &AppHandle) -> tauri::Result<()> {
    let toggle = MenuItem::with_id(app, "toggle", "Go Quiet", true, None::<&str>)?;
    let show = MenuItem::with_id(app, "show", "Open ComputeQuiet", true, None::<&str>)?;
    let separator = PredefinedMenuItem::separator(app)?;
    let quit = MenuItem::with_id(app, "quit", "Quit", true, None::<&str>)?;
    let menu = Menu::with_items(app, &[&toggle, &show, &separator, &quit])?;

    TrayIconBuilder::with_id("main")
        .icon(tauri::image::Image::from_bytes(ICON_IDLE)?)
        .icon_as_template(false)
        .tooltip("ComputeQuiet — idle")
        .menu(&menu)
        .show_menu_on_left_click(false)
        .on_menu_event(|app, event| match event.id().as_ref() {
            "toggle" => toggle_from_tray(app.clone()),
            "show" => reveal(app),
            "quit" => {
                reveal(app);
                // The window owns the "restore before quitting?" decision.
                let _ = app.emit("confirm-quit", ());
            }
            _ => {}
        })
        .on_tray_icon_event(|tray, event| {
            if let TrayIconEvent::Click {
                button: MouseButton::Left,
                button_state: MouseButtonState::Up,
                ..
            } = event
            {
                toggle_window(tray.app_handle());
            }
        })
        .build(app)?;

    app.manage(TrayHandles { toggle });
    Ok(())
}

/// Keep the icon, tooltip and menu in step with the engine.
pub fn refresh(app: &AppHandle, state: &EngineState) {
    let (bytes, tooltip, label) = if state.quiet {
        (ICON_QUIET, "ComputeQuiet — Quiet Mode on", "Restore")
    } else {
        (ICON_IDLE, "ComputeQuiet — idle", "Go Quiet")
    };
    if let Some(tray) = app.tray_by_id("main") {
        if let Ok(image) = tauri::image::Image::from_bytes(bytes) {
            let _ = tray.set_icon(Some(image));
        }
        let _ = tray.set_tooltip(Some(tooltip));
    }
    if let Some(handles) = app.try_state::<TrayHandles>() {
        let _ = handles.toggle.set_text(label);
    }
}

fn toggle_from_tray(app: AppHandle) {
    tauri::async_runtime::spawn(async move {
        let engine = app
            .state::<std::sync::Arc<crate::engine::Engine>>()
            .inner()
            .clone();
        let quiet = !engine.is_quiet();
        let hidden = crate::commands::window_hidden(&app);
        let outcome = crate::commands::run_transition(app.clone(), engine.clone(), quiet).await;
        if hidden && engine.settings().notifications {
            let body = match (&outcome, quiet) {
                (Ok(state), true) => format!(
                    "Quiet Mode on: {} services stopped, {} processes parked.",
                    state.summary.services_stopped,
                    state.summary.processes_suspended + state.summary.processes_closed
                ),
                (Ok(state), false) if !state.quiet => "Everything is back.".to_string(),
                (Ok(_), false) => {
                    "Some changes could not be restored. Open the window.".to_string()
                }
                (Err(error), _) => error.to_string(),
            };
            let _ = app
                .notification()
                .builder()
                .title("ComputeQuiet")
                .body(body)
                .show();
        }
    });
}

/// Show the window from wherever the request came from.
pub fn reveal(app: &AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.show();
        let _ = window.unminimize();
        let _ = window.set_focus();
    }
}

fn toggle_window(app: &AppHandle) {
    let Some(window) = app.get_webview_window("main") else {
        return;
    };
    let visible = window.is_visible().unwrap_or(false);
    let focused = window.is_focused().unwrap_or(false);
    if visible && focused {
        let _ = window.hide();
    } else {
        reveal(app);
    }
}
