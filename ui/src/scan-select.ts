/** Pure helpers for the Scan view: what is ticked by default and what a row says. */
import type { Recommendation, RecommendationKind } from "./bridge.ts";

/** Low-risk finds that are not already targets start ticked; the rest do not. */
export function defaultSelection(items: Recommendation[]): Set<number> {
  const selected = new Set<number>();
  items.forEach((item, index) => {
    if (item.risk === "low" && !item.already_targeted) selected.add(index);
  });
  return selected;
}

export function selectedItems(
  items: Recommendation[],
  selected: Set<number>,
): Recommendation[] {
  return items.filter(
    (item, index) => selected.has(index) && !item.already_targeted,
  );
}

export function kindLabel(kind: RecommendationKind): string {
  switch (kind.kind) {
    case "process":
      return kind.action === "close" ? "Close & relaunch" : "Suspend";
    case "service":
      return "Stop service";
    case "power_plan":
      return "Power plan";
    case "memory_purge":
      return "Purge cache";
  }
}

export interface ScanSummary {
  total: number;
  selectable: number;
  low: number;
  medium: number;
  targeted: number;
  memoryBytes: number;
}

/** Headline figures for a report; memory counts only what can still be added. */
export function summarize(items: Recommendation[]): ScanSummary {
  const summary: ScanSummary = {
    total: items.length,
    selectable: 0,
    low: 0,
    medium: 0,
    targeted: 0,
    memoryBytes: 0,
  };
  for (const item of items) {
    if (item.already_targeted) {
      summary.targeted += 1;
      continue;
    }
    summary.selectable += 1;
    if (item.risk === "low") summary.low += 1;
    else summary.medium += 1;
    if (item.kind.kind === "process") summary.memoryBytes += item.memory_bytes;
  }
  return summary;
}
