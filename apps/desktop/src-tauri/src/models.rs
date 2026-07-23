use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum ProviderMode {
    Subscription,
    ApiCost,
    Manual,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum MeterKind {
    Monthly,
    Weekly,
    Rolling5h,
    Custom,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum MeterUnit {
    Percent,
    UsdCents,
    Requests,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum ProviderStatus {
    Ok,
    NeedsReauth,
    Error,
    Stale,
    NotLinked,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum PaceVerdict {
    Under,
    OnTrack,
    Over,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UsagePoint {
    pub timestamp: DateTime<Utc>,
    pub used: f64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UsageMeter {
    pub id: String,
    pub label: String,
    pub kind: MeterKind,
    pub start: Option<DateTime<Utc>>,
    pub end: Option<DateTime<Utc>>,
    pub resets_at: Option<DateTime<Utc>>,
    pub used: f64,
    pub limit: f64,
    pub unit: MeterUnit,
    #[serde(default)]
    pub series: Vec<UsagePoint>,
}

impl UsageMeter {
    pub fn remaining(&self) -> f64 {
        (self.limit - self.used).max(0.0)
    }

    pub fn percent_used(&self) -> f64 {
        if self.limit <= 0.0 {
            0.0
        } else {
            ((self.used / self.limit) * 100.0).clamp(0.0, 100.0)
        }
    }

    pub fn percent_remaining(&self) -> f64 {
        100.0 - self.percent_used()
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ExtraStat {
    pub label: String,
    pub value: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ProviderSnapshot {
    pub id: String,
    pub display_name: String,
    pub plan_label: Option<String>,
    pub mode: ProviderMode,
    #[serde(default)]
    pub meters: Vec<UsageMeter>,
    #[serde(default)]
    pub extras: Vec<ExtraStat>,
    pub status: ProviderStatus,
    pub status_message: Option<String>,
    pub fetched_at: DateTime<Utc>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PaceSnapshot {
    pub meter: UsageMeter,
    pub recommended_percent: f64,
    pub actual_percent: f64,
    pub delta_percent: f64,
    pub verdict: PaceVerdict,
    pub projected_exhaustion: Option<DateTime<Utc>>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppSettings {
    pub first_run_done: bool,
    pub poll_interval_secs: u64,
    pub launch_at_startup: bool,
    #[serde(default)]
    pub show_used_not_left: bool,
    #[serde(default = "default_tray_pins")]
    pub tray_pins: Vec<String>,
    #[serde(default = "default_hotkey")]
    pub hotkey: String,
    #[serde(default)]
    pub alert_near_limit_pct: Option<f64>,
    #[serde(default = "default_alert_over_pace")]
    pub alert_over_pace: bool,
    #[serde(default)]
    pub alert_reset_soon_mins: Option<u64>,
    #[serde(default = "default_provider_order")]
    pub provider_order: Vec<String>,
    #[serde(default)]
    pub hidden_meters: Vec<String>,
    #[serde(default)]
    pub budgets_cents: HashMap<String, u64>,
}

fn default_tray_pins() -> Vec<String> {
    vec!["cursor:percent".into(), "cursor:pace".into()]
}

fn default_hotkey() -> String {
    "Alt+Shift+T".into()
}

fn default_alert_over_pace() -> bool {
    true
}

fn default_provider_order() -> Vec<String> {
    vec!["cursor".into(), "openai".into(), "anthropic".into()]
}

impl Default for AppSettings {
    fn default() -> Self {
        Self {
            first_run_done: false,
            poll_interval_secs: 300,
            launch_at_startup: false,
            show_used_not_left: false,
            tray_pins: default_tray_pins(),
            hotkey: default_hotkey(),
            alert_near_limit_pct: Some(90.0),
            alert_over_pace: true,
            alert_reset_soon_mins: Some(60),
            provider_order: default_provider_order(),
            hidden_meters: vec![],
            budgets_cents: HashMap::new(),
        }
    }
}
