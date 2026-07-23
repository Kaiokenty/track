use chrono::{Datelike, TimeZone, Utc};
use serde::Deserialize;

use crate::models::*;

const COST_URL: &str = "https://api.anthropic.com/v1/organizations/cost_report";

pub async fn fetch_anthropic(budget_cents: Option<u64>) -> ProviderSnapshot {
    let Some(key) = super::secret::get_secret("anthropic") else {
        return not_linked("Paste an Anthropic Admin API key in Settings to track org API spend.");
    };

    let now = Utc::now();
    let month_start = Utc
        .with_ymd_and_hms(now.year(), now.month(), 1, 0, 0, 0)
        .single()
        .unwrap();
    let day_start = Utc
        .with_ymd_and_hms(now.year(), now.month(), now.day(), 0, 0, 0)
        .single()
        .unwrap();
    let thirty_days_ago = now - chrono::Duration::days(30);

    let client = reqwest::Client::new();
    let month_cost = match fetch_cost_total(&client, &key, month_start, now).await {
        Ok(v) => v,
        Err(e) => return error(&format!("Anthropic Admin API failed: {e}")),
    };
    let today_cost = fetch_cost_total(&client, &key, day_start, now)
        .await
        .unwrap_or(0.0);
    let thirty_day_cost = fetch_cost_total(&client, &key, thirty_days_ago, now)
        .await
        .unwrap_or(month_cost);

    let used_cents = month_cost;
    let limit_cents = budget_cents
        .map(|b| b as f64)
        .unwrap_or_else(|| (used_cents * 1.2).max(10_000.0));

    let month_end = add_months(month_start, 1);

    let mut extras = vec![
        ExtraStat {
            label: "Today".into(),
            value: format_usd_cents(today_cost),
        },
        ExtraStat {
            label: "Last 30 days".into(),
            value: format_usd_cents(thirty_day_cost),
        },
    ];
    if budget_cents.is_none() {
        extras.push(ExtraStat {
            label: "Budget".into(),
            value: "Not set — using soft cap".into(),
        });
    }

    ProviderSnapshot {
        id: "anthropic".into(),
        display_name: "Claude API".into(),
        plan_label: Some("Admin API".into()),
        mode: ProviderMode::ApiCost,
        meters: vec![UsageMeter {
            id: "monthly".into(),
            label: "Monthly spend".into(),
            kind: MeterKind::Monthly,
            start: Some(month_start),
            end: Some(month_end),
            resets_at: Some(month_end),
            used: used_cents,
            limit: limit_cents.max(used_cents.max(1.0)),
            unit: MeterUnit::UsdCents,
            series: vec![],
        }],
        extras,
        status: ProviderStatus::Ok,
        status_message: None,
        fetched_at: now,
    }
}

async fn fetch_cost_total(
    client: &reqwest::Client,
    key: &str,
    start: chrono::DateTime<Utc>,
    end: chrono::DateTime<Utc>,
) -> Result<f64, String> {
    let mut total_cents = 0.0;
    let mut page: Option<String> = None;
    loop {
        let mut req = client
            .get(COST_URL)
            .header("x-api-key", key)
            .header("anthropic-version", "2023-06-01")
            .query(&[
                ("starting_at", start.to_rfc3339()),
                ("ending_at", end.to_rfc3339()),
            ]);
        if let Some(ref p) = page {
            req = req.query(&[("page", p.clone())]);
        }
        let resp = req.send().await.map_err(|e| e.to_string())?;
        if !resp.status().is_success() {
            let status = resp.status();
            let body = resp.text().await.unwrap_or_default();
            let msg = if body.len() > 200 {
                format!("HTTP {status}")
            } else {
                format!("HTTP {status}: {body}")
            };
            return Err(msg);
        }
        let parsed: AnthropicCostPage = resp.json().await.map_err(|e| e.to_string())?;
        for bucket in parsed.data {
            for result in bucket.results {
                if let Ok(cents) = result.amount.parse::<f64>() {
                    total_cents += cents;
                }
            }
        }
        if parsed.has_more {
            page = parsed.next_page;
        } else {
            break;
        }
    }
    Ok(total_cents)
}

fn not_linked(message: &str) -> ProviderSnapshot {
    ProviderSnapshot {
        id: "anthropic".into(),
        display_name: "Claude API".into(),
        plan_label: None,
        mode: ProviderMode::ApiCost,
        meters: vec![],
        extras: vec![],
        status: ProviderStatus::NotLinked,
        status_message: Some(message.into()),
        fetched_at: Utc::now(),
    }
}

fn error(message: &str) -> ProviderSnapshot {
    ProviderSnapshot {
        id: "anthropic".into(),
        display_name: "Claude API".into(),
        plan_label: None,
        mode: ProviderMode::ApiCost,
        meters: vec![],
        extras: vec![],
        status: ProviderStatus::Error,
        status_message: Some(message.into()),
        fetched_at: Utc::now(),
    }
}

fn format_usd_cents(cents: f64) -> String {
    format!("${:.2}", cents / 100.0)
}

fn add_months(dt: chrono::DateTime<Utc>, months: i32) -> chrono::DateTime<Utc> {
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

#[derive(Debug, Deserialize)]
struct AnthropicCostPage {
    data: Vec<AnthropicCostBucket>,
    has_more: bool,
    next_page: Option<String>,
}

#[derive(Debug, Deserialize)]
struct AnthropicCostBucket {
    results: Vec<AnthropicCostResult>,
}

#[derive(Debug, Deserialize)]
struct AnthropicCostResult {
    amount: String,
}
