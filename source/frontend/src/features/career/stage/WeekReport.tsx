import { useTranslation } from 'react-i18next';

import type { ReportLineView, WeekReportView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';
import { count, pln, signed, stat } from '@/format';

import { causeLabel, lineLabel, statLabel } from '../reportLabels';

export interface WeekReportProps {
  readonly report: WeekReportView;
  /** Given when the report is the current step; folded reports (last report, batch ledger) have no button. */
  readonly onNext?: () => void;
}

export function WeekReport({ report, onNext }: WeekReportProps) {
  const { t } = useTranslation();
  const title = t('report.title', { week: report.week });
  return (
    <div className="grid gap-2">
      {onNext ? <StepHeading>{title}</StepHeading> : <h3 className="panel-title">{title}</h3>}
      <dl className="grid grid-cols-[auto_1fr] gap-x-3">
        <dt>{t('report.viewers')}</dt>
        <dd className="m-0">{count(report.liveViewers)}</dd>
        <dt>{t('report.followersGained')}</dt>
        <dd className="good m-0">{signed(report.followersGained)}</dd>
        <dt>{t('report.followersLost')}</dt>
        <dd className="bad m-0">{signed(-report.followersLost)}</dd>
        <dt>{t('report.followersNet')}</dt>
        <dd className="m-0">{signed(report.followersNet)}</dd>
        <dt>{t('report.cashDelta')}</dt>
        <dd className="m-0">{signed(report.cashDelta, pln)}</dd>
      </dl>
      <MoneyLines title={t('report.income')} lines={report.income} sign={1} />
      <MoneyLines title={t('report.costs')} lines={report.costs} sign={-1} />
      <h3 className="font-bold">{t('report.statChanges')}</h3>
      {report.statChanges.length === 0 ? (
        <p>{t('report.none')}</p>
      ) : (
        <ul aria-label={t('report.statChanges')}>
          {report.statChanges.map((line, index) => {
            const cause = causeLabel(t, line.detail);
            return (
              <li key={`${line.code}-${String(index)}`}>
                {statLabel(t, line.code)} {signed(line.delta ?? 0, stat)}
                {cause === null ? '' : ` (${cause})`}
              </li>
            );
          })}
        </ul>
      )}
      {report.ending !== null && <p className="font-bold">{t(`career.ending.${report.ending}`)}</p>}
      {onNext && (
        <Button variant="primary" onClick={onNext}>
          {t('report.next')}
        </Button>
      )}
    </div>
  );
}

function MoneyLines({
  title,
  lines,
  sign,
}: {
  readonly title: string;
  readonly lines: readonly ReportLineView[];
  readonly sign: 1 | -1;
}) {
  const { t } = useTranslation();
  return (
    <>
      <h3 className="font-bold">{title}</h3>
      {lines.length === 0 ? (
        <p>{t('report.none')}</p>
      ) : (
        <ul aria-label={title}>
          {lines.map((line, index) => (
            <li key={`${line.code}-${String(index)}`}>
              {lineLabel(t, line.code)} {signed(sign * (line.amountPln ?? 0), pln)}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
