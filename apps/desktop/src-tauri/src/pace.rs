use chrono::{DateTime, Datelike, Duration, Timelike, Utc};

use crate::models::{MeterKind, PaceSnapshot, PaceVerdict, UsageMeter};

const ON_TRACK_TOLERANCE: f64 = 5.0;

pub fn calculate(meter: &UsageMeter, now: Option<DateTime<Utc>>) -> PaceSnapshot {
    let at = now.unwrap_or_else(Utc::now);
    let actual = meter.percent_used();
    let recommended = recommended_percent(meter, at);
    let delta = actual - recommended;
    let verdict = if delta > ON_TRACK_TOLERANCE {
        PaceVerdict::Over
    } else if delta < -ON_TRACK_TOLERANCE {
        PaceVerdict::Under
    } else {
        PaceVerdict::OnTrack
    };

    PaceSnapshot {
        meter: meter.clone(),
        recommended_percent: recommended,
        actual_percent: actual,
        delta_percent: delta,
        verdict,
        projected_exhaustion: project_exhaustion(meter, at),
    }
}

pub fn recommended_percent(meter: &UsageMeter, now: DateTime<Utc>) -> f64 {
    match (meter.start, meter.end) {
        (Some(start), Some(end)) if end > start => {
            let total = (end - start).num_milliseconds() as f64;
            if total <= 0.0 {
                return 0.0;
            }
            let elapsed = (now - start).num_milliseconds() as f64;
            ((elapsed / total) * 100.0).clamp(0.0, 100.0)
        }
        _ => even_split_fallback(&meter.kind, now),
    }
}

fn even_split_fallback(kind: &MeterKind, now: DateTime<Utc>) -> f64 {
    match kind {
        MeterKind::Weekly => {
            let dow = now.weekday().num_days_from_sunday() as f64;
            if dow <= 0.0 {
                100.0 / 7.0
            } else {
                (dow / 7.0) * 100.0
            }
        }
        MeterKind::Rolling5h => 50.0,
        _ => {
            let day = now.day() as f64;
            let days = days_in_month(now.year(), now.month()) as f64;
            (day / days) * 100.0
        }
    }
}

fn days_in_month(year: i32, month: u32) -> u32 {
    let (ny, nm) = if month == 12 {
        (year + 1, 1)
    } else {
        (year, month + 1)
    };
    let first_next = chrono::NaiveDate::from_ymd_opt(ny, nm, 1).unwrap();
    let first_this = chrono::NaiveDate::from_ymd_opt(year, month, 1).unwrap();
    (first_next - first_this).num_days() as u32
}

fn project_exhaustion(meter: &UsageMeter, now: DateTime<Utc>) -> Option<DateTime<Utc>> {
    let start = meter.start?;
    if meter.used <= 0.0 || meter.limit <= 0.0 {
        return None;
    }
    let elapsed = now - start;
    if elapsed.num_seconds() <= 0 {
        return None;
    }
    let rate = meter.used / elapsed.num_seconds() as f64;
    if rate <= 0.0 {
        return None;
    }
    let secs = (meter.limit - meter.used) / rate;
    if secs < 0.0 {
        return Some(now);
    }
    Some(now + Duration::seconds(secs as i64))
}

#[allow(dead_code)]
pub fn format_relative_reset(resets_at: DateTime<Utc>, now: DateTime<Utc>) -> String {
    let d = resets_at - now;
    if d.num_seconds() <= 0 {
        return "now".into();
    }
    let days = d.num_days();
    let hours = d.num_hours() % 24;
    let mins = d.num_minutes() % 60;
    if days > 0 {
        format!("{days}d {hours}h")
    } else if hours > 0 {
        format!("{hours}h {mins}m")
    } else {
        format!("{mins}m")
    }
}

#[allow(dead_code)]
pub fn _keep_timelike(now: DateTime<Utc>) -> u32 {
    now.hour()
}
