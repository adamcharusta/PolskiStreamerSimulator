import { describe, expect, it, vi } from 'vitest';

import {
  beginCreation,
  clearPendingCreation,
  pendingCreation,
  pendingCreationLifetimeMs,
} from './pendingCreation';

const request = {
  nickname: 'Streamer',
  background: 'dormRoomNewcomer',
  difficulty: 'normal',
  pronouns: 'woman',
} as const;

describe('pending creation', () => {
  it('keeps the request under a fresh creation ID until it is cleared', () => {
    const pending = beginCreation(request);

    expect(pending?.creationId).toMatch(/^[0-9a-f-]{36}$/u);
    expect(pendingCreation()).toEqual(pending);

    clearPendingCreation();
    expect(pendingCreation()).toBeNull();
  });

  it('gives every creation its own ID', () => {
    const first = beginCreation(request);
    const second = beginCreation(request);

    expect(first?.creationId).not.toBe(second?.creationId);
    expect(pendingCreation()?.creationId).toBe(second?.creationId);
  });

  it('drops a pending creation the server no longer repeats', () => {
    const pending = beginCreation(request);
    const started = Date.parse(pending?.startedAt ?? '');

    expect(pendingCreation(started + pendingCreationLifetimeMs - 1)).not.toBeNull();
    expect(pendingCreation(started + pendingCreationLifetimeMs)).toBeNull();
    expect(localStorage.getItem('pss.pendingCreation.v1')).toBeNull();
  });

  it('reads malformed storage as no pending creation', () => {
    localStorage.setItem('pss.pendingCreation.v1', '{"creationId":1}');
    expect(pendingCreation()).toBeNull();

    localStorage.setItem('pss.pendingCreation.v1', 'not json');
    expect(pendingCreation()).toBeNull();

    localStorage.setItem(
      'pss.pendingCreation.v1',
      JSON.stringify({ creationId: 'x', startedAt: 'never', request }),
    );
    expect(pendingCreation()).toBeNull();
  });

  it('reports a browser that cannot store the creation', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('Storage disabled', 'SecurityError');
    });
    const removeItem = vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
      throw new DOMException('Storage disabled', 'SecurityError');
    });
    try {
      expect(beginCreation(request)).toBeNull();
      expect(() => {
        clearPendingCreation();
      }).not.toThrow();
    } finally {
      setItem.mockRestore();
      removeItem.mockRestore();
    }
  });
});
