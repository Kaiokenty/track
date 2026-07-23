use chrono::Utc;
use std::collections::HashMap;
use std::sync::Arc;
use tokio::sync::{Mutex, Notify};

use crate::adapters::{fetch_anthropic, fetch_openai};
use crate::alerts::AlertTracker;
use crate::cursor::adapter::fetch_cursor;
use crate::models::*;
use crate::store::{self, SnapshotStore};

pub struct UsageService {
    cache: Mutex<HashMap<String, ProviderSnapshot>>,
    store: Mutex<Option<SnapshotStore>>,
    settings: Mutex<AppSettings>,
    alerts: Mutex<AlertTracker>,
    notify: Notify,
}

impl UsageService {
    pub fn new() -> Arc<Self> {
        let settings = load_settings();
        let store = SnapshotStore::open(None).ok();
        Arc::new(Self {
            cache: Mutex::new(HashMap::new()),
            store: Mutex::new(store),
            settings: Mutex::new(settings),
            alerts: Mutex::new(AlertTracker::new()),
            notify: Notify::new(),
        })
    }

    pub async fn get_cached_snapshots(&self) -> Vec<ProviderSnapshot> {
        let cache = self.cache.lock().await;
        let settings = self.settings.lock().await;
        order_snapshots(&cache, &settings)
    }

    pub async fn refresh_now(&self) -> Vec<ProviderSnapshot> {
        let settings = self.settings.lock().await.clone();
        let cursor = fetch_cursor().await;
        let openai_budget = settings.budgets_cents.get("openai").copied();
        let anthropic_budget = settings.budgets_cents.get("anthropic").copied();

        let openai = if crate::adapters::has_secret("openai") {
            fetch_openai(openai_budget).await
        } else {
            placeholder_api("openai", "OpenAI API")
        };
        let anthropic = if crate::adapters::has_secret("anthropic") {
            fetch_anthropic(anthropic_budget).await
        } else {
            placeholder_api("anthropic", "Claude API")
        };

        {
            let mut cache = self.cache.lock().await;
            cache.insert("cursor".into(), cursor.clone());
            cache.insert("openai".into(), openai.clone());
            cache.insert("anthropic".into(), anthropic.clone());
        }

        for snap in [&cursor, &openai, &anthropic] {
            self.record_history(snap).await;
        }

        self.get_cached_snapshots().await
    }

    pub async fn get_settings(&self) -> AppSettings {
        self.settings.lock().await.clone()
    }

    pub async fn update_settings(&self, next: AppSettings) -> AppSettings {
        {
            let mut s = self.settings.lock().await;
            *s = next.clone();
        }
        save_settings(&next);
        next
    }

    pub async fn get_pace(&self, provider_id: &str, meter_id: &str) -> Option<PaceSnapshot> {
        let snaps = self.get_cached_snapshots().await;
        let snap = snaps.into_iter().find(|s| s.id == provider_id)?;
        let meter = snap.meters.into_iter().find(|m| m.id == meter_id)?;
        let mut meter = meter;
        if let Some(start) = meter.start {
            if let Some(store) = self.store.lock().await.as_ref() {
                if let Ok(series) = store.get_series(provider_id, meter_id, start) {
                    if series.len() >= 2 {
                        meter.series = series;
                    }
                }
            }
        }
        Some(crate::pace::calculate(&meter, None))
    }

    pub async fn remove_provider(&self, id: &str) {
        let mut cache = self.cache.lock().await;
        cache.remove(id);
    }

    pub fn spawn_poll_loop(self: &Arc<Self>, app: tauri::AppHandle) {
        let svc = Arc::clone(self);
        tauri::async_runtime::spawn(async move {
            let _ = svc.refresh_now().await;
            emit_snapshots(&app, &svc).await;
            loop {
                let secs = svc.settings.lock().await.poll_interval_secs.max(30);
                tokio::select! {
                    _ = tokio::time::sleep(std::time::Duration::from_secs(secs)) => {}
                    _ = svc.notify.notified() => {}
                }
                let _ = svc.refresh_now().await;
                emit_snapshots(&app, &svc).await;
            }
        });
    }

    pub fn kick_refresh(&self) {
        self.notify.notify_one();
    }

    pub async fn check_alerts(&self, app: &tauri::AppHandle) {
        let settings = self.settings.lock().await.clone();
        let snapshots = self.get_cached_snapshots().await;
        let mut alerts = self.alerts.lock().await;
        alerts.check_and_notify(app, &settings, &snapshots);
    }

    async fn record_history(&self, snap: &ProviderSnapshot) {
        if !matches!(
            snap.status,
            ProviderStatus::Ok | ProviderStatus::Stale
        ) {
            return;
        }
        let guard = self.store.lock().await;
        let Some(store) = guard.as_ref() else {
            return;
        };
        for meter in &snap.meters {
            let _ = store.append_meter_sample(
                &snap.id,
                &meter.id,
                meter.used,
                meter.limit,
                snap.fetched_at,
            );
        }
    }
}

