import { useEffect, useMemo, useState } from "react";
import { getPace, getSettings, getSnapshots, onSnapshotsChanged, openSettingsWindow } from "../api";
import { PaceChart } from "../components/PaceChart";
import { ProviderGlyph } from "../components/ProviderGlyph";
import {
  formatRemaining,
  formatReset,
  percentUsed,
  relativeUpdated,
  verdictLabel,
  type AppSettings,
  type PaceSnapshot,
  type ProviderSnapshot,
} from "../types";

function ProviderSection({
  snap,
  showUsed,
  onFlip,
}: {
  snap: ProviderSnapshot;
  showUsed: boolean;
  onFlip: () => void;
}) {
  const [weekly, setWeekly] = useState(false);
  const [pace, setPace] = useState<PaceSnapshot | null>(null);

  const focusMeter = useMemo(() => {
    if (snap.mode === "apiCost") {
      return snap.meters.find((m) => m.id === "monthly") ?? snap.meters[0] ?? null;
    }
    const id = weekly ? "weekly" : "total";
    return snap.meters.find((m) => m.id === id) ?? snap.meters[0] ?? null;
  }, [snap, weekly]);

  useEffect(() => {
    if (!focusMeter) {
      setPace(null);
      return;
    }
    getPace(snap.id, focusMeter.id).then(setPace).catch(() => setPace(null));
  }, [snap, focusMeter]);

  if (snap.status === "notLinked") {
    return (
      <div className="card provider-card muted">
        <div className="row">
          <ProviderGlyph id={snap.id} />
          <strong>{snap.displayName}</strong>
        </div>
        <p style={{ margin: "8px 0 0", fontSize: 11 }}>
          {snap.statusMessage ?? "Not linked — connect in Settings."}
        </p>
      </div>
    );
  }

  const secondary = snap.meters.filter((m) => m.id !== focusMeter?.id);
  const spendTiles = snap.extras.filter((e) =>
    ["Today", "Last 30 days"].includes(e.label),
  );

  return (
    <div className="card provider-card">
      <div className="row">
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <ProviderGlyph id={snap.id} />
          <strong style={{ letterSpacing: "-0.02em" }}>{snap.displayName}</strong>
        </div>
        <span className="plan-badge">{snap.planLabel ?? snap.displayName}</span>
      </div>

      {snap.mode === "subscription" && snap.meters.some((m) => m.id === "weekly") && (
        <div
          className="segmented"
          role="tablist"
          aria-label="Pace window"
          data-active={weekly ? "weekly" : "monthly"}
        >
          <button
            type="button"
            role="tab"
            aria-selected={!weekly}
            className={!weekly ? "active" : ""}
            onClick={() => setWeekly(false)}
          >
            Monthly
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={weekly}
            className={weekly ? "active" : ""}
            onClick={() => setWeekly(true)}
          >
            Weekly
          </button>
        </div>
      )}

      {spendTiles.length > 0 && (
        <div className="spend-tiles">
          {spendTiles.map((t) => (
            <div className="spend-tile" key={t.label}>
              <div className="muted">{t.label}</div>
              <div className="spend-value">{t.value}</div>
            </div>
          ))}
        </div>
      )}

      {pace && (
        <div className={`verdict ${pace.verdict}`}>
          {verdictLabel(pace.verdict, pace.deltaPercent)}
        </div>
      )}

      {pace && <PaceChart pace={pace} />}

      {focusMeter && (
        <button type="button" className="remaining flip-row" onClick={onFlip} title="Click to flip used ⟷ left">
          {formatRemaining(focusMeter, showUsed)}
          {focusMeter.resetsAt && (
            <span className="muted"> · Resets in {formatReset(focusMeter.resetsAt)}</span>
          )}
        </button>
      )}

      {pace?.projectedExhaustion && pace.verdict === "over" && (
        <div className="warn">
          At current rate, limit around{" "}
          {new Date(pace.projectedExhaustion).toLocaleString(undefined, {
            weekday: "short",
            hour: "numeric",
            minute: "2-digit",
          })}
        </div>
      )}

      {secondary.map((m) => {
        const pct = percentUsed(m);
        return (
          <div className="meter" key={m.id}>
            <div className="meter-top">
              <span>{m.label}</span>
              <span className="muted">
                {pct.toFixed(0)}% · {formatReset(m.resetsAt)}
              </span>
            </div>
            <div className="bar" role="progressbar" aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100}>
              <i style={{ transform: `scaleX(${pct / 100})` }} />
            </div>
          </div>
        );
      })}

      {snap.statusMessage && snap.status !== "ok" && (
        <div className="warn">{snap.statusMessage}</div>
      )}
    </div>
  );
}

export function Flyout() {
  const [snaps, setSnaps] = useState<ProviderSnapshot[]>([]);
  const [settings, setSettings] = useState<AppSettings | null>(null);
  const [flipUsed, setFlipUsed] = useState(false);

  useEffect(() => {
    getSnapshots().then(setSnaps).catch(console.error);
    getSettings()
      .then((s) => {
        setSettings(s);
        setFlipUsed(s.showUsedNotLeft);
      })
      .catch(console.error);
    const un = onSnapshotsChanged((p) => setSnaps(p.snapshots));
    return () => {
      un.then((f) => f());
    };
  }, []);

  const fetched = snaps.map((s) => s.fetchedAt).sort();
  const updated = relativeUpdated(fetched[fetched.length - 1]);
  const linked = snaps.filter((s) => s.status !== "notLinked" || s.meters.length > 0);
  const showUsed = flipUsed || settings?.showUsedNotLeft;

  if (linked.length === 0) {
    return (
      <div className="flyout">
        <div className="flyout-header">
          <h1>Track</h1>
          <span className="muted">{updated}</span>
        </div>
        <div className="card muted">
          No usage yet. Open Cursor while signed in, then connect in Settings.
          <div style={{ marginTop: 12 }}>
            <button type="button" className="btn primary" onClick={() => openSettingsWindow()}>
              Open Settings
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flyout">
      <div className="flyout-header">
        <h1>Track</h1>
        <span className="muted">{updated}</span>
      </div>

      {snaps.map((snap) => (
        <ProviderSection
          key={snap.id}
          snap={snap}
          showUsed={!!showUsed}
          onFlip={() => setFlipUsed((v) => !v)}
        />
      ))}
    </div>
  );
}
