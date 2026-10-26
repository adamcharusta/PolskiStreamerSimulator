import { useTranslation } from 'react-i18next';

import type { PronounSet, RollView } from '@/api/generated/game.schemas';
import { formatContent } from '@/content/icu';

import { rollLine } from './reportLabels';
import { useRollName } from './rollName';

/**
 * One roll as a story (#82): the event and the chosen response, the outcome against the threshold, then the authored
 * outcome text for the career's pronouns.
 */
export function RollStory({
  roll,
  pronouns,
}: {
  readonly roll: RollView;
  readonly pronouns: PronounSet;
}) {
  const { t } = useTranslation();
  const name = useRollName();
  const label = roll.choiceLabel
    ? formatContent(roll.choiceLabel, pronouns)
    : t(`event.${roll.eventId}.${roll.choiceId}`, { defaultValue: roll.choiceId });
  return (
    <>
      <span>
        {t('roll.story', { event: name(roll, pronouns), choice: label, result: rollLine(t, roll) })}
      </span>
      {roll.outcomeText && (
        <span className="muted"> {formatContent(roll.outcomeText, pronouns)}</span>
      )}
    </>
  );
}
