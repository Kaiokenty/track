import { useEffect, useState } from "react";
import {
  connectProvider,
  disconnectProvider,
  getSettings,
  getSnapshots,
  onSnapshotsChanged,
  setProviderSecret,
  updateSettings,
} from "../api";
import { ProviderGlyph } from "../components/ProviderGlyph";
import {
  TRAY_PIN_OPTIONS,
  defaultSettings,
  humanStatus,
  providerName,
  type AppSettings,
  type ProviderSnapshot,
} from "../types";

type Tab = "connections" | "budgets" | "layout" | "alerts" | "general";

const API_PROVIDERS = [
  { id: "openai", name: "OpenAI API" },
  { id: "anthropic", name: "Claude API" },
] as const;

export function Settings() {
  const [tab, setTab] = useState<Tab>("connections");
  const [snaps, setSnaps] = useState<ProviderSnapshot[]>([]);
  const [settings, setSettings] = useState<AppSettings | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [flash, setFlash] = useState<Record<string, string>>({});
  const [keyDraft, setKeyDraft] = useState<Record<string, string>>({});

  useEffect(() => {
    getSnapshots().then(setSnaps).catch(console.error);
    getSettings()
      .then(async (s) => {
        const merged = { ...defaultSettings(), ...s };
        setSettings(merged);
        if (!s.firstRunDone) {
          setSettings(await updateSettings({ ...merged, firstRunDone: true }));
        }
      })
      .catch(console.error);
    const un = onSnapshotsChanged((p) => setSnaps(p.snapshots));
    return () => {
      un.then((f) => f());
    };
  }, []);

  const cursor = snaps.find((s) => s.id === "cursor");

  async function save(next: AppSettings) {
    setSettings(await updateSettings(next));
  }

  async function onConnectCursor() {
    setBusy("cursor");
    setFlash((f) => ({ ...f, cursor: "" }));
    try {
      const snap = await connectProvider("cursor");
      setSnaps((prev) => [...prev.filter((p) => p.id !== "cursor"), snap]);
      setFlash((f) => ({
        ...f,
        cursor: snap.status === "ok" ? "Connected" : (snap.statusMessage ?? humanStatus(snap)),
      }));
    } catch (e) {
      setFlash((f) => ({ ...f, cursor: String(e) }));
    } finally {
      setBusy(null);
    }
  }

  async function onConnectApi(id: string) {
    const key = keyDraft[id]?.trim();
    if (!key) {
      setFlash((f) => ({ ...f, [id]: "Paste an Admin API key first." }));
      return;
    }
    setBusy(id);
    setFlash((f) => ({ ...f, [id]: "" }));
    try {
      await setProviderSecret(id, key);
      const snap = await connectProvider(id);
      setSnaps((prev) => [...prev.filter((p) => p.id !== id), snap]);
      setKeyDraft((d) => ({ ...d, [id]: "" }));
      setFlash((f) => ({
        ...f,
        [id]: snap.status === "ok" ? "Connected" : (snap.statusMessage ?? humanStatus(snap)),
      }));
    } catch (e) {
      setFlash((f) => ({ ...f, [id]: String(e) }));
    } finally {
      setBusy(null);
    }
  }

  async function onDisconnectApi(id: string) {
    setBusy(id);
    try {
      await disconnectProvider(id);
      setSnaps((prev) => prev.filter((p) => p.id !== id));
      setFlash((f) => ({ ...f, [id]: "Disconnected" }));
    } catch (e) {
      setFlash((f) => ({ ...f, [id]: String(e) }));
    } finally {
      setBusy(null);
    }
  }

  function toggleTrayPin(pin: string) {
    if (!settings) return;
    const has = settings.trayPins.includes(pin);
    const nextPins = has
      ? settings.trayPins.filter((p) => p !== pin)
      : [...settings.trayPins, pin].slice(0, 2);
    void save({ ...settings, trayPins: nextPins });
  }

  function toggleHiddenMeter(key: string) {
    if (!settings) return;
    const has = settings.hiddenMeters.includes(key);
    const next = has
      ? settings.hiddenMeters.filter((m) => m !== key)
      : [...settings.hiddenMeters, key];
    void save({ ...settings, hiddenMeters: next });
  }

  const nav: { id: Tab; label: string }[] = [
    { id: "connections", label: "Connections" },
    { id: "budgets", label: "Budgets" },
    { id: "layout", label: "Layout" },
    { id: "alerts", label: "Alerts" },
    { id: "general", label: "General" },
  ];

  return (
    <div className="settings">
      <aside className="sidebar">
        <div className="brand">Track</div>
        <nav>
          {nav.map((t) => (
            <button
              key={t.id}
              type="button"
              className={tab === t.id ? "active" : ""}
              onClick={() => setTab(t.id)}
            >
              {t.label}
            </button>
          ))}
        </nav>
      </aside>

      <main className="settings-main">
        {tab === "connections" && (
          <div className="stagger" key="connections">
            <div>
              <h2>Connections</h2>
              <p className="muted" style={{ marginTop: 0 }}>
                Link local Cursor session or Admin API keys. Secrets stay in Windows Credential
                Manager — never sent to React or logged.
              </p>
            </div>

            <div className="tip">
              Track lives in the system tray. Left-click for usage; right-click → Open usage.
              Overflow chevron if hidden.
            </div>

            <div className="conn">
              <ProviderGlyph id="cursor" />
              <div>
                <div className="title">Cursor</div>
                <div className="sub" title={cursor?.statusMessage ?? undefined}>
                  {cursor ? humanStatus(cursor) : "Checking…"}
                  {cursor?.statusMessage && cursor.status !== "ok"
                    ? ` — ${cursor.statusMessage}`
                    : ""}
                </div>
                {flash.cursor && (
                  <div className={cursor?.status === "ok" ? "flash-ok" : "warn"}>{flash.cursor}</div>
                )}
              </div>
              <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                <span className={`pill ${cursor?.status === "ok" ? "ok" : ""}`}>
                  {cursor?.status === "ok" ? "Connected" : "Local"}
                </span>
                <button
                  type="button"
                  className={`btn primary${busy === "cursor" ? " busy" : ""}`}
                  disabled={busy === "cursor"}
                  onClick={onConnectCursor}
                >
                  {busy === "cursor" ? "…" : cursor?.status === "ok" ? "Refresh" : "Connect"}
                </button>
              </div>
            </div>

            {API_PROVIDERS.map((p) => {
              const snap = snaps.find((s) => s.id === p.id);
              const linked = snap?.status === "ok";
              return (
                <div className="conn" key={p.id}>
                  <ProviderGlyph id={p.id} />
                  <div>
                    <div className="title">{p.name}</div>
                    <div className="sub">
                      Admin API org cost — not consumer chat quotas.
                      {snap?.statusMessage && snap.status !== "ok" ? ` ${snap.statusMessage}` : ""}
                    </div>
                    {!linked && (
                      <input
                        className="field key-field"
                        type="password"
                        placeholder="sk-… Admin key"
                        value={keyDraft[p.id] ?? ""}
                        onChange={(e) =>
                          setKeyDraft((d) => ({ ...d, [p.id]: e.target.value }))
                        }
                      />
                    )}
                    {flash[p.id] && (
                      <div className={linked ? "flash-ok" : "warn"}>{flash[p.id]}</div>
                    )}
                  </div>
                  <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                    <span className={`pill ${linked ? "ok" : ""}`}>
                      {linked ? "Connected" : "Not linked"}
                    </span>
                    {linked ? (
                      <>
                        <button
                          type="button"
                          className={`btn${busy === p.id ? " busy" : ""}`}
                          disabled={busy === p.id}
                          onClick={() => onConnectApi(p.id)}
                        >
                          Refresh
                        </button>
                        <button
                          type="button"
                          className="btn"
                          disabled={busy === p.id}
                          onClick={() => onDisconnectApi(p.id)}
                        >
                          Disconnect
                        </button>
                      </>
                    ) : (
                      <button
                        type="button"
                        className={`btn primary${busy === p.id ? " busy" : ""}`}
                        disabled={busy === p.id}
                        onClick={() => onConnectApi(p.id)}
                      >
                        Connect
                      </button>
                    )}
                  </div>
                </div>
              );
            })}

            <div className="conn conn-soon">
              <ProviderGlyph id="xai" />
              <div>
                <div className="title">Grok (xAI)</div>
                <div className="sub">Phase 3</div>
              </div>
              <button type="button" className="btn" disabled>
                Soon
              </button>
            </div>
          </div>
        )}

        {tab === "budgets" && settings && (
          <div className="stagger" key="budgets">
            <div>
              <h2>Budgets</h2>
              <p className="muted" style={{ marginTop: 0 }}>
                Optional monthly spend cap per API provider. Drives $ pace vs recommended.
              </p>
            </div>
            {API_PROVIDERS.map((p) => (
              <div className="card" key={p.id}>
                <label className="row" style={{ fontSize: 13 }}>
                  <span>{p.name} monthly cap ($)</span>
                  <input
                    className="field budget-field"
                    type="number"
                    min={0}
                    step={1}
                    placeholder="e.g. 50"
                    value={
                      settings.budgetsCents[p.id]
                        ? settings.budgetsCents[p.id] / 100
                        : ""
                    }
                    onChange={async (e) => {
                      const dollars = Number(e.target.value);
                      const next = {
                        ...settings,
                        budgetsCents: { ...settings.budgetsCents },
                      };
                      if (!e.target.value || dollars <= 0) {
                        delete next.budgetsCents[p.id];
                      } else {
                        next.budgetsCents[p.id] = Math.round(dollars * 100);
                      }
                      await save(next);
                    }}
                  />
                </label>
              </div>
            ))}
          </div>
        )}

        {tab === "layout" && settings && (
          <div className="stagger" key="layout">
            <div>
              <h2>Layout</h2>
              <p className="muted" style={{ marginTop: 0 }}>
                Tray tooltip pins (max 2) and meter visibility.
              </p>
            </div>
            <div className="section-label">Tray pins</div>
            <div className="pin-grid">
              {TRAY_PIN_OPTIONS.map((opt) => (
                <label key={opt.id} className="check-row">
                  <input
                    type="checkbox"
                    checked={settings.trayPins.includes(opt.id)}
                    onChange={() => toggleTrayPin(opt.id)}
                  />
                  {opt.label}
                </label>
              ))}
            </div>
            <div className="section-label">Provider order</div>
            {settings.providerOrder.map((id, idx) => (
              <div className="card row" key={id}>
                <span>{providerName(id)}</span>
                <div style={{ display: "flex", gap: 4 }}>
                  <button
                    type="button"
                    className="btn"
                    disabled={idx === 0}
                    onClick={() => {
                      const order = [...settings.providerOrder];
                      [order[idx - 1], order[idx]] = [order[idx], order[idx - 1]];
                      void save({ ...settings, providerOrder: order });
                    }}
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    className="btn"
                    disabled={idx === settings.providerOrder.length - 1}
                    onClick={() => {
                      const order = [...settings.providerOrder];
                      [order[idx], order[idx + 1]] = [order[idx + 1], order[idx]];
                      void save({ ...settings, providerOrder: order });
                    }}
                  >
                    ↓
                  </button>
                </div>
              </div>
            ))}
            <div className="section-label">Hidden meters</div>
            {snaps.flatMap((s) =>
              s.meters.map((m) => {
                const key = `${s.id}:${m.id}`;
                return (
                  <label key={key} className="check-row">
                    <input
                      type="checkbox"
                      checked={settings.hiddenMeters.includes(key)}
                      onChange={() => toggleHiddenMeter(key)}
                    />
                    {providerName(s.id)} — {m.label}
                  </label>
                );
              }),
            )}
          </div>
        )}

        {tab === "alerts" && settings && (
          <div className="stagger" key="alerts">
            <div>
              <h2>Alerts</h2>
              <p className="muted" style={{ marginTop: 0 }}>
                Windows toasts when near limit, over pace, or reset soon. No secrets in notifications.
              </p>
            </div>
            <div className="card">
              <label className="check-row">
                <input
                  type="checkbox"
                  checked={settings.alertOverPace}
                  onChange={(e) => void save({ ...settings, alertOverPace: e.target.checked })}
                />
                Alert when over recommended pace
              </label>
              <label className="row" style={{ fontSize: 13, marginTop: 12 }}>
                <span>Near limit (%)</span>
                <input
                  className="field"
                  type="number"
                  min={50}
                  max={100}
                  value={settings.alertNearLimitPct ?? ""}
                  onChange={(e) =>
                    void save({
                      ...settings,
                      alertNearLimitPct: e.target.value ? Number(e.target.value) : null,
                    })
                  }
                />
              </label>
              <label className="row" style={{ fontSize: 13, marginTop: 8 }}>
                <span>Reset soon (minutes)</span>
                <input
                  className="field"
                  type="number"
                  min={5}
                  value={settings.alertResetSoonMins ?? ""}
                  onChange={(e) =>
                    void save({
                      ...settings,
                      alertResetSoonMins: e.target.value ? Number(e.target.value) : null,
                    })
                  }
                />
              </label>
            </div>
          </div>
        )}

        {tab === "general" && settings && (
          <div className="stagger" key="general">
            <div>
              <h2>General</h2>
            </div>
            <div className="card">
              <label className="check-row">
                <input
                  type="checkbox"
                  checked={settings.launchAtStartup}
                  onChange={(e) =>
                    void save({ ...settings, launchAtStartup: e.target.checked })
                  }
                />
                Launch at startup
              </label>
              <label className="check-row" style={{ marginTop: 12 }}>
                <input
                  type="checkbox"
                  checked={settings.showUsedNotLeft}
                  onChange={(e) =>
                    void save({ ...settings, showUsedNotLeft: e.target.checked })
                  }
                />
                Flyout: show used % instead of left (click meter row to flip)
              </label>
              <label className="row" style={{ fontSize: 13, marginTop: 12 }}>
                <span>Poll interval (seconds)</span>
                <input
                  className="field"
                  type="number"
                  min={30}
                  value={settings.pollIntervalSecs}
                  onChange={(e) =>
                    void save({
                      ...settings,
                      pollIntervalSecs: Number(e.target.value) || 300,
                    })
                  }
                />
              </label>
              <label className="row" style={{ fontSize: 13, marginTop: 8 }}>
                <span>Global hotkey (toggle flyout)</span>
                <input
                  className="field hotkey-field"
                  type="text"
                  value={settings.hotkey}
                  onChange={(e) => void save({ ...settings, hotkey: e.target.value })}
                />
              </label>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}
