import { describe, expect, it, vi } from 'vitest';

import { saveTheme, storedTheme } from './theme';

describe('theme', () => {
  it('falls back to the overlay theme for an unknown stored value', () => {
    localStorage.setItem('pss.theme', 'neon');

    expect(storedTheme()).toBe('overlay');
  });

  it('keeps a known stored theme', () => {
    localStorage.setItem('pss.theme', 'web2005');

    expect(storedTheme()).toBe('web2005');
  });

  it('uses the overlay theme when browser storage is unavailable', () => {
    const getItem = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('Storage disabled', 'SecurityError');
    });

    try {
      expect(storedTheme()).toBe('overlay');
    } finally {
      getItem.mockRestore();
    }
  });

  it('applies and stores a chosen theme', () => {
    saveTheme('win98');

    expect(document.documentElement.dataset.theme).toBe('win98');
    expect(localStorage.getItem('pss.theme')).toBe('win98');
  });

  it('still applies a chosen theme when browser storage is unavailable', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Storage disabled', 'SecurityError');
    });

    try {
      expect(() => {
        saveTheme('web2005');
      }).not.toThrow();
      expect(document.documentElement.dataset.theme).toBe('web2005');
    } finally {
      setItem.mockRestore();
    }
  });
});
