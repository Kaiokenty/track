use chrono::{DateTime, Utc};
use std::collections::HashMap;
use tauri_plugin_notification::NotificationExt;

use crate::models::{AppSettings, PaceVerdict, ProviderSnapshot, UsageMeter};
use crate::pace;

const COOLDOWN_SECS: i64 = 3600;

pub struct AlertTracker {
    last_fired: HashMap<String, DateTime<Utc>>,
}

impl AlertTracker {
    pub fn new() -> Self {
        Self {
            last_fired: HashMap::new(),
        }
    }

    pub fn check_and_notify(
        &mut self,
        app: &tauri::AppHandle,
        settings: &AppSettings,
        snapshots: &[ProviderSnapshot],
    ) {
        if !settings.alert_over_pace
            && settings.alert_near_limit_pct.is_none()
            && settings.alert_reset_soon_mins.is_none()
        {
            return;
        }

        for snap in snapshots {
            if snap.status != crate::models::ProviderStatus::Ok {
                continue;
            }
            for meter in &snap.meters {
                if settings.hidden_meters.contains(&format!("{}:{}", snap.id, meter.id)) {
                    continue;
                }
                if let Some(threshold) = settings.alert_near_limit_pct {
                    let pct = meter.percent_used();
                    if pct >= threshold {
                        self.fire(
                            app,
                            &format!("near-limit:{}:{}", snap.id, meter.id),
                            "Track — Near limit",
                            &format!(
                                "{} {} is at {:.0}% (limit {:.0}%)",
                                snap.display_name,
                                meter.label,
                                pct,
                                threshold
                            ),
                        );
                    }
                }

                if settings.alert_over_pace {
                    let pace = pace::calculate(meter, None);
                    if pace.verdict == PaceVerdict::Over {
                        self.fire(
                            app,
                            &format!("over-pace:{}:{}", snap.id, meter.id),
                            "Track — Over pace",
                            &format!(
                                "{} {} is {:.0}% over recommended pace",
                                snap.display_name,
                                meter.label,
                                pace.delta_percent.abs()
                            ),
                        );
                    }
                }

                if let Some(mins) = settings.alert_reset_soon_mins {
                    if let Some(resets) = meter.resets_at {
                        let until = resets - Utc::now();
                        let until_mins = until.num_minutes();
                        if until_mins >= 0 && until_mins <= mins as i64 {
                            self.fire(
                                app,
                                &format!("reset-soon:{}:{}", snap.id, meter.id),
                                "Track — Reset soon",
                                &format!(
                                    "{} {} resets in {}m",
                                    snap.display_name,
                                    meter.label,
                                    until_mins
                                ),
                            );
                        }
                    }
                }
            }
        }
    }

    fn fire(&mut self, app: &tauri::AppHandle, key: &str, title: &str, body: &str) {
        let now = Utc::now();
        if let Some(last) = self.last_fired.get(key) {
            if (now - *last).num_seconds() < COOLDOWN_SECS {
                return;
            }
        }
        self.last_fired.insert(key.to_string(), now);
        let _ = app
            .notification()
            .builder()
            .title(title)
            .body(body)
            .show();
    }
}

#[allow(dead_code)]
fn _meter_ref(m: &UsageMeter) -> &UsageMeter {
    m
}
