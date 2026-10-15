import { describe, expect, it } from 'vitest';

import { createCareerSchema } from './schema';

const form = (nickname: string) => ({
  nickname,
  background: 'dormRoomNewcomer',
  difficulty: 'normal',
  pronouns: 'woman',
});

describe('the nickname rule mirrors the server (TD-92)', () => {
  it.each(['Streamer', 'Zażółć Gęślą', 'a_b-c.d', '  Trim Me  ', 'abc', 'x'.repeat(20)])(
    'accepts %j',
    (nickname) => {
      expect(createCareerSchema.safeParse(form(nickname)).success).toBe(true);
    },
  );

  it.each(['ab', 'x'.repeat(21), 'emoji 😀', 'a<b>', ' '.repeat(3)])('rejects %j', (nickname) => {
    expect(createCareerSchema.safeParse(form(nickname)).success).toBe(false);
  });

  it('stores the nickname trimmed', () => {
    expect(createCareerSchema.parse(form('  Trim Me  ')).nickname).toBe('Trim Me');
  });
});
