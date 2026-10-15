import { create } from 'zustand';

import { saveTheme, storedTheme, type Theme } from './theme';

interface ThemeState {
  readonly theme: Theme;
  readonly setTheme: (theme: Theme) => void;
}

export const useTheme = create<ThemeState>()((set) => ({
  theme: storedTheme(),
  setTheme: (theme) => {
    saveTheme(theme);
    set({ theme });
  },
}));
