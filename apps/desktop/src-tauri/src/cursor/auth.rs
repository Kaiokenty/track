use rusqlite::{params, Connection};
use std::fs;
use std::path::{Path, PathBuf};

#[derive(Debug, Clone)]
pub struct CursorAuthSession {
    pub access_token: String,
    pub refresh_token: Option<String>,
    pub email: Option<String>,
    pub membership_type: Option<String>,
    pub subscription_status: Option<String>,
}

pub fn find_state_database_path() -> Option<PathBuf> {
    let primary = dirs::config_dir()?.join("Cursor").join("User").join("globalStorage").join("state.vscdb");
    if primary.exists() {
        return Some(primary);
    }
    let alt = dirs::data_local_dir()?.join("Cursor").join("User").join("globalStorage").join("state.vscdb");
    if alt.exists() {
        Some(alt)
    } else {
        None
    }
}

pub fn is_cursor_installed() -> bool {
    if find_state_database_path().is_some() {
        return true;
    }
    dirs::config_dir()
        .map(|p| p.join("Cursor").exists())
        .unwrap_or(false)
}

pub fn try_read_session() -> Result<Option<CursorAuthSession>, String> {
    let Some(db_path) = find_state_database_path() else {
        return Ok(None);
    };

    match read_from_database(&db_path) {
        Ok(s) => Ok(s),
        Err(_) => {
            let temp = std::env::temp_dir().join("TrackCursorAuth");
            fs::create_dir_all(&temp).map_err(|e| e.to_string())?;
            for suffix in ["", "-wal", "-shm"] {
                let src = PathBuf::from(format!("{}{}", db_path.display(), suffix));
                if src.exists() {
                    let dest = temp.join(format!("state.vscdb{suffix}"));
                    let _ = fs::copy(&src, &dest);
                }
            }
            read_from_database(&temp.join("state.vscdb"))
        }
    }
}

fn read_from_database(db_path: &Path) -> Result<Option<CursorAuthSession>, String> {
    let conn = Connection::open_with_flags(
        db_path,
        rusqlite::OpenFlags::SQLITE_OPEN_READ_ONLY | rusqlite::OpenFlags::SQLITE_OPEN_NO_MUTEX,
    )
    .map_err(|e| e.to_string())?;

    let get = |key: &str| -> Option<String> {
        conn.query_row(
            "SELECT value FROM ItemTable WHERE key = ?1 LIMIT 1",
            params![key],
            |row| {
                let value: Result<String, _> = row.get(0);
                match value {
                    Ok(s) => Ok(Some(s)),
                    Err(_) => {
                        let bytes: Vec<u8> = row.get(0)?;
                        Ok(Some(String::from_utf8_lossy(&bytes).into_owned()))
                    }
                }
            },
        )
        .ok()
        .flatten()
    };

    let access = get("cursorAuth/accessToken");
    let Some(access) = access.filter(|s| !s.trim().is_empty()) else {
        return Ok(None);
    };

    Ok(Some(CursorAuthSession {
        access_token: access,
        refresh_token: get("cursorAuth/refreshToken"),
        email: get("cursorAuth/cachedEmail"),
        membership_type: get("cursorAuth/stripeMembershipType"),
        subscription_status: get("cursorAuth/stripeSubscriptionStatus"),
    }))
}
