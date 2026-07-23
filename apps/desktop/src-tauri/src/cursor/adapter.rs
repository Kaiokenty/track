use chrono::{DateTime, Datelike, Duration, TimeZone, Utc, Weekday};

use super::api::CursorApiClient;
use super::auth::{self, CursorAuthSession};
use crate::models::*;

pub async fn fetch_cursor() -> ProviderSnapshot {
    if !auth::is_cursor_installed() {
        return not_linked("Cursor not detected. Install and sign in, then reconnect.");
    }

    let session = match auth::try_read_session() {
        Ok(s) => s,
        Err(e) => return error(&format!("Could not read Cursor login state: {e}"), None),
    };

    let Some(session) = session else {
        return not_linked("Open Cursor and sign in, then refresh Track.");
    };

    let mut client = CursorApiClient::new(session.access_token.clone(), session.refresh_token.clone());

    let usage = match client.get_current_period_usage().await {
        Ok(u) => u,
        Err(e) => return error(&format!("Cursor API failed: {e}"), Some(&session)),
    };
    let plan = client.get_plan_info().await.ok().flatten();

    let Some(usage) = usage else {
        return ProviderSnapshot {
            id: "cursor".into(),
            display_name: "Cursor".into(),
            plan_label: plan
                .as_ref()
                .and_then(|p| p.plan_info.as_ref())
                .and_then(|p| p.plan_name.clone())
                .or_else(|| title_case(session.membership_type.as_deref())),
            mode: ProviderMode::Subscription,
            meters: vec![],
            extras: vec![],
            status: ProviderStatus::NeedsReauth,
            status_message: Some(
                "Signed in locally, but usage API returned no data. Try signing in again in Cursor."
                    .into(),
            ),
            fetched_at: Utc::now(),
        };
    };

    let Some(pu) = usage.plan_usage else {
        return error("Usage payload missing planUsage", Some(&session));
    };

    let start = parse_unix_ms(usage.billing_cycle_start.as_deref()).unwrap_or_else(start_of_month_utc);
    let end = parse_unix_ms(usage.billing_cycle_end.as_deref()).unwrap_or_else(|| add_months(start, 1));

    let mut limit_cents = if pu.limit > 0.0 {
        pu.limit
    } else {
        plan.as_ref()
            .and_then(|p| p.plan_info.as_ref())
            .and_then(|p| p.included_amount_cents)
            .unwrap_or(0.0)
    };
    let mut used_cents = pu.included_spend;
    if limit_cents <= 0.0 {
        if let Some(pct) = pu.total_percent_used {
            if pct > 0.0 {
                limit_cents = 100.0;
                used_cents = pct;
            }
        }
    }

    let total_percent = pu
        .total_percent_used
        .unwrap_or_else(|| if limit_cents > 0.0 { used_cents / limit_cents * 100.0 } else { 0.0 });

    let mut meters = vec![
        UsageMeter {
            id: "total".into(),
            label: "Total".into(),
            kind: MeterKind::Monthly,
            start: Some(start),
            end: Some(end),
            resets_at: Some(end),
            used: used_cents,
            limit: limit_cents.max(if used_cents > 0.0 { used_cents } else { 1.0 }),
            unit: MeterUnit::UsdCents,
            series: vec![],
        },
        build_weekly_derived(start, end, used_cents, limit_cents, total_percent),
    ];

    if let Some(auto) = pu.auto_percent_used {
        meters.push(UsageMeter {
            id: "auto".into(),
            label: "Auto".into(),
            kind: MeterKind::Monthly,
            start: Some(start),
            end: Some(end),
            resets_at: Some(end),
            used: auto,
            limit: 100.0,
            unit: MeterUnit::Percent,
            series: vec![],
        });
    }
    if let Some(api) = pu.api_percent_used {
        meters.push(UsageMeter {
            id: "api".into(),
            label: "API".into(),
            kind: MeterKind::Monthly,
            start: Some(start),
            end: Some(end),
            resets_at: Some(end),
            used: api,
            limit: 100.0,
            unit: MeterUnit::Percent,
            series: vec![],
        });
    }

    let mut extras = Vec::new();
    if pu.bonus_spend > 0.0 {
        extras.push(ExtraStat {
            label: "Bonus spend".into(),
            value: format_usd(pu.bonus_spend),
        });
    }
    if let Some(spend) = usage.spend_limit_usage {
        if let Some(on_demand) = spend.individual_used.or(spend.total_spend).filter(|v| *v > 0.0) {
            extras.push(ExtraStat {
                label: "On-demand".into(),
                value: format_usd(on_demand),
            });
        }
    }

    let plan_label = plan
        .as_ref()
        .and_then(|p| p.plan_info.as_ref())
        .and_then(|p| p.plan_name.clone())
        .or_else(|| title_case(session.membership_type.as_deref()))
        .unwrap_or_else(|| "Cursor".into());

    ProviderSnapshot {
        id: "cursor".into(),
        display_name: "Cursor".into(),
        plan_label: Some(plan_label),
        mode: ProviderMode::Subscription,
        meters,
        extras,
        status: ProviderStatus::Ok,
        status_message: usage.display_message,
        fetched_at: Utc::now(),
    }
}

