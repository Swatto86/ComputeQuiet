#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod ai;
mod engine;
mod model;
mod process;

use engine::Engine;
use model::{Preference, Settings, View};
use std::sync::{Arc, Mutex};
use tauri::{
    menu::{Menu, MenuItem, PredefinedMenuItem},
    tray::TrayIconBuilder,
    Emitter, Manager, State,
};

#[derive(Clone)]
struct Shared(Arc<Mutex<Engine>>);

/// The tray menu is often the only visible part of the app, so it names the current state
/// and the action separately. A single "Toggle" item said neither.
struct TrayMenu {
    status: MenuItem<tauri::Wry>,
    toggle: MenuItem<tauri::Wry>,
}

fn status_label(active: bool) -> &'static str {
    if active {
        "Game Mode: ON"
    } else {
        "Game Mode: OFF"
    }
}

fn toggle_label(active: bool) -> &'static str {
    if active {
        "Turn off & restore"
    } else {
        "Turn on Game Mode"
    }
}

fn show(app: &tauri::AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.show();
        let _ = window.set_focus();
    }
}

fn tray_state(app: &tauri::AppHandle, active: bool) {
    if let Some(tray) = app.tray_by_id("main") {
        let _ = tray.set_tooltip(Some(if active {
            "GameQuiet — Game Mode ON"
        } else {
            "GameQuiet — Game Mode OFF"
        }));
    }
    if let Some(menu) = app.try_state::<TrayMenu>() {
        let _ = menu.status.set_text(status_label(active));
        let _ = menu.toggle.set_text(toggle_label(active));
    }
}

async fn work(
    shared: Shared,
    app: tauri::AppHandle,
    action: impl FnOnce(&mut Engine) -> anyhow::Result<()> + Send + 'static,
) -> Result<View, String> {
    tauri::async_runtime::spawn_blocking(move || {
        let mut engine = shared
            .0
            .try_lock()
            .map_err(|_| "An operation is already running".to_string())?;
        let result = action(&mut engine);
        if let Err(ref error) = result {
            engine.status = format!("{error:#}");
        }
        let view = engine.view();
        tray_state(&app, view.state.active);
        let _ = app.emit("updated", &view);
        result.map_err(|e| format!("{e:#}"))?;
        Ok(view)
    })
    .await
    .map_err(|e| e.to_string())?
}

#[tauri::command]
fn get_state(shared: State<Shared>) -> Result<View, String> {
    Ok(shared
        .0
        .try_lock()
        .map_err(|_| "An operation is already running".to_string())?
        .view())
}
#[tauri::command]
fn ready(app: tauri::AppHandle) {
    if !std::env::args().any(|a| a == "--tray") {
        show(&app);
    }
}
#[tauri::command]
async fn scan(shared: State<'_, Shared>, app: tauri::AppHandle) -> Result<View, String> {
    work(shared.inner().clone(), app, Engine::scan).await
}
#[tauri::command]
async fn assess(shared: State<'_, Shared>, app: tauri::AppHandle) -> Result<View, String> {
    work(shared.inner().clone(), app, Engine::assess).await
}
#[tauri::command]
async fn save_settings(
    shared: State<'_, Shared>,
    app: tauri::AppHandle,
    settings: Settings,
) -> Result<View, String> {
    work(shared.inner().clone(), app, move |e| e.settings(settings)).await
}
#[tauri::command]
async fn set_preference(
    shared: State<'_, Shared>,
    app: tauri::AppHandle,
    id: String,
    preference: Preference,
) -> Result<View, String> {
    work(shared.inner().clone(), app, move |e| {
        e.preference(&id, preference)
    })
    .await
}
#[tauri::command]
async fn enable(shared: State<'_, Shared>, app: tauri::AppHandle) -> Result<View, String> {
    work(shared.inner().clone(), app, Engine::enable).await
}
#[tauri::command]
async fn restore(shared: State<'_, Shared>, app: tauri::AppHandle) -> Result<View, String> {
    work(shared.inner().clone(), app, Engine::restore).await
}
#[tauri::command]
async fn confirm_restored(
    shared: State<'_, Shared>,
    app: tauri::AppHandle,
    id: String,
) -> Result<View, String> {
    work(shared.inner().clone(), app, move |e| e.forget_restored(&id)).await
}
#[tauri::command]
async fn exit_app(shared: State<'_, Shared>, app: tauri::AppHandle) -> Result<(), String> {
    let view = work(shared.inner().clone(), app.clone(), Engine::restore).await?;
    if view.state.active {
        return Err("Restore remaining workloads before quitting, or confirm that you restored them manually.".into());
    }
    app.exit(0);
    Ok(())
}

