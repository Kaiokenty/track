use reqwest::Client;
use serde::Deserialize;
use serde_json::json;

const CLIENT_ID: &str = "KbZUR41cY7W6zRSdpSUJ7I7mLYBKOCmB";
const BASE_URL: &str = "https://api2.cursor.sh";

pub struct CursorApiClient {
    http: Client,
    access_token: String,
    refresh_token: Option<String>,
}

impl CursorApiClient {
    pub fn new(access_token: String, refresh_token: Option<String>) -> Self {
        Self {
            http: Client::builder()
                .timeout(std::time::Duration::from_secs(30))
                .build()
                .expect("http client"),
            access_token,
            refresh_token,
        }
    }

    pub async fn get_current_period_usage(&mut self) -> Result<Option<CursorPeriodUsage>, String> {
        let body = self
            .send_dashboard("/aiserver.v1.DashboardService/GetCurrentPeriodUsage", json!({}))
            .await?;
        Ok(body.and_then(|b| serde_json::from_str(&b).ok()))
    }

    pub async fn get_plan_info(&mut self) -> Result<Option<CursorPlanInfoResponse>, String> {
        let body = self
            .send_dashboard("/aiserver.v1.DashboardService/GetPlanInfo", json!({}))
            .await?;
        Ok(body.and_then(|b| serde_json::from_str(&b).ok()))
    }

    async fn refresh_access_token(&mut self) -> Result<bool, String> {
        let Some(refresh) = self.refresh_token.clone() else {
            return Ok(false);
        };
        let resp = self
            .http
            .post(format!("{BASE_URL}/oauth/token"))
            .json(&json!({
                "grant_type": "refresh_token",
                "client_id": CLIENT_ID,
                "refresh_token": refresh
            }))
            .send()
            .await
            .map_err(|e| e.to_string())?;
        if !resp.status().is_success() {
            return Ok(false);
        }
        let payload: CursorTokenRefreshResponse = resp.json().await.map_err(|e| e.to_string())?;
        if payload.should_logout || payload.access_token.as_deref().unwrap_or("").is_empty() {
            return Ok(false);
        }
        self.access_token = payload.access_token.unwrap();
        Ok(true)
    }

    async fn send_dashboard(&mut self, path: &str, body: serde_json::Value) -> Result<Option<String>, String> {
        let once = |token: &str, http: &Client, path: &str, body: &serde_json::Value| {
            let token = token.to_string();
            let path = path.to_string();
            let body = body.clone();
            let http = http.clone();
            async move {
                http.post(format!("{BASE_URL}{path}"))
                    .header("Authorization", format!("Bearer {token}"))
                    .header("Connect-Protocol-Version", "1")
                    .json(&body)
                    .send()
                    .await
            }
        };

        let first = once(&self.access_token, &self.http, path, &body)
            .await
            .map_err(|e| e.to_string())?;
        if first.status() == reqwest::StatusCode::UNAUTHORIZED
            || first.status() == reqwest::StatusCode::FORBIDDEN
        {
            if !self.refresh_access_token().await? {
                return Ok(None);
            }
            let retry = once(&self.access_token, &self.http, path, &body)
                .await
                .map_err(|e| e.to_string())?;
            if !retry.status().is_success() {
                return Ok(None);
            }
            return Ok(Some(retry.text().await.map_err(|e| e.to_string())?));
        }
        if !first.status().is_success() {
            return Ok(None);
        }
        Ok(Some(first.text().await.map_err(|e| e.to_string())?))
    }
}

#[derive(Debug, Deserialize)]
pub struct CursorTokenRefreshResponse {
    #[serde(rename = "access_token")]
    pub access_token: Option<String>,
    #[serde(rename = "shouldLogout", default)]
    pub should_logout: bool,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CursorPeriodUsage {
    pub billing_cycle_start: Option<String>,
    pub billing_cycle_end: Option<String>,
    pub plan_usage: Option<CursorPlanUsage>,
    pub spend_limit_usage: Option<CursorSpendLimitUsage>,
    pub display_message: Option<String>,
    #[serde(default = "default_true")]
    pub enabled: bool,
}

fn default_true() -> bool {
    true
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CursorPlanUsage {
    pub total_spend: f64,
    pub included_spend: f64,
    pub bonus_spend: f64,
    pub remaining: Option<f64>,
    pub limit: f64,
    pub auto_percent_used: Option<f64>,
    pub api_percent_used: Option<f64>,
    pub total_percent_used: Option<f64>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CursorSpendLimitUsage {
    pub total_spend: Option<f64>,
    pub individual_used: Option<f64>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CursorPlanInfoResponse {
    pub plan_info: Option<CursorPlanInfo>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CursorPlanInfo {
    pub plan_name: Option<String>,
    pub included_amount_cents: Option<f64>,
}
