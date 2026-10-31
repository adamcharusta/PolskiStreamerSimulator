import { describe, expect, it } from 'vitest';

import { findRecoveryKey } from './recoveryKey';

const key = 'pss1.0199e3a0000070008000000000000001.abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123-_6';

describe('recovery key in text', () => {
  it('finds the key inside a key file or pasted text', () => {
    expect(findRecoveryKey(`Klucz kariery Streamer\n${key}\nWklej go na stronie.`)).toBe(key);
    expect(findRecoveryKey(`  ${key}  `)).toBe(key);
  });

  it('finds nothing in text without a whole key', () => {
    expect(findRecoveryKey('pss1.nope')).toBeNull();
    expect(findRecoveryKey(key.slice(0, -2))).toBeNull();
    expect(findRecoveryKey('')).toBeNull();
  });
});
