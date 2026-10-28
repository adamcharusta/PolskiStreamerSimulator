import { useTranslation } from 'react-i18next';

import type { PronounSet, RollView } from '@/api/generated/game.schemas';
import { formatContent } from '@/content/icu';

/** Names the event a roll belongs to: its authored title, an M1 translation, the action, or the bare ID. */
export function useRollName() {
  const { t } = useTranslation();
  return (roll: RollView, pronouns: PronounSet) => {
    if (roll.eventTitle) {
      return formatContent(roll.eventTitle, pronouns);
    }

    if (roll.eventId === 'network' || roll.eventId === 'pitch') {
      return t(`roll.action.${roll.eventId}`);
    }

    return t(`event.${roll.eventId}.title`, { defaultValue: roll.eventId });
  };
}
