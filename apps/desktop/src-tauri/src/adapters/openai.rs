use chrono::{Datelike, TimeZone, Utc};
use serde::Deserialize;

use crate::models::*;

const COSTS_URL: &str = "https://api.openai.com/v1/organization/costs";

pub async fn fetch_openai(budget_cents: Option<u64>) -> ProviderSnapshot {
    let Some(key) = super::secret::get_secret("openai") else {
        return not_linked("Paste an OpenAI Admin API key in Settings to track org API spend.");
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
        Err(e) => return error(&format!("OpenAI Admin API failed: {e}")),
    };
    let today_cost = fetch_cost_total(&client, &key, day_start, now)
        .await
        .unwrap_or(0.0);
    let thirty_day_cost = fetch_cost_total(&client, &key, thirty_days_ago, now)
        .await
        .unwrap_or(month_cost);

    let used_cents = (month_cost * 100.0).round();
    let limit_cents = budget_cents
        .map(|b| b as f64)
        .unwrap_or_else(|| (used_cents * 1.2).max(10_000.0));

    let month_end = add_months(month_start, 1);

    let mut extras = vec![
        ExtraStat {
            label: "Today".into(),
            value: format_usd(today_cost),
        },
        ExtraStat {
            label: "Last 30 days".into(),
            value: format_usd(thirty_day_cost),
        },
    ];
    if budget_cents.is_none() {
        extras.push(ExtraStat {
            label: "Budget".into(),
            value: "Not set — using soft cap".into(),
        });
    }

    ProviderSnapshot {
        id: "openai".into(),
        display_name: "OpenAI API".into(),
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
    let mut total = 0.0;
    let mut page: Option<String> = None;
    loop {
        let mut req = client
            .get(COSTS_URL)
            .header("Authorization", format!("Bearer {key}"))
            .header("Content-Type", "application/json")
            .query(&[
                ("start_time", start.timestamp().to_string()),
                ("end_time", end.timestamp().to_string()),
                ("bucket_width", "1d".into()),
                ("limit", "180".into()),
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
        let parsed: CostsPage = resp.json().await.map_err(|e| e.to_string())?;
        for bucket in parsed.data {
            for result in bucket.results {
                total += result.amount.value;
            }
        }
        if parsed.has_more {
            page = parsed.next_page;
        } else {
            break;
        }
    }
    Ok(total)
}

fn not_linked(message: &str) -> ProviderSnapshot {
    ProviderSnapshot {
        id: "openai".into(),
        display_name: "OpenAI API".into(),
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
        id: "openai".into(),
        display_name: "OpenAI API".into(),
        plan_label: None,
        mode: ProviderMode::ApiCost,
        meters: vec![],
        extras: vec![],
        status: ProviderStatus::Error,
        status_message: Some(message.into()),
        fetched_at: Utc::now(),
    }
}

fn format_usd(dollars: f64) -> String {
    format!("${:.2}", dollars)
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
struct CostsPage {
    data: Vec<CostBucket>,
    has_more: bool,
    next_page: Option<String>,
}

#[derive(Debug, Deserialize)]
struct CostBucket {
    results: Vec<CostResult>,
}

#[derive(Debug, Deserialize)]
struct CostResult {
    amount: CostAmount,
}

#[derive(Debug, Deserialize)]
struct CostAmount {
    value: f64,
}