fn build_weekly_derived(
    period_start: DateTime<Utc>,
    period_end: DateTime<Utc>,
    used_cents: f64,
    limit_cents: f64,
    total_percent: f64,
) -> UsageMeter {
    let now = Utc::now();
    let week_start = start_of_week(now);
    let week_end = week_start + Duration::days(7);
    let period_days = ((period_end - period_start).num_days() as f64).max(1.0);
    let weekly_limit = if limit_cents > 0.0 {
        limit_cents * (7.0 / period_days)
    } else {
        100.0 / (period_days / 7.0)
    };
    let elapsed_period_days = ((now - period_start).num_days() as f64).max(0.01);
    let days_into_week = ((now - week_start).num_days() as f64).clamp(0.01, 7.0);
    let mut approx = used_cents * (days_into_week / elapsed_period_days);
    if limit_cents <= 0.0 {
        approx = total_percent * (days_into_week / 7.0);
    }
    UsageMeter {
        id: "weekly".into(),
        label: "Weekly".into(),
        kind: MeterKind::Weekly,
        start: Some(week_start),
        end: Some(week_end),
        resets_at: Some(week_end),
        used: approx.min(if weekly_limit > 0.0 {
            weekly_limit * 3.0
        } else {
            approx
        }),
        limit: weekly_limit.max(1.0),
        unit: if limit_cents > 0.0 {
            MeterUnit::UsdCents
        } else {
            MeterUnit::Percent
        },
        series: vec![],
    }
}

fn not_linked(message: &str) -> ProviderSnapshot {
    ProviderSnapshot {
        id: "cursor".into(),
        display_name: "Cursor".into(),
        plan_label: None,
        mode: ProviderMode::Subscription,
        meters: vec![],
        extras: vec![],
        status: ProviderStatus::NotLinked,
        status_message: Some(message.into()),
        fetched_at: Utc::now(),
    }
}

fn error(message: &str, session: Option<&CursorAuthSession>) -> ProviderSnapshot {
    ProviderSnapshot {
        id: "cursor".into(),
        display_name: "Cursor".into(),
        plan_label: session.and_then(|s| title_case(s.membership_type.as_deref())),
        mode: ProviderMode::Subscription,
        meters: vec![],
        extras: vec![],
        status: ProviderStatus::Error,
        status_message: Some(message.into()),
        fetched_at: Utc::now(),
    }
}

fn parse_unix_ms(value: Option<&str>) -> Option<DateTime<Utc>> {
    let v = value?.parse::<i64>().ok()?;
    Utc.timestamp_millis_opt(v).single()
}

fn start_of_month_utc() -> DateTime<Utc> {
    let now = Utc::now();
    Utc.with_ymd_and_hms(now.year(), now.month(), 1, 0, 0, 0)
        .single()
        .unwrap()
}

fn add_months(dt: DateTime<Utc>, months: i32) -> DateTime<Utc> {
    let naive = dt.naive_utc().date();
    let mut y = naive.year();
    let mut m = naive.month() as i32 + months;
    while m > 12 {
        m -= 12;
        y += 1;
    }
    while m < 1 {
        m += 12;
        y -= 1;
    }
    Utc.with_ymd_and_hms(y, m as u32, 1, 0, 0, 0)
        .single()
        .unwrap()
}

fn start_of_week(now: DateTime<Utc>) -> DateTime<Utc> {
    let d = now.date_naive();
    let diff = (d.weekday().num_days_from_monday()) as i64;
    let day = d - Duration::days(diff);
    Utc.with_ymd_and_hms(day.year(), day.month(), day.day(), 0, 0, 0)
        .single()
        .unwrap()
}

fn format_usd(cents: f64) -> String {
    format!("${:.2}", cents / 100.0)
}

fn title_case(value: Option<&str>) -> Option<String> {
    let v = value?.trim();
    if v.is_empty() {
        return None;
    }
    let mut chars = v.chars();
    let first = chars.next()?.to_uppercase().collect::<String>();
    Some(first + &chars.as_str().to_lowercase())
}

#[allow(dead_code)]
fn _weekday_keep() -> Weekday {
    Weekday::Mon
}
