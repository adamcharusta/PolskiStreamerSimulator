import { useTranslation } from 'react-i18next';

import type { RollView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';

import { rollText } from '../reportLabels';

const gains = new Set(['success', 'criticalSuccess']);

export function RollResult({
  roll,
  onNext,
}: {
  readonly roll: RollView;
  readonly onNext: () => void;
}) {
  const { t } = useTranslation();
  const outcome = t(`roll.outcome.${roll.outcome}`);
  const good = gains.has(roll.outcome);
  return (
    <div className="grid gap-3">
      <StepHeading>{t('roll.title', { outcome })}</StepHeading>
      <p>{rollText(t, roll)}</p>
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
