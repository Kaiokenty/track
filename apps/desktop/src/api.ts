import { invoke } from "@tauri-apps/api/core";
import { listen, type UnlistenFn } from "@tauri-apps/api/event";
import type { AppSettings, PaceSnapshot, ProviderSnapshot } from "./types";

export async function getSnapshots(): Promise<ProviderSnapshot[]> {
  return invoke("get_snapshots");
}

export async function refreshNow(): Promise<ProviderSnapshot[]> {
  return invoke("refresh_now");
}

export async function getPace(providerId: string, meterId: string): Promise<PaceSnapshot | null> {
  return invoke("get_pace", { providerId, meterId });
}

export async function connectProvider(id: string): Promise<ProviderSnapshot> {
  return invoke("connect_provider", { id });
}

export async function disconnectProvider(id: string): Promise<void> {
  return invoke("disconnect_provider", { id });
}

export async function setProviderSecret(providerId: string, secret: string): Promise<void> {
  return invoke("set_provider_secret", { providerId, secret });
}

export async function removeProviderSecret(providerId: string): Promise<void> {
  return invoke("remove_provider_secret", { providerId });
}

export async function getSettings(): Promise<AppSettings> {
  return invoke("get_settings");
}

export async function updateSettings(settings: AppSettings): Promise<AppSettings> {
  return invoke("update_settings", { settings });
}

export async function openSettingsWindow(): Promise<void> {
  return invoke("open_settings_window");
}

export async function hideFlyout(): Promise<void> {
  return invoke("hide_flyout");
}

export async function onSnapshotsChanged(
  cb: (payload: { snapshots: ProviderSnapshot[]; fetchedAt: string }) => void,
): Promise<UnlistenFn> {
  return listen("snapshots-changed", (e) => {
    cb(e.payload as { snapshots: ProviderSnapshot[]; fetchedAt: string });
  });
}
