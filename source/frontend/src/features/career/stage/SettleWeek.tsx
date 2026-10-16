import { useTranslation } from 'react-i18next';

import type { CareerView, WeekReportView } from '@/api/generated/game.schemas';
import { ActionType } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';

import { RetryBanner } from '../RetryBanner';
import { useActionCommand } from '../useCareerCommand';

export interface SettleWeekProps {
  readonly career: CareerView;
  readonly secret: string;
  readonly onSettled: (report: WeekReportView) => void;
}

export function SettleWeek({ career, secret, onSettled }: SettleWeekProps) {
  const { t } = useTranslation();
  const command = useActionCommand(career.careerId, secret, (outcome) => {
    if (outcome.report !== null) {
      onSettled(outcome.report);
    }
  });
  return (
    <div className="grid gap-3">
      <StepHeading>{t('settle.title')}</StepHeading>
      <p>{t('settle.body')}</p>
      <RetryBanner command={command} />
      <Button
        variant="primary"
        disabled={command.stalled}
        pending={command.pending}
        onClick={() => {
          command.run({ expectedVersion: career.version, type: ActionType.settleWeek });
        }}
      >
        {t('settle.action')}
      </Button>
    </div>
  );
}
