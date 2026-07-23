pub fn set_secret(provider_id: &str, secret: &str) -> Result<(), String> {
    let entry = keyring::Entry::new("track-ai-usage", provider_id).map_err(|e| e.to_string())?;
    entry.set_password(secret).map_err(|e| e.to_string())
}

pub fn get_secret(provider_id: &str) -> Option<String> {
    let entry = keyring::Entry::new("track-ai-usage", provider_id).ok()?;
    entry.get_password().ok()
}

pub fn has_secret(provider_id: &str) -> bool {
    get_secret(provider_id).is_some()
}

pub fn remove_secret(provider_id: &str) -> Result<(), String> {
    let entry = keyring::Entry::new("track-ai-usage", provider_id).map_err(|e| e.to_string())?;
    match entry.delete_credential() {
        Ok(()) => Ok(()),
        Err(keyring::Error::NoEntry) => Ok(()),
        Err(e) => Err(e.to_string()),
    }
}
