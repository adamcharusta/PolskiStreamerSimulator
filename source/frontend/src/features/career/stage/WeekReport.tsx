import { useTranslation } from 'react-i18next';

import type { ReportLineView, RollView, WeekReportView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';
import { count, multiplier, percent, pln, signed } from '@/format';

import { lineLabel, rollLine, statChangeText } from '../reportLabels';

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
        {report.actionRoll !== null && (
          <>
            <dt>{t('report.actionRoll')}</dt>
            <dd className="m-0">{rollLine(t, report.actionRoll)}</dd>
          </>
        )}
      </dl>
      {report.viral !== null && (
        <p className="good font-bold">
          {t('report.viral', {
            multiplier: multiplier(report.viral.multiplier),
            followers: count(report.viral.followersBonus),
            roll: report.viral.roll,
            threshold: report.viral.threshold,
          })}
        </p>
      )}
      <RiskLines report={report} />
      <DecisionRolls rolls={report.decisionRolls} />
      <MoneyLines title={t('report.income')} lines={report.income} sign={1} />
      <MoneyLines title={t('report.costs')} lines={report.costs} sign={-1} />
      <h3 className="font-bold">{t('report.statChanges')}</h3>
      {report.statChanges.length === 0 ? (
        <p>{t('report.none')}</p>
      ) : (
        <ul aria-label={t('report.statChanges')}>
          {report.statChanges.map((line, index) => (
            <li key={`${line.code}-${String(index)}`}>{statChangeText(t, line)}</li>
          ))}
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

/** The wager, the strike roll and a platform move of the week (M3-B); nothing when none happened. */
function RiskLines({ report }: { readonly report: WeekReportView }) {
  const { t } = useTranslation();
  const { wager, strike, migration } = report;
  return (
    <>
      {wager !== null && (
        <p className={wager.won ? 'good' : 'bad'}>
          {t(wager.won ? 'wager.won' : 'wager.lost', {
            net: signed(wager.netPln, pln),
            roll: wager.roll,
            threshold: wager.threshold,
          })}
        </p>
      )}
      {typeof strike?.roll === 'number' && (
        <p className={strike.strike ? 'bad' : undefined}>
          {t('report.strike', {
            roll: strike.roll,
            threshold: strike.threshold,
            result: t(strike.strike ? 'report.strikeHit' : 'report.strikeMiss'),
          })}
        </p>
      )}
      {strike?.banned === true && (
        <p className="bad font-bold">
          {t('report.banned', { platform: t(`platform.${strike.platform}`) })}
        </p>
      )}
      {migration !== null && (
        <p className="font-bold">
          {t('report.migration', {
            from: t(`platform.${migration.from}`),
            to: t(`platform.${migration.to}`),
            before: count(migration.followersBefore),
            after: count(migration.followersAfter),
            retention: percent(migration.retention),
          })}
        </p>
      )}
    </>
  );
}

/** The rolls of the mandatory decisions answered before the week (M3); nothing when there were none. */
function DecisionRolls({ rolls }: { readonly rolls: readonly RollView[] }) {
  const { t } = useTranslation();
  if (rolls.length === 0) {
    return null;
  }

  const title = t('report.decisions');
  return (
    <>
      <h3 className="font-bold">{title}</h3>
      <ul aria-label={title}>
        {rolls.map((roll, index) => (
          <li key={`${roll.eventId}-${String(index)}`}>{rollLine(t, roll)}</li>
        ))}
      </ul>
    </>
  );
}
