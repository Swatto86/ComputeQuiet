/** The Scan view: what could be parked right now, and one click to do it. */
import {
  api,
  errorMessage,
  type Recommendation,
  type ScanReport,
  type Settings,
} from "./bridge.ts";
import { toast } from "./dialog.ts";
import { formatBytes, formatPercent } from "./format.ts";
import {
  defaultSelection,
  kindLabel,
  selectedItems,
  summarize,
} from "./scan-select.ts";

function byId<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);
  if (!element) throw new Error(`missing #${id}`);
  return element as T;
}

export interface ScanHost {
  /** The new settings after finds were added to the targets. */
  onSettings(settings: Settings): void;
  /** Switch Quiet Mode on now (the dashboard's toggle). */
  goQuiet(): Promise<void>;
}

export class Scan {
  private report: ScanReport | null = null;
  private selected = new Set<number>();
  private busy = false;

  constructor(private readonly host: ScanHost) {
    byId("scan-run").addEventListener("click", () => void this.refresh());
    byId("scan-apply").addEventListener("click", () => void this.apply(false));
    byId("scan-apply-quiet").addEventListener(
      "click",
      () => void this.apply(true),
    );
    this.render();
  }

  async refresh(): Promise<void> {
    if (this.busy) return;
    this.setBusy(true, "Scanning…");
    try {
      this.report = await api.scan();
      this.selected = defaultSelection(this.report.recommendations);
    } catch (error) {
      toast(`Scan failed: ${errorMessage(error)}`, true);
    } finally {
      this.setBusy(false, "");
      this.render();
    }
  }

  private async apply(thenQuiet: boolean): Promise<void> {
    if (!this.report || this.busy) return;
    const accepted = selectedItems(this.report.recommendations, this.selected);
    if (accepted.length === 0) return;
    this.setBusy(true, "Adding…");
    try {
      const settings = await api.applyRecommendations(accepted);
      this.host.onSettings(settings);
      toast(`${accepted.length} added to targets`);
      this.report = await api.scan();
      this.selected = defaultSelection(this.report.recommendations);
    } catch (error) {
      toast(errorMessage(error), true);
    } finally {
      this.setBusy(false, "");
      this.render();
    }
    if (thenQuiet) await this.host.goQuiet();
  }

  private setBusy(busy: boolean, status: string): void {
    this.busy = busy;
    byId<HTMLButtonElement>("scan-run").disabled = busy;
    byId("scan-status").textContent = status;
  }

  private render(): void {
    const rows = byId<HTMLTableSectionElement>("scan-rows");
    const summary = byId("scan-summary");
    const items = this.report?.recommendations ?? [];
    const picked = selectedItems(items, this.selected).length;
    byId<HTMLButtonElement>("scan-apply").disabled = picked === 0 || this.busy;
    byId<HTMLButtonElement>("scan-apply-quiet").disabled =
      picked === 0 || this.busy;

    if (!this.report) {
      summary.textContent = "Press Scan to see what could be parked right now.";
      rows.replaceChildren(this.emptyRow("No scan yet."));
      return;
    }
    const counts = summarize(items);
    const parts = [
      `${counts.selectable} new find${counts.selectable === 1 ? "" : "s"}`,
      `${counts.low} low risk`,
      `${counts.medium} medium risk`,
      `${counts.targeted} already targeted`,
      `${formatBytes(counts.memoryBytes)} in programs not yet parked`,
    ];
    if (this.report.cached_bytes > 0)
      parts.push(`${formatBytes(this.report.cached_bytes)} cached`);
    if (!this.report.activity_known)
      parts.push("unknown programs are not guessed on this platform");
    summary.textContent = parts.join(" · ");

    if (items.length === 0) {
      rows.replaceChildren(
        this.emptyRow(
          "Nothing to add: your targets already cover what is running.",
        ),
      );
      return;
    }
    rows.replaceChildren(...items.map((item, index) => this.row(item, index)));
  }

  private row(item: Recommendation, index: number): HTMLTableRowElement {
    const row = document.createElement("tr");
    if (item.already_targeted) row.className = "targeted";
    const tick = document.createElement("td");
    const input = document.createElement("input");
    input.type = "checkbox";
    input.checked = this.selected.has(index) && !item.already_targeted;
    input.disabled = item.already_targeted;
    input.setAttribute("aria-label", `Park ${item.name}`);
    input.addEventListener("change", () => {
      if (input.checked) this.selected.add(index);
      else this.selected.delete(index);
      this.render();
    });
    tick.appendChild(input);

    const name = document.createElement("td");
    name.className = "name";
    name.textContent =
      item.instances > 1 ? `${item.name} ×${item.instances}` : item.name;
    const action = document.createElement("td");
    action.textContent = item.already_targeted
      ? "already a target"
      : kindLabel(item.kind);
    const why = document.createElement("td");
    why.className = "why";
    why.textContent = item.reason;
    const risk = document.createElement("td");
    const chip = document.createElement("span");
    chip.className = `risk risk-${item.risk}`;
    chip.textContent = item.risk === "low" ? "low" : "medium";
    risk.appendChild(chip);
    const memory = document.createElement("td");
    memory.className = "num";
    memory.textContent =
      item.memory_bytes > 0 ? formatBytes(item.memory_bytes) : "—";
    const cpu = document.createElement("td");
    cpu.className = "num";
    cpu.textContent =
      item.kind.kind === "process" ? formatPercent(item.cpu_percent) : "—";
    row.append(tick, name, action, why, risk, memory, cpu);
    return row;
  }

  private emptyRow(text: string): HTMLTableRowElement {
    const row = document.createElement("tr");
    const cell = document.createElement("td");
    cell.colSpan = 7;
    cell.className = "muted";
    cell.textContent = text;
    row.appendChild(cell);
    return row;
  }
}
