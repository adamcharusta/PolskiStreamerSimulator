import { useTranslation } from 'react-i18next';

import { PronounSet, type RollView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';
import { formatContent } from '@/content/icu';

import { rollText } from '../reportLabels';
import { useRollName } from '../rollName';

const gains = new Set(['success', 'criticalSuccess']);

export function RollResult({
  roll,
  onNext,
  pronouns = PronounSet.nonbinary,
}: {
  readonly roll: RollView;
  readonly onNext: () => void;
  readonly pronouns?: PronounSet;
}) {
  const { t } = useTranslation();
  const name = useRollName();
  const outcome = t(`roll.outcome.${roll.outcome}`);
  const good = gains.has(roll.outcome);
  return (
    <div className="grid gap-3">
      <StepHeading>{t('roll.title', { outcome })}</StepHeading>
      <p className="font-bold">
        {name(roll, pronouns)}
        {roll.choiceLabel && `: ${formatContent(roll.choiceLabel, pronouns)}`}
      </p>
      <p>{rollText(t, roll)}</p>
      {roll.outcomeText && <p>{formatContent(roll.outcomeText, pronouns)}</p>}
      <p className={good ? 'good font-bold' : 'bad font-bold'}>
        <span aria-hidden="true">{good ? '✔ ' : '✖ '}</span>
        {outcome}
      </p>
      <Button variant="primary" onClick={onNext}>
        {t('roll.next')}
      </Button>
    </div>
  );
}
