import type { Theme } from "./bridge.ts";

/** The `data-theme` attribute value; `null` removes the override so the OS decides. */
export function themeAttribute(theme: Theme): string | null {
  return theme === "system" ? null : theme;
}

export function applyTheme(theme: Theme): void {
  const value = themeAttribute(theme);
  if (value === null) document.documentElement.removeAttribute("data-theme");
  else document.documentElement.setAttribute("data-theme", value);
}
