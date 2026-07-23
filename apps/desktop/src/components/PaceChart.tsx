import { useEffect, useRef } from "react";
import uPlot from "uplot";
import "uplot/dist/uPlot.min.css";
import type { PaceSnapshot, PaceVerdict } from "../types";

function strokeFor(v: PaceVerdict): string {
  if (v === "over") return "#c2410c";
  if (v === "under") return "#0f766e";
  return "#a16207";
}

export function PaceChart({ pace }: { pace: PaceSnapshot }) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!ref.current) return;

    const meter = pace.meter;
    const start = meter.start ? new Date(meter.start).getTime() : Date.now() - 86400000 * 30;
    const end = meter.end ? new Date(meter.end).getTime() : Date.now();
    const span = Math.max(1, end - start);
    const now = Date.now();
    const nowFrac = Math.min(1, Math.max(0, (now - start) / span));

    const xs: number[] = [];
    const actual: (number | null)[] = [];
    const recommended: number[] = [];

    const series = meter.series ?? [];
    if (series.length >= 2) {
      for (const p of series) {
        const t = new Date(p.timestamp).getTime();
        const x = (t - start) / span;
        const y = meter.limit > 0 ? Math.min(120, (p.used / meter.limit) * 100) : 0;
        xs.push(x);
        actual.push(y);
        recommended.push(x * 100);
      }
    } else {
      xs.push(0, nowFrac);
      actual.push(0, pace.actualPercent);
      recommended.push(0, nowFrac * 100);
    }

    // Extend recommended to full window for dashed diagonal context
    if (xs[xs.length - 1] < 1) {
      xs.push(1);
      actual.push(null);
      recommended.push(100);
    }

    const color = strokeFor(pace.verdict);
    const width = ref.current.clientWidth || 320;

    const plot = new uPlot(
      {
        width,
        height: 168,
        scales: {
          x: { time: false, min: 0, max: 1 },
          y: { min: 0, max: 100 },
        },
        axes: [
          {
            stroke: "#78716c",
            font: "10px JetBrains Mono",
            grid: { show: false },
            values: (_u, vals) => vals.map((v) => (v === 0 ? "start" : v === 1 ? "end" : "")),
          },
          {
            stroke: "#78716c",
            font: "10px JetBrains Mono",
            grid: { stroke: "#e7e5e4", width: 1 },
            values: (_u, vals) => vals.map((v) => `${v}%`),
            size: 36,
          },
        ],
        series: [
          {},
          {
            label: "actual",
            stroke: color,
            width: 2.5,
            fill: hexToRgba(color, 0.12),
            points: { show: false },
          },
          {
            label: "recommended",
            stroke: "#a8a29e",
            width: 1.5,
            dash: [4, 3],
            points: { show: false },
          },
        ],
        legend: { show: false },
        cursor: { show: false },
      },
      [xs, actual, recommended],
      ref.current,
    );

    return () => {
      plot.destroy();
    };
  }, [pace]);

  const unit = pace.meter.unit === "usdCents" ? "$" : "%";

  return (
    <div>
      <div className="legend">
        <span className="actual">Actual</span>
        <span className="recommended">Recommended</span>
        <span style={{ marginLeft: "auto" }}>Y: {unit === "$" ? "$ (as % of limit)" : "%"}</span>
      </div>
      <div ref={ref} />
    </div>
  );
}

function hexToRgba(hex: string, a: number): string {
  const h = hex.replace("#", "");
  const n = parseInt(h, 16);
  const r = (n >> 16) & 255;
  const g = (n >> 8) & 255;
  const b = n & 255;
  return `rgba(${r},${g},${b},${a})`;
}
