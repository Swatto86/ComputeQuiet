/**
 * An in-app dialog and a toast. Browser `confirm` is not used: it cannot be
 * styled, breaks keyboard focus in a webview, and gives no control over the
 * button labels — "Restore and quit" versus "Quit without restoring" is the
 * whole point.
 */

export interface DialogButton {
  label: string;
  value: string;
  primary?: boolean;
  danger?: boolean;
}

export interface DialogOptions {
  title: string;
  body: string;
  buttons: DialogButton[];
  /** Value returned on Escape or a click outside; defaults to the last button. */
  cancel?: string;
}

export function showDialog(options: DialogOptions): Promise<string> {
  const root = document.getElementById("dialog-root");
  if (!root) return Promise.reject(new Error("no #dialog-root"));
  const previous =
    document.activeElement instanceof HTMLElement
      ? document.activeElement
      : null;
  const cancelValue =
    options.cancel ?? options.buttons[options.buttons.length - 1]?.value ?? "";

  return new Promise((resolve) => {
    const overlay = document.createElement("div");
    overlay.className = "dialog-overlay";
    const dialog = document.createElement("div");
    dialog.className = "dialog";
    dialog.setAttribute("role", "dialog");
    dialog.setAttribute("aria-modal", "true");
    const title = document.createElement("h2");
    title.textContent = options.title;
    title.id = "dialog-title";
    dialog.setAttribute("aria-labelledby", title.id);
    const body = document.createElement("p");
    body.textContent = options.body;
    const buttons = document.createElement("div");
    buttons.className = "dialog-buttons";

    const finish = (value: string) => {
      document.removeEventListener("keydown", onKey);
      overlay.remove();
      previous?.focus();
      resolve(value);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        finish(cancelValue);
      }
      if (event.key === "Tab") {
        const focusable = [
          ...buttons.querySelectorAll<HTMLButtonElement>("button"),
        ];
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (!first || !last) return;
        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault();
          last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault();
          first.focus();
        }
      }
    };

    let focusTarget: HTMLButtonElement | null = null;
    for (const spec of options.buttons) {
      const button = document.createElement("button");
      button.type = "button";
      button.textContent = spec.label;
      if (spec.primary) button.classList.add("primary");
      if (spec.danger) button.classList.add("danger");
      button.addEventListener("click", () => finish(spec.value));
      buttons.appendChild(button);
      if (spec.primary || !focusTarget) focusTarget = button;
    }

    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) finish(cancelValue);
    });
    document.addEventListener("keydown", onKey);
    dialog.append(title, body, buttons);
    overlay.appendChild(dialog);
    root.appendChild(overlay);
    focusTarget?.focus();
  });
}

let toastTimer: number | undefined;

export function toast(message: string, isError = false): void {
  const element = document.getElementById("toast");
  if (!element) return;
  element.textContent = message;
  element.classList.toggle("error", isError);
  element.hidden = false;
  window.clearTimeout(toastTimer);
  toastTimer = window.setTimeout(
    () => {
      element.hidden = true;
    },
    isError ? 8000 : 4000,
  );
}
