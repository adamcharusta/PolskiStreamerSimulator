import { IntlMessageFormat } from 'intl-messageformat';

import { PronounSet } from '@/api/generated/game.schemas';

const gender: Record<PronounSet, 'female' | 'male' | 'other'> = {
  [PronounSet.woman]: 'female',
  [PronounSet.man]: 'male',
  [PronounSet.nonbinary]: 'other',
};

/**
 * Formats event text from the career's content snapshot (ICU MessageFormat, TD-111) with the career's pronouns.
 * The server validated the message; a malformed one is shown raw rather than breaking the screen.
 */
export function formatContent(text: string, pronouns: PronounSet): string {
  try {
    return String(
      new IntlMessageFormat(text, 'pl-PL', undefined, { ignoreTag: true }).format({
        gender: gender[pronouns],
      }),
    );
  } catch {
    return text;
  }
}
