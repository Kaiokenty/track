export type ProviderMode = "subscription" | "apiCost" | "manual";
export type MeterKind = "monthly" | "weekly" | "rolling5h" | "custom";
export type MeterUnit = "percent" | "usdCents" | "requests";
export type ProviderStatus = "ok" | "needsReauth" | "error" | "stale" | "notLinked";
export type PaceVerdict = "under" | "onTrack" | "over";

export interface UsagePoint {
  timestamp: string;
  used: number;
}

export interface UsageMeter {
  id: string;
  label: string;
  kind: MeterKind;
  start?: string | null;
  end?: string | null;
  resetsAt?: string | null;
  used: number;
  limit: number;
  unit: MeterUnit;
  series?: UsagePoint[];
}

export interface ExtraStat {
  label: string;
  value: string;
}

export interface ProviderSnapshot {
  id: string;
  displayName: string;
  planLabel?: string | null;
  mode: ProviderMode;
  meters: UsageMeter[];
  extras: ExtraStat[];
  status: ProviderStatus;
  statusMessage?: string | null;
  fetchedAt: string;
}

export interface PaceSnapshot {
  meter: UsageMeter;
  recommendedPercent: number;
  actualPercent: number;
  deltaPercent: number;
  verdict: PaceVerdict;
  projectedExhaustion?: string | null;
}

export interface AppSettings {
  firstRunDone: boolean;
  pollIntervalSecs: number;
  launchAtStartup: boolean;
  showUsedNotLeft: boolean;
  trayPins: string[];
  hotkey: string;
  alertNearLimitPct?: number | null;
  alertOverPace: boolean;
  alertResetSoonMins?: number | null;
  providerOrder: string[];
  hiddenMeters: string[];
  budgetsCents: Record<string, number>;
}

export const TRAY_PIN_OPTIONS = [
  { id: "cursor:percent", label: "Cursor %" },
  { id: "cursor:pace", label: "Cursor pace" },
  { id: "openai:spend", label: "OpenAI $" },
  { id: "anthropic:spend", label: "Claude $" },
] as const;

export const PROVIDER_IDS = ["cursor", "openai", "anthropic"] as const;

export function percentUsed(m: UsageMeter): number {
  if (m.limit <= 0) return 0;
  return Math.min(100, Math.max(0, (m.used / m.limit) * 100));
}

export function formatRemaining(m: UsageMeter, showUsed = false): string {
  if (m.unit === "usdCents") {
    const used = m.used / 100;
    const limit = m.limit / 100;
    if (showUsed) {
      return `$${used.toFixed(2)} used of $${limit.toFixed(0)}`;
    }
    if (m.used >= m.limit) return `Limit reached · $${used.toFixed(0)} of $${limit.toFixed(0)}`;
    return `$${((m.limit - m.used) / 100).toFixed(2)} left`;
  }
  const used = percentUsed(m);
  const left = 100 - used;
  if (showUsed) return `${used.toFixed(0)}% used`;
  return `${left.toFixed(0)}% left`;
}

export function formatReset(resetsAt?: string | null): string {
  if (!resetsAt) return "";
  const end = new Date(resetsAt).getTime();
  const ms = end - Date.now();
  if (ms <= 0) return "now";
  const days = Math.floor(ms / 86400000);
  const hours = Math.floor((ms % 86400000) / 3600000);
  const mins = Math.floor((ms % 3600000) / 60000);
  if (days > 0) return `${days}d ${hours}h`;
  if (hours > 0) return `${hours}h ${mins}m`;
  return `${mins}m`;
}

export function relativeUpdated(iso?: string | null): string {
  if (!iso) return "Not refreshed";
  const mins = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000));
  if (mins < 1) return "Updated just now";
  if (mins === 1) return "Updated 1m ago";
  if (mins < 60) return `Updated ${mins}m ago`;
  const hours = Math.round(mins / 60);
  return `Updated ${hours}h ago`;
}

export function humanStatus(s: ProviderSnapshot): string {
  if (s.status === "ok") return `${s.planLabel ?? "Connected"} · Connected`;
  if (s.status === "notLinked") return "Not linked";
  if (s.status === "needsReauth") return "Needs reauth";
  if (s.status === "stale") return "Stale";
  return "Error";
}

export function verdictLabel(v: PaceVerdict, delta: number): string {
  const sign = delta >= 0 ? "+" : "";
  if (v === "over") return `${sign}${delta.toFixed(0)}% over pace`;
  if (v === "under") return `${sign}${delta.toFixed(0)}% under pace`;
  return "On track";
}

export function providerName(id: string): string {
  if (id === "cursor") return "Cursor";
  if (id === "openai") return "OpenAI API";
  if (id === "anthropic") return "Claude API";
  return id;
}

export function defaultSettings(): AppSettings {
  return {
    firstRunDone: false,
    pollIntervalSecs: 300,
    launchAtStartup: false,
    showUsedNotLeft: false,
    trayPins: ["cursor:percent", "cursor:pace"],
    hotkey: "Alt+Shift+T",
    alertNearLimitPct: 90,
    alertOverPace: true,
    alertResetSoonMins: 60,
    providerOrder: ["cursor", "openai", "anthropic"],
    hiddenMeters: [],
    budgetsCents: {},
  };
}
