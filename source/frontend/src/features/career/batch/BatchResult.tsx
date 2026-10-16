import { useTranslation } from 'react-i18next';

import type { BatchOutcome, CareerStatsView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';
import { count, pln, signed, stat } from '@/format';

import { WeekReport } from '../stage/WeekReport';
import { reasonText } from './reasons';

type SummaryKey = keyof Pick<
  CareerStatsView,
  'cashPln' | 'followers' | 'energy' | 'burnout' | 'drama' | 'credibility'
>;

const summaryRows: readonly {
  label: string;
  key: SummaryKey;
  format: (value: number) => string;
}[] = [
  { label: 'career.cash', key: 'cashPln', format: pln },
  { label: 'career.followers', key: 'followers', format: count },
  { label: 'stat.energy', key: 'energy', format: stat },
  { label: 'stat.burnout', key: 'burnout', format: stat },
  { label: 'stat.drama', key: 'drama', format: stat },
  { label: 'stat.credibility', key: 'credibility', format: stat },
];

export function BatchResult({
  outcome,
  onNext,
}: {
  readonly outcome: BatchOutcome;
  readonly onNext: () => void;
}) {
  const { t } = useTranslation();
  const { summary } = outcome;
  return (
    <div className="grid gap-3">
      <StepHeading>{t('batch.title')}</StepHeading>
      <p className="topbar">{t(`batch.stop.${outcome.stopReason}`)}</p>
      <p>{t('batch.weeksCommitted', { value: outcome.weeksCommitted })}</p>

      <table className="w-full">
        <caption className="text-left font-bold">{t('batch.summary')}</caption>
        <thead>
          <tr>
            <td />
            <th scope="col">{t('batch.start')}</th>
            <th scope="col">{t('batch.end')}</th>
          </tr>
        </thead>
        <tbody>
          {summaryRows.map((row) => (
            <tr key={row.key}>
              <th scope="row" className="text-left">
                {t(row.label)}
              </th>
              <td>{row.format(summary.start[row.key])}</td>
              <td>{row.format(summary.end[row.key])}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <dl className="grid grid-cols-[auto_1fr] gap-x-3">
        <dt>{t('report.followersNet')}</dt>
        <dd className="m-0">{signed(summary.followersNet)}</dd>
        <dt>{t('report.cashDelta')}</dt>
        <dd className="m-0">{signed(summary.cashDelta, pln)}</dd>
        <dt>{t('report.income')}</dt>
        <dd className="m-0">{pln(summary.incomePln)}</dd>
        <dt>{t('report.costs')}</dt>
        <dd className="m-0">{pln(summary.costsPln)}</dd>
      </dl>

      <h3 className="font-bold">{t('batch.ledger')}</h3>
      <ol aria-label={t('batch.ledger')} className="grid list-none gap-2 p-0 md:grid-cols-2">
        {outcome.weeks.map((week) => {
          const heading = `batch-week-${String(week.week)}`;
          return (
            <li key={week.week} className="panel" aria-labelledby={heading}>
              <h4 id={heading} className="font-bold">
                {t('batch.week', { week: week.week })}
              </h4>
              <p>
                {t('batch.plan', {
                  action: t(`action.${week.plan.mainAction}`),
                  category: t(`category.${week.plan.category}`),
                })}
              </p>
              <p className="muted">{reasonText(t, week.planReason)}</p>
              {week.choices.length > 0 && (
                <ul>
                  {week.choices.map((choice, index) => (
                    <li key={`${choice.eventId}-${String(index)}`}>
                      {t('batch.choice', {
                        event: t(`event.${choice.eventId}.title`, { defaultValue: choice.eventId }),
                        choice: t(`event.${choice.eventId}.${choice.choiceId}`, {
                          defaultValue: choice.choiceId,
                        }),
                      })}{' '}
                      — {t(`roll.outcome.${choice.roll.outcome}`)}.{' '}
                      <span className="muted">{reasonText(t, choice.reason)}</span>
                    </li>
                  ))}
                </ul>
              )}
              <details>
                <summary>{t('report.title', { week: week.report.week })}</summary>
                <WeekReport report={week.report} />
              </details>
            </li>
          );
        })}
      </ol>
      <Button variant="primary" onClick={onNext}>
        {t('batch.next')}
      </Button>
    </div>
  );
}