fn order_snapshots(
    cache: &HashMap<String, ProviderSnapshot>,
    settings: &AppSettings,
) -> Vec<ProviderSnapshot> {
    let mut out = Vec::new();
    for id in &settings.provider_order {
        if let Some(s) = cache.get(id) {
            out.push(filter_meters(s.clone(), settings));
        }
    }
    for (id, snap) in cache {
        if !settings.provider_order.contains(id) {
            out.push(filter_meters(snap.clone(), settings));
        }
    }
    if out.is_empty() {
        out.push(placeholder_cursor());
    }
    out
}

fn filter_meters(mut snap: ProviderSnapshot, settings: &AppSettings) -> ProviderSnapshot {
    snap.meters.retain(|m| {
        !settings
            .hidden_meters
            .contains(&format!("{}:{}", snap.id, m.id))
    });
    snap
}

pub async fn emit_snapshots(app: &tauri::AppHandle, svc: &UsageService) {
    use tauri::Emitter;
    let snapshots = svc.get_cached_snapshots().await;
    let fetched_at = snapshots
        .iter()
        .map(|s| s.fetched_at)
        .max()
        .unwrap_or_else(Utc::now);
    let _ = app.emit(
        "snapshots-changed",
        serde_json::json!({
            "snapshots": snapshots,
            "fetchedAt": fetched_at,
        }),
    );

    let settings = svc.get_settings().await;
    let tip = build_tray_tooltip(&snapshots, &settings);
    let _ = app.emit("tray-tooltip", serde_json::json!({ "text": tip }));

    svc.check_alerts(app).await;
}

fn build_tray_tooltip(snapshots: &[ProviderSnapshot], settings: &AppSettings) -> String {
    let pins: Vec<String> = settings
        .tray_pins
        .iter()
        .take(2)
        .filter_map(|pin| format_pin(pin, snapshots))
        .collect();
    if pins.is_empty() {
        "Track — AI usage".into()
    } else {
        format!("Track — {}", pins.join(" · "))
    }
}

fn format_pin(pin: &str, snapshots: &[ProviderSnapshot]) -> Option<String> {
    let parts: Vec<&str> = pin.split(':').collect();
    if parts.len() != 2 {
        return None;
    }
    let provider_id = parts[0];
    let field = parts[1];
    let snap = snapshots.iter().find(|s| s.id == provider_id)?;

    match field {
        "percent" => {
            let m = snap.meters.first()?;
            let label = match provider_id {
                "cursor" => "C",
                "openai" => "O",
                "anthropic" => "A",
                _ => "?",
            };
            Some(format!("{label} {:.0}%", m.percent_used()))
        }
        "pace" => {
            let m = snap.meters.first()?;
            let pace = crate::pace::calculate(m, None);
            let v = match pace.verdict {
                PaceVerdict::Over => "over",
                PaceVerdict::Under => "under",
                PaceVerdict::OnTrack => "ok",
            };
            Some(v.into())
        }
        "spend" => {
            let m = snap.meters.first()?;
            if m.unit == MeterUnit::UsdCents {
                Some(format!("${:.0}", m.used / 100.0))
            } else {
                None
            }
        }
        _ => None,
    }
}

fn placeholder_cursor() -> ProviderSnapshot {
    ProviderSnapshot {
        id: "cursor".into(),
        display_name: "Cursor".into(),
        plan_label: None,
        mode: ProviderMode::Subscription,
        meters: vec![],
        extras: vec![],
        status: ProviderStatus::NotLinked,
        status_message: Some("Waiting for first refresh…".into()),
        fetched_at: Utc::now(),
    }
}

fn placeholder_api(id: &str, name: &str) -> ProviderSnapshot {
    ProviderSnapshot {
        id: id.into(),
        display_name: name.into(),
        plan_label: None,
        mode: ProviderMode::ApiCost,
        meters: vec![],
        extras: vec![],
        status: ProviderStatus::NotLinked,
        status_message: Some("Add an Admin API key in Settings.".into()),
        fetched_at: Utc::now(),
    }
}

fn load_settings() -> AppSettings {
    let path = store::settings_path();
    if let Ok(raw) = std::fs::read_to_string(&path) {
        if let Ok(s) = serde_json::from_str(&raw) {
            return s;
        }
    }
    AppSettings::default()
}

fn save_settings(settings: &AppSettings) {
    let path = store::settings_path();
    let _ = store::ensure_parent(&path);
    if let Ok(raw) = serde_json::to_string_pretty(settings) {
        let _ = std::fs::write(path, raw);
    }
}
