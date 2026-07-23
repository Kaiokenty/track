use chrono::{DateTime, Utc};
use rusqlite::{params, Connection};
use std::path::{Path, PathBuf};

use crate::models::UsagePoint;

pub struct SnapshotStore {
    conn: Connection,
}

impl SnapshotStore {
    pub fn open(path: Option<PathBuf>) -> Result<Self, String> {
        let path = path.unwrap_or_else(default_path);
        if let Some(parent) = path.parent() {
            std::fs::create_dir_all(parent).map_err(|e| e.to_string())?;
        }
        let conn = Connection::open(&path).map_err(|e| e.to_string())?;
        let store = Self { conn };
        store.ensure_schema()?;
        Ok(store)
    }

    pub fn append_meter_sample(
        &self,
        provider_id: &str,
        meter_id: &str,
        used: f64,
        limit: f64,
        at: DateTime<Utc>,
    ) -> Result<(), String> {
        self.conn
            .execute(
                "INSERT INTO meter_samples (provider_id, meter_id, used, limit_value, sampled_at)
                 VALUES (?1, ?2, ?3, ?4, ?5)",
                params![
                    provider_id,
                    meter_id,
                    used,
                    limit,
                    at.to_rfc3339()
                ],
            )
            .map_err(|e| e.to_string())?;
        Ok(())
    }

    pub fn get_series(
        &self,
        provider_id: &str,
        meter_id: &str,
        since: DateTime<Utc>,
    ) -> Result<Vec<UsagePoint>, String> {
        let mut stmt = self
            .conn
            .prepare(
                "SELECT sampled_at, used FROM meter_samples
                 WHERE provider_id = ?1 AND meter_id = ?2 AND sampled_at >= ?3
                 ORDER BY sampled_at ASC",
            )
            .map_err(|e| e.to_string())?;

        let rows = stmt
            .query_map(params![provider_id, meter_id, since.to_rfc3339()], |row| {
                let at: String = row.get(0)?;
                let used: f64 = row.get(1)?;
                Ok(UsagePoint {
                    timestamp: DateTime::parse_from_rfc3339(&at)
                        .map(|d| d.with_timezone(&Utc))
                        .unwrap_or_else(|_| Utc::now()),
                    used,
                })
            })
            .map_err(|e| e.to_string())?;

        let mut points = Vec::new();
        for r in rows {
            points.push(r.map_err(|e| e.to_string())?);
        }
        Ok(points)
    }

    fn ensure_schema(&self) -> Result<(), String> {
        self.conn
            .execute_batch(
                "CREATE TABLE IF NOT EXISTS meter_samples (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  provider_id TEXT NOT NULL,
                  meter_id TEXT NOT NULL,
                  used REAL NOT NULL,
                  limit_value REAL NOT NULL,
                  sampled_at TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_meter_samples_lookup
                  ON meter_samples (provider_id, meter_id, sampled_at);",
            )
            .map_err(|e| e.to_string())?;
        Ok(())
    }
}

pub fn default_path() -> PathBuf {
    let root = dirs::data_local_dir()
        .unwrap_or_else(|| PathBuf::from("."))
        .join("Track");
    root.join("history.db")
}

#[allow(dead_code)]
pub fn settings_path() -> PathBuf {
    let root = dirs::data_local_dir()
        .unwrap_or_else(|| PathBuf::from("."))
        .join("Track");
    root.join("settings.json")
}

#[allow(dead_code)]
pub fn ensure_parent(path: &Path) -> Result<(), String> {
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent).map_err(|e| e.to_string())?;
    }
    Ok(())
}
