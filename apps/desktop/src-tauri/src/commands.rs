use std::sync::Arc;

use tauri::{AppHandle, Manager, State, WebviewWindow};

use tauri_plugin_autostart::ManagerExt;



use crate::adapters;

use crate::models::{AppSettings, PaceSnapshot, ProviderSnapshot};

use crate::usage::{emit_snapshots, UsageService};



#[tauri::command]

pub async fn get_snapshots(state: State<'_, Arc<UsageService>>) -> Result<Vec<ProviderSnapshot>, String> {

    Ok(state.get_cached_snapshots().await)

}



#[tauri::command]

pub async fn refresh_now(

    app: AppHandle,

    state: State<'_, Arc<UsageService>>,

) -> Result<Vec<ProviderSnapshot>, String> {

    let snaps = state.refresh_now().await;

    emit_snapshots(&app, &state).await;

    Ok(snaps)

}



#[tauri::command]

pub async fn get_pace(

    state: State<'_, Arc<UsageService>>,

    provider_id: String,

    meter_id: String,

) -> Result<Option<PaceSnapshot>, String> {

    Ok(state.get_pace(&provider_id, &meter_id).await)

}



#[tauri::command]

pub async fn connect_provider(

    app: AppHandle,

    state: State<'_, Arc<UsageService>>,

    id: String,

) -> Result<ProviderSnapshot, String> {

    if !matches!(id.as_str(), "cursor" | "openai" | "anthropic") {

        return Err("Unknown provider".into());

    }

    if matches!(id.as_str(), "openai" | "anthropic") && !adapters::has_secret(&id) {

        return Err("Add an Admin API key first.".into());

    }

    let snaps = state.refresh_now().await;

    emit_snapshots(&app, &state).await;

    snaps

        .into_iter()

        .find(|s| s.id == id)

        .ok_or_else(|| format!("{id} snapshot missing"))

}



#[tauri::command]

pub async fn disconnect_provider(

    app: AppHandle,

    state: State<'_, Arc<UsageService>>,

    id: String,

) -> Result<(), String> {

    if matches!(id.as_str(), "openai" | "anthropic") {

        adapters::remove_secret(&id)?;

        state.remove_provider(&id).await;

        emit_snapshots(&app, &state).await;

    }

    Ok(())

}



#[tauri::command]

pub async fn get_settings(state: State<'_, Arc<UsageService>>) -> Result<AppSettings, String> {

    Ok(state.get_settings().await)

}



#[tauri::command]

pub async fn update_settings(

    app: AppHandle,

    state: State<'_, Arc<UsageService>>,

    settings: AppSettings,

) -> Result<AppSettings, String> {

    let prev = state.get_settings().await;

    let next = state.update_settings(settings).await;



    if prev.launch_at_startup != next.launch_at_startup {

        sync_autostart(&app, next.launch_at_startup)?;

    }



    if prev.hotkey != next.hotkey {

        register_hotkey(&app, &next.hotkey)?;

    }



    Ok(next)

}



#[tauri::command]

pub fn open_settings_window(app: AppHandle) -> Result<(), String> {

    if let Some(w) = app.get_webview_window("settings") {

        let _ = w.show();

        let _ = w.set_focus();

    }

    Ok(())

}



#[tauri::command]

pub fn hide_flyout(app: AppHandle) -> Result<(), String> {

    if let Some(w) = app.get_webview_window("flyout") {

        let _ = w.hide();

    }

    Ok(())

}



#[tauri::command]

pub fn show_flyout(app: AppHandle) -> Result<(), String> {

    toggle_flyout_window(&app)

}



pub fn toggle_flyout_window(app: &AppHandle) -> Result<(), String> {

    let Some(w) = app.get_webview_window("flyout") else {

        return Err("flyout window missing".into());

    };

    if w.is_visible().unwrap_or(false) {

        let _ = w.hide();

    } else {

        position_near_tray(app, &w);

        let _ = w.show();

        let _ = w.set_focus();

    }

    Ok(())

}



pub fn show_flyout_window(app: &AppHandle) -> Result<(), String> {

    let Some(w) = app.get_webview_window("flyout") else {

        return Err("flyout window missing".into());

    };

    position_near_tray(app, &w);

    let _ = w.show();

    let _ = w.set_focus();

    Ok(())

}



fn position_near_tray(app: &AppHandle, window: &WebviewWindow) {

    if let Ok(Some(m)) = app.primary_monitor() {

        let size = m.size();

        let scale = m.scale_factor();

        let win = window.outer_size().unwrap_or(tauri::PhysicalSize::new(380, 520));

        let x = (size.width as f64 / scale) - (win.width as f64 / scale) - 12.0;

        let y = (size.height as f64 / scale) - (win.height as f64 / scale) - 48.0;

        let _ = window.set_position(tauri::Position::Logical(tauri::LogicalPosition { x, y }));

    }

}



#[tauri::command]

pub fn set_provider_secret(provider_id: String, secret: String) -> Result<(), String> {

    adapters::set_secret(&provider_id, &secret)

}



#[tauri::command]

pub fn remove_provider_secret(provider_id: String) -> Result<(), String> {

    adapters::remove_secret(&provider_id)

}



pub fn sync_autostart(app: &AppHandle, enabled: bool) -> Result<(), String> {

    let autostart = app.autolaunch();

    if enabled {

        autostart.enable().map_err(|e| e.to_string())?;

    } else {

        autostart.disable().map_err(|e| e.to_string())?;

    }

    Ok(())

}



pub fn register_hotkey(app: &AppHandle, hotkey: &str) -> Result<(), String> {

    use tauri_plugin_global_shortcut::{GlobalShortcutExt, Shortcut};

    let gs = app.global_shortcut();

    let _ = gs.unregister_all();

    if hotkey.trim().is_empty() {

        return Ok(());

    }

    let shortcut = hotkey.parse::<Shortcut>().map_err(|e| e.to_string())?;

    let app_handle = app.clone();

    gs.on_shortcut(shortcut, move |_app, _shortcut, _event| {

        let _ = toggle_flyout_window(&app_handle);

    })

    .map_err(|e| e.to_string())?;

    Ok(())

}


