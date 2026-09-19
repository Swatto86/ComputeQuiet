/** Boot, wiring and the flows that span views: toggle, quit, elevation. */
import {
  api,
  errorMessage,
  isAppError,
  onConfirmQuit,
  onProgress,
  onState,
  type AppInfo,
  type EngineState,
  type Settings,
} from "./bridge.ts";
import { Dashboard } from "./dashboard.ts";
import { showDialog, toast } from "./dialog.ts";
import { Scan } from "./scan.ts";
import { SettingsView } from "./settings-view.ts";
import { Targets } from "./targets.ts";
import { applyTheme } from "./theme.ts";

function byId<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);
  if (!element) throw new Error(`missing #${id}`);
  return element as T;
}

let engine: EngineState;
let settings: Settings;
let info: AppInfo;
let memoryBaseline: number | null = null;
let busy = false;

const dashboard = new Dashboard(() => void toggle());
let targets: Targets;
let settingsView: SettingsView;
let scanView: Scan;

async function boot(): Promise<void> {
  [settings, engine, info] = await Promise.all([
    api.getSettings(),
    api.getState(),
    api.appInfo(),
  ]);
  applyTheme(settings.theme);

  targets = new Targets(settings.profile, {
    save: async (profile) => saveSettings({ ...settings, profile }),
    defaults: async () => (await api.defaultSettings()).profile,
  });
  settingsView = new SettingsView({
    current: () => settings,
    save: saveSettings,
    quit: () => void quitFlow(),
  });
  scanView = new Scan({
    onSettings: (next) => {
      settings = next;
      targets.setProfile(next.profile);
      renderAll();
    },
    goQuiet: async () => {
      if (!engine.quiet) await toggle();
    },
  });

  renderAll();
  wireTabs();
  wireBanner();

  await onProgress((line) => dashboard.appendLog(line));
  await onState((state) => {
    engine = state;
    renderAll();
  });
  await onConfirmQuit(() => void quitFlow());

  if (engine.startup_error) toast(engine.startup_error, true);
  else if (engine.recovered)
    toast("Recovered a previous Quiet session. Restore puts everything back.");

  await api.frontendReady();
  void pollStats();
  window.setInterval(() => void pollStats(), 2000);
  window.setInterval(() => dashboard.tick(), 15_000);
}

function renderAll(): void {
  dashboard.render(engine);
  targets.describe(engine.capabilities, engine.os);
  settingsView.render(settings, info);
  renderAbout();
  renderBanner();
}

function renderAbout(): void {
  byId("about-version").textContent = `v${info.version}`;
  byId("about-os").textContent = {
    windows: "Windows",
    linux: "Linux",
    mac_os: "macOS",
  }[info.os];
  byId("about-build").textContent = info.debug ? "development" : "release";
  byId("about-admin").textContent = engine.capabilities.elevated
    ? "yes"
    : engine.capabilities.can_elevate
      ? "no — services and the memory purge need it"
      : "not required on this platform";
}

function needsElevation(): boolean {
  const caps = engine.capabilities;
  if (caps.elevated || !caps.can_elevate) return false;
  // A journal recovered from an elevated session holds stopped services that
  // only an elevated copy can start again.
  if (engine.quiet) return engine.summary.services_stopped > 0;
  const profile = settings.profile;
  return profile.services.some((s) => s.enabled) || profile.purge_memory;
}

function renderBanner(): void {
  const banner = byId("banner");
  const text = byId("banner-text");
  const action = byId<HTMLButtonElement>("banner-action");
  if (banner.dataset["dismissed"] === "1") return;
  if (needsElevation()) {
    text.textContent = engine.quiet
      ? "Restoring the stopped services needs administrator rights; the elevated copy picks up this session."
      : "Stopping services and purging memory need administrator rights.";
    action.textContent = "Relaunch as administrator";
    action.hidden = false;
    banner.hidden = false;
  } else {
    banner.hidden = true;
  }
}

function wireBanner(): void {
  byId("banner-action").addEventListener(
    "click",
    () => void relaunchElevated(),
  );
  byId("banner-dismiss").addEventListener("click", () => {
    const banner = byId("banner");
    banner.dataset["dismissed"] = "1";
    banner.hidden = true;
  });
}

async function relaunchElevated(): Promise<void> {
  try {
    await api.relaunchElevated();
  } catch (error) {
    toast(errorMessage(error), true);
  }
}

function wireTabs(): void {
  const tabs = [...document.querySelectorAll<HTMLButtonElement>(".tab")];
  const show = (view: string) => {
    for (const tab of tabs)
      tab.setAttribute("aria-selected", String(tab.dataset["view"] === view));
    for (const section of document.querySelectorAll<HTMLElement>(".view")) {
      section.hidden = section.id !== `view-${view}`;
    }
    if (view === "targets") void targets.refreshRunning();
    if (view === "settings") void settingsView.refreshAutostart();
    if (view === "scan") void scanView.refresh();
  };
  for (const tab of tabs) {
    tab.addEventListener("click", () =>
      show(tab.dataset["view"] ?? "dashboard"),
    );
  }
  document.addEventListener("keydown", (event) => {
    if (!event.ctrlKey || event.key < "1" || event.key > "5") return;
    const tab = tabs[Number(event.key) - 1];
    if (tab) {
      event.preventDefault();
      show(tab.dataset["view"] ?? "dashboard");
      tab.focus();
    }
  });
}

async function saveSettings(next: Settings): Promise<void> {
  await api.saveSettings(next);
  settings = next;
  renderBanner();
}

async function pollStats(): Promise<void> {
  if (document.hidden) return;
  try {
    const stats = await api.getStats();
    const freed =
      engine.quiet && memoryBaseline !== null
        ? Math.max(0, memoryBaseline - stats.memory_used)
        : null;
    dashboard.updateStats(stats, freed);
    if (!engine.quiet) memoryBaseline = stats.memory_used;
  } catch {
    // The next poll will report; a missed sample is not worth a toast.
  }
}

async function toggle(): Promise<void> {
  if (busy) return;
  busy = true;
  dashboard.setBusy(true);
  try {
    engine = engine.quiet ? await api.restore() : await api.goQuiet();
    if (engine.quiet && engine.log.some((line) => !line.ok)) {
      toast("Some steps failed; see Activity.", true);
    }
  } catch (error) {
    toast(errorMessage(error), true);
    if (isAppError(error) && error.code === "needs_elevation") {
      const banner = byId("banner");
      delete banner.dataset["dismissed"];
    }
    engine = await api.getState();
  } finally {
    busy = false;
    dashboard.setBusy(false);
    renderAll();
  }
}

async function quitFlow(): Promise<void> {
  if (!engine.quiet) {
    await api.quit(false);
    return;
  }
  const choice = await showDialog({
    title: "Quiet Mode is still on",
    body: "Restore the services, programs and power plan before quitting? Quitting without restoring leaves them parked; the journal is kept so you can restore next time.",
    buttons: [
      {
        label: "Restore and quit",
        value: "restore",
        primary: settings.restore_on_quit,
      },
      {
        label: "Quit without restoring",
        value: "quit",
        danger: true,
        primary: !settings.restore_on_quit,
      },
      { label: "Cancel", value: "cancel" },
    ],
    cancel: "cancel",
  });
  if (choice === "cancel") return;
  try {
    await api.quit(choice === "restore");
  } catch (error) {
    toast(errorMessage(error), true);
    engine = await api.getState();
    renderAll();
  }
}

void boot().catch((error: unknown) => {
  toast(`ComputeQuiet could not start: ${errorMessage(error)}`, true);
  void api.frontendReady();
});
