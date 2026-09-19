/** Pure presentation helpers, tested without a DOM. */
import type { Summary } from "./bridge.ts";

const UNITS = ["B", "KB", "MB", "GB", "TB"];

export function formatBytes(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) return "—";
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < UNITS.length - 1) {
    value /= 1024;
    unit += 1;
  }
  const digits = unit === 0 ? 0 : value >= 100 ? 0 : 1;
  return `${value.toFixed(digits)} ${UNITS[unit]}`;
}

export function formatPercent(value: number): string {
  if (!Number.isFinite(value)) return "—";
  return `${Math.max(0, Math.min(100, Math.round(value)))}%`;
}

/** "3 s", "12 min", "1 h 5 min", "2 d 3 h". */
export function formatSince(startedAt: number, now: number): string {
  const seconds = Math.max(0, Math.floor(now - startedAt));
  if (seconds < 60) return `${seconds} s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    const rest = minutes % 60;
    return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
  }
  const days = Math.floor(hours / 24);
  const restHours = hours % 24;
  return restHours === 0 ? `${days} d` : `${days} d ${restHours} h`;
}

function plural(count: number, one: string, many: string): string {
  return `${count} ${count === 1 ? one : many}`;
}

/** Human lines for the "what Quiet Mode did" card; empty when nothing happened. */
export function summaryLines(summary: Summary): string[] {
  const lines: string[] = [];
  if (summary.services_stopped > 0)
    lines.push(
      `${plural(summary.services_stopped, "service", "services")} stopped`,
    );
  if (summary.processes_suspended > 0)
    lines.push(
      `${plural(summary.processes_suspended, "process", "processes")} suspended`,
    );
  if (summary.processes_closed > 0)
    lines.push(
      `${plural(summary.processes_closed, "process", "processes")} closed (relaunched on restore)`,
    );
  if (summary.power_changed) lines.push("Performance power plan active");
  if (summary.memory_purged) lines.push("Cached memory purged");
  return lines;
}
