export const themes = ['overlay', 'win98', 'web2005'] as const;

export type Theme = (typeof themes)[number];

const storageKey = 'pss.theme';

export function isTheme(value: unknown): value is Theme {
  return typeof value === 'string' && (themes as readonly string[]).includes(value);
}

/** The stored theme, or the overlay default for a missing or unknown value. */
export function storedTheme(): Theme {
  try {
    const value = localStorage.getItem(storageKey);
    return isTheme(value) ? value : 'overlay';
  } catch {
    return 'overlay';
  }
}

export function applyTheme(theme: Theme): void {
  document.documentElement.dataset.theme = theme;
}

/** Presentation only: the theme never reaches the server or the rules. */
export function saveTheme(theme: Theme): void {
  applyTheme(theme);
  try {
    localStorage.setItem(storageKey, theme);
  } catch {
    // The choice still applies for this page when the browser blocks storage.
  }
}
