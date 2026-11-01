import { useTranslation } from 'react-i18next';

import {
  PronounSet,
  type ReportLineView,
  type RollView,
  type WeekReportView,
} from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';
import { count, multiplier, percent, pln, signed } from '@/format';

import { lineLabel, statChangeText } from '../reportLabels';
import { RollStory } from '../RollStory';

export interface WeekReportProps {
  readonly report: WeekReportView;
  /** Given when the report is the current step; folded reports (last report, batch ledger) have no button. */
  readonly onNext?: () => void;
  /** The career's pronouns for the authored roll texts; the neutral form without them. */
  readonly pronouns?: PronounSet;
  /** Level of a folded report's headings: 3 under a step heading, 5 inside the batch ledger's week headings. */
  readonly headingLevel?: 3 | 5;
}

type ReportHeading = 'h3' | 'h5';

export function WeekReport({
  report,
  onNext,
  pronouns = PronounSet.nonbinary,
  headingLevel = 3,
}: WeekReportProps) {
  const { t } = useTranslation();
  const title = t('report.title', { week: report.week });
  const Heading: ReportHeading = headingLevel === 5 ? 'h5' : 'h3';
  return (
    <div className="grid gap-2">
      {onNext ? (
        <StepHeading>{title}</StepHeading>
      ) : (
        <Heading className="panel-title">{title}</Heading>
      )}
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
            <dd className="m-0">
              <RollStory roll={report.actionRoll} pronouns={pronouns} />
            </dd>
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
      <Rolls
        title={t('report.decisions')}
        rolls={report.decisionRolls}
        pronouns={pronouns}
        heading={Heading}
      />
      <Rolls
        title={t('report.events')}
        rolls={report.rolls}
        pronouns={pronouns}
        heading={Heading}
      />
      <MoneyLines title={t('report.income')} lines={report.income} sign={1} heading={Heading} />
      <MoneyLines title={t('report.costs')} lines={report.costs} sign={-1} heading={Heading} />
      <Heading className="font-bold">{t('report.statChanges')}</Heading>
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
  heading: Heading,
}: {
  readonly title: string;
  readonly lines: readonly ReportLineView[];
  readonly sign: 1 | -1;
  readonly heading: ReportHeading;
}) {
  const { t } = useTranslation();
  return (
    <>
      <Heading className="font-bold">{title}</Heading>
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

/** Rolls with their event, response, result and authored outcome text; nothing when there were none. */
function Rolls({
  title,
  rolls,
  pronouns,
  heading: Heading,
}: {
  readonly title: string;
  readonly rolls: readonly RollView[];
  readonly pronouns: PronounSet;
  readonly heading: ReportHeading;
}) {
  if (rolls.length === 0) {
    return null;
  }

  return (
    <>
      <Heading className="font-bold">{title}</Heading>
      <ul aria-label={title}>
        {rolls.map((roll, index) => (
          <li key={`${roll.eventId}-${String(index)}`}>
            <RollStory roll={roll} pronouns={pronouns} />
          </li>
        ))}
      </ul>
    </>
  );
}
