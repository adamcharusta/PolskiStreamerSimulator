import { useTranslation } from 'react-i18next';

import type { PronounSet, RollView } from '@/api/generated/game.schemas';
import { formatContent } from '@/content/icu';
import { sensitiveTag, useSensitiveFilter } from '@/textFilters/textFilters';

/**
 * Names the event a roll belongs to: its authored title (or the Sensitive content filter's neutral title), an M1
 * translation, the action, or the bare ID.
 */
export function useRollName() {
  const { t } = useTranslation();
  const sensitiveOn = useSensitiveFilter();
  return (roll: RollView, pronouns: PronounSet) => {
    const sensitive = sensitiveTag(roll.tags, sensitiveOn);
    if (sensitive !== null) {
      return t(`filters.sensitiveText.${sensitive}.title`);
    }

    if (roll.eventTitle) {
      return formatContent(roll.eventTitle, pronouns);
    }

    if (roll.eventId === 'network' || roll.eventId === 'pitch') {
      return t(`roll.action.${roll.eventId}`);
    }

    return t(`event.${roll.eventId}.title`, { defaultValue: roll.eventId });
  };
}

/** A roll's authored outcome text, or the Sensitive content filter's neutral wording, formatted for the pronouns. */
export function useOutcomeText() {
  const { t } = useTranslation();
  const sensitiveOn = useSensitiveFilter();
  return (roll: RollView, pronouns: PronounSet): string | null => {
    if (!roll.outcomeText) {
      return null;
    }

    const sensitive = sensitiveTag(roll.tags, sensitiveOn);
    return sensitive === null
      ? formatContent(roll.outcomeText, pronouns)
      : t(`filters.sensitiveText.${sensitive}.outcome`);
  };
}
