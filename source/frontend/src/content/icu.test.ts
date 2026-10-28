import { describe, expect, it } from 'vitest';

import { PronounSet } from '@/api/generated/game.schemas';

import { formatContent } from './icu';

const text = '{gender, select, female {Zrobiłaś to.} male {Zrobiłeś to.} other {Zrobiłoś to.}}';

describe('formatContent', () => {
  it.each([
    [PronounSet.woman, 'Zrobiłaś to.'],
    [PronounSet.man, 'Zrobiłeś to.'],
    [PronounSet.nonbinary, 'Zrobiłoś to.'],
  ])('picks the %s form', (pronouns, expected) => {
    expect(formatContent(text, pronouns)).toBe(expected);
  });

  it('keeps plain text and Polish quotes', () => {
    expect(formatContent('Czat pisze „F”.', PronounSet.woman)).toBe('Czat pisze „F”.');
  });

  it('reads a doubled apostrophe as one', () => {
    expect(formatContent("Rock''n''roll", PronounSet.man)).toBe("Rock'n'roll");
  });

  it('falls back to the raw text when a message is malformed', () => {
    expect(formatContent('Otwarte { bez końca', PronounSet.man)).toBe('Otwarte { bez końca');
  });
});