fn main() {
    let context = tauri::generate_context!();
    #[cfg(debug_assertions)]
    let context = {
        let mut context = context;
        // Elevated WebView2 ignores environment overrides. Forward WebDriver's
        // settings through the API only in debug builds, never in installed releases.
        for window in &mut context.config_mut().app.windows {
            window.additional_browser_args =
                std::env::var("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS").ok();
            window.data_directory =
                std::env::var_os("WEBVIEW2_USER_DATA_FOLDER").map(std::path::PathBuf::from);
        }
        context
    };
    let result = tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, _, _| show(app)))
        .setup(|app| {
            let root = if let Some(root) = std::env::var_os("GAMEQUIET_DATA_DIR") {
                std::path::PathBuf::from(root)
            } else {
                std::path::PathBuf::from(
                    std::env::var_os("LOCALAPPDATA").ok_or("LOCALAPPDATA is not set")?,
                )
                .join("GameQuiet")
            };
            let engine = Engine::open(root)?;
            let active = engine.disk.active;
            app.manage(Shared(Arc::new(Mutex::new(engine))));
            let status =
                MenuItem::with_id(app, "status", status_label(active), false, None::<&str>)?;
            let open = MenuItem::with_id(app, "open", "Open GameQuiet", true, None::<&str>)?;
            let toggle =
                MenuItem::with_id(app, "toggle", toggle_label(active), true, None::<&str>)?;
            let exit = MenuItem::with_id(app, "exit", "Restore and quit", true, None::<&str>)?;
            let separator = PredefinedMenuItem::separator(app)?;
            let menu = Menu::with_items(app, &[&status, &separator, &open, &toggle, &exit])?;
            app.manage(TrayMenu {
                status: status.clone(),
                toggle: toggle.clone(),
            });
            let icon = app
                .default_window_icon()
                .ok_or("Application icon missing")?
                .clone();
            TrayIconBuilder::with_id("main")
                .icon(icon)
                .menu(&menu)
                .show_menu_on_left_click(true)
                .on_menu_event(|app, event| match event.id.as_ref() {
                    "open" => show(app),
                    "status" => {}
                    "toggle" | "exit" => {
                        let app = app.clone();
                        let quitting = event.id.as_ref() == "exit";
                        let shared = app.state::<Shared>().inner().clone();
                        tauri::async_runtime::spawn(async move {
                            let result = work(shared, app.clone(), move |engine| {
                                if quitting || engine.disk.active {
                                    engine.restore()
                                } else {
                                    engine.enable()
                                }
                            })
                            .await;
                            match result {
                                Ok(view) if quitting && !view.state.active => app.exit(0),
                                Err(_) => show(&app),
                                Ok(_) if quitting => show(&app),
                                _ => {}
                            }
                        });
                    }
                    _ => {}
                })
                .build(app)?;
            tray_state(app.handle(), active);
            Ok(())
        })
        .on_window_event(|window, event| {
            if let tauri::WindowEvent::CloseRequested { api, .. } = event {
                api.prevent_close();
                let _ = window.hide();
            }
        })
        .invoke_handler(tauri::generate_handler![
            get_state,
            ready,
            scan,
            assess,
            save_settings,
            set_preference,
            enable,
            restore,
            confirm_restored,
            exit_app
        ])
        .run(context);
    if let Err(error) = result {
        // Startup errors must be visible even when the app has no console.
        let message = serde_json::json!({"message":format!("GameQuiet could not start: {error}")})
            .to_string();
        let script="Add-Type -AssemblyName System.Windows.Forms; $v=[Console]::In.ReadToEnd()|ConvertFrom-Json; [System.Windows.Forms.MessageBox]::Show($v.message,'GameQuiet')|Out-Null";
        let mut cmd = process::command("pwsh.exe");
        cmd.args(["-NoProfile", "-NonInteractive", "-Command", script]);
        let _ = process::run(cmd, &message, std::time::Duration::from_secs(120));
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn the_tray_names_the_current_state_and_the_action_separately() {
        for active in [true, false] {
            assert_ne!(status_label(active), toggle_label(active));
        }
        assert!(status_label(true).contains("ON") && status_label(false).contains("OFF"));
        // The action item must describe what pressing it does, not the state it is in.
        assert!(toggle_label(true).contains("off"), "{}", toggle_label(true));
        assert!(
            toggle_label(false).contains("on"),
            "{}",
            toggle_label(false)
        );
    }
}
