mod adapters;

mod alerts;

mod commands;

mod cursor;

mod models;

mod pace;

mod store;

mod usage;



use std::sync::Arc;

use tauri::{

    menu::{Menu, MenuItem, PredefinedMenuItem},

    tray::{MouseButton, MouseButtonState, TrayIconBuilder, TrayIconEvent},

    Listener, Manager,

};

use usage::UsageService;



#[cfg_attr(mobile, tauri::mobile_entry_point)]

pub fn run() {

    let usage = UsageService::new();



    tauri::Builder::default()

        .plugin(tauri_plugin_opener::init())

        .plugin(tauri_plugin_positioner::init())

        .plugin(tauri_plugin_notification::init())

        .plugin(tauri_plugin_autostart::init(

            tauri_plugin_autostart::MacosLauncher::LaunchAgent,

            Some(vec!["--minimized"]),

        ))

        .plugin(tauri_plugin_global_shortcut::Builder::new().build())

        .manage(Arc::clone(&usage))

        .invoke_handler(tauri::generate_handler![

            commands::get_snapshots,

            commands::refresh_now,

            commands::get_pace,

            commands::connect_provider,

            commands::disconnect_provider,

            commands::get_settings,

            commands::update_settings,

            commands::open_settings_window,

            commands::hide_flyout,

            commands::show_flyout,

            commands::set_provider_secret,

            commands::remove_provider_secret,

        ])

        .setup(move |app| {

            let handle = app.handle().clone();



            if let Some(settings) = app.get_webview_window("settings") {

                let s = settings.clone();

                settings.on_window_event(move |event| {

                    if let tauri::WindowEvent::CloseRequested { api, .. } = event {

                        api.prevent_close();

                        let _ = s.hide();

                    }

                });

            }



            if let Some(flyout) = app.get_webview_window("flyout") {

                let f = flyout.clone();

                flyout.on_window_event(move |event| {

                    if let tauri::WindowEvent::CloseRequested { api, .. } = event {

                        api.prevent_close();

                        let _ = f.hide();

                    }

                    if let tauri::WindowEvent::Focused(false) = event {

                        let _ = f.hide();

                    }

                });

            }



            let open_usage = MenuItem::with_id(app, "open_usage", "Open usage", true, None::<&str>)?;

            let refresh = MenuItem::with_id(app, "refresh", "Refresh", true, None::<&str>)?;

            let open_settings =

                MenuItem::with_id(app, "settings", "Settings", true, None::<&str>)?;

            let sep = PredefinedMenuItem::separator(app)?;

            let quit = MenuItem::with_id(app, "quit", "Quit", true, None::<&str>)?;

            let menu = Menu::with_items(

                app,

                &[&open_usage, &refresh, &open_settings, &sep, &quit],

            )?;



            let tray_usage = Arc::clone(&usage);

            let tray = TrayIconBuilder::with_id("main")

                .icon(app.default_window_icon().unwrap().clone())

                .tooltip("Track — AI usage")

                .menu(&menu)

                .show_menu_on_left_click(false)

                .on_menu_event({

                    let handle = handle.clone();

                    let tray_usage = Arc::clone(&tray_usage);

                    move |app, event| match event.id().as_ref() {

                        "open_usage" => {

                            let _ = commands::show_flyout_window(app);

                        }

                        "refresh" => {

                            let u = Arc::clone(&tray_usage);

                            let app = app.clone();

                            tauri::async_runtime::spawn(async move {

                                let _ = u.refresh_now().await;

                                usage::emit_snapshots(&app, &u).await;

                            });

                        }

                        "settings" => {

                            let _ = commands::open_settings_window(handle.clone());

                        }

                        "quit" => {

                            app.exit(0);

                        }

                        _ => {}

                    }

                })

                .on_tray_icon_event(|tray, event| {

                    tauri_plugin_positioner::on_tray_event(tray.app_handle(), &event);

                    if let TrayIconEvent::Click {

                        button: MouseButton::Left,

                        button_state: MouseButtonState::Up,

                        ..

                    } = event

                    {

                        let _ = commands::show_flyout_window(tray.app_handle());

                    }

                })

                .build(app)?;

            let _ = tray;



            let usage_for_first = Arc::clone(&usage);

            let handle_first = handle.clone();

            tauri::async_runtime::spawn(async move {

                let settings = usage_for_first.get_settings().await;

                if !settings.first_run_done {

                    let _ = commands::open_settings_window(handle_first);

                }

            });



            let tray_handle = handle.clone();

            let _ = app.listen("tray-tooltip", move |event| {

                if let Ok(v) = serde_json::from_str::<serde_json::Value>(event.payload()) {

                    if let Some(text) = v.get("text").and_then(|t| t.as_str()) {

                        if let Some(tray) = tray_handle.tray_by_id("main") {

                            let _ = tray.set_tooltip(Some(text));

                        }

                    }

                }

            });



            let settings = tauri::async_runtime::block_on(usage.get_settings());
            let _ = commands::sync_autostart(&handle, settings.launch_at_startup);
            let hotkey = settings.hotkey.clone();

            let _ = commands::register_hotkey(&handle, &hotkey);



            usage.spawn_poll_loop(handle);

            Ok(())

        })

        .run(tauri::generate_context!())

        .expect("error while running Track");

}


