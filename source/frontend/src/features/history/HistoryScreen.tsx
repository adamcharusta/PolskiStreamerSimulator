import { Link, useParams } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useGetHistory, useGetHistoryWeek } from '@/api/generated/game';
import { PronounSet, type WeekSummaryView } from '@/api/generated/game.schemas';
import { careerAuth, careerSecret } from '@/careers/knownCareers';
import { Button } from '@/components/ui/Button';
import { WeekReport } from '@/features/career/stage/WeekReport';
import { useCareer } from '@/features/career/useCareer';
import { count, pln, signed } from '@/format';

import { filterWeeks, type HistoryFilter, yearlyReviews } from './yearly';

/** The incident tags the server sends, in the order the filter lists them. */
const tags = [
  'viral',
  'decision',
  'strike',
  'ban',
  'migration',
  'wager',
  'sponsorPaid',
  'sponsorPenalty',
  'shortStream',
  'ending',
];

const number = (raw: string) => (raw === '' ? null : Number(raw));

const emptyFilter: HistoryFilter = { fromWeek: null, toWeek: null, tag: null, text: '' };

/** The career history (#24): yearly reviews, filters and the settled weeks; a week opens its full stored report. */
export function HistoryScreen() {
  const { careerId } = useParams({ from: '/kariera/$careerId/historia' });
  const { t } = useTranslation();
  const secret = careerSecret(careerId);
  const auth = secret === null ? undefined : careerAuth(secret);
  const history = useGetHistory(careerId, { query: { enabled: secret !== null }, request: auth });
  const pronouns = useCareer(careerId).query.data?.pronouns ?? PronounSet.nonbinary;
  const [filter, setFilter] = useState<HistoryFilter>(emptyFilter);
  const [open, setOpen] = useState<number | null>(null);

  const describe = (week: WeekSummaryView) =>
    [
      t(`action.${week.plan.mainAction}`),
      t(`category.${week.plan.category}`),
      ...week.tags.map((tag) => t(`history.tag.${tag}`)),
    ].join(' ');

  if (secret === null || history.isError) {
    return (
      <div role="alert" className="panel">
        <p>{t('history.loadFailed')}</p>
        <Link className="btn btn-secondary" to="/">
          {t('career.backHome')}
        </Link>
      </div>
    );
  }

  if (history.data === undefined) {
    return <output className="block">{t('history.loading')}</output>;
  }

  const weeks = filterWeeks(history.data.weeks, filter, describe);
  return (
    <div className="grid gap-3">
      <h1 className="panel-title">{t('history.title')}</h1>
      <p>
        <Link to="/kariera/$careerId" params={{ careerId }}>
          {t('history.back')}
        </Link>
      </p>

      <section aria-label={t('history.years')} className="grid gap-2 md:grid-cols-2">
        <h2 className="panel-title md:col-span-2">{t('history.years')}</h2>
        {yearlyReviews(history.data.weeks).map((year) => (
          <article
            key={year.year}
            className="panel"
            aria-label={t('history.year', { year: year.year })}
          >
            <h3 className="font-bold">{t('history.year', { year: year.year })}</h3>
            <p className="m-0">
              {t('history.yearSummary', {
                weeks: year.weeks,
                followers: signed(year.followersNet),
                cash: signed(year.cashDelta, pln),
              })}
            </p>
            {year.topCategory !== null && (
              <p className="m-0 muted">
                {t('history.topCategory', { category: t(`category.${year.topCategory}`) })}
              </p>
            )}
            {Object.keys(year.incidents).length > 0 && (
              <p className="m-0 muted">
                {Object.entries(year.incidents)
                  .map(([tag, n]) =>
                    t('history.incident', { tag: t(`history.tag.${tag}`), count: n }),
                  )
                  .join(', ')}
              </p>
            )}
          </article>
        ))}
      </section>

      <fieldset className="panel grid gap-2 md:grid-cols-4">
        <legend className="font-bold">{t('history.filters')}</legend>
        <label className="grid gap-1">
          {t('history.fromWeek')}
          <input
            type="number"
            min={1}
            max={1040}
            value={filter.fromWeek ?? ''}
            onChange={(e) => {
              setFilter({ ...filter, fromWeek: number(e.target.value) });
            }}
          />
        </label>
        <label className="grid gap-1">
          {t('history.toWeek')}
          <input
            type="number"
            min={1}
            max={1040}
            value={filter.toWeek ?? ''}
            onChange={(e) => {
              setFilter({ ...filter, toWeek: number(e.target.value) });
            }}
          />
        </label>
        <label className="grid gap-1">
          {t('history.kind')}
          <select
            value={filter.tag ?? ''}
            onChange={(e) => {
              setFilter({ ...filter, tag: e.target.value === '' ? null : e.target.value });
            }}
          >
            <option value="">{t('history.allKinds')}</option>
            {tags.map((tag) => (
              <option key={tag} value={tag}>
                {t(`history.tag.${tag}`)}
              </option>
            ))}
          </select>
        </label>
        <label className="grid gap-1">
          {t('history.search')}
          <input
            type="search"
            value={filter.text}
            onChange={(e) => {
              setFilter({ ...filter, text: e.target.value });
            }}
          />
        </label>
      </fieldset>

      <p aria-live="polite">{t('history.count', { count: weeks.length })}</p>
      <ol className="grid list-none gap-2 p-0" aria-label={t('history.weeks')}>
        {weeks.map((week) => (
          <li key={week.week} className="panel">
            <div className="flex flex-wrap items-baseline gap-2">
              <span className="font-bold">{t('batch.week', { week: week.week })}</span>
              <span>
                {t('batch.plan', {
                  action: t(`action.${week.plan.mainAction}`),
                  category: t(`category.${week.plan.category}`),
                })}
              </span>
              <span className="muted">
                {t('history.row', {
                  viewers: count(week.liveViewers),
                  followers: signed(week.followersNet),
                  cash: signed(week.cashDelta, pln),
                })}
                {week.automatic && ` · ${t('history.automatic')}`}
              </span>
              {week.tags.length > 0 && (
                <span>{week.tags.map((tag) => t(`history.tag.${tag}`)).join(', ')}</span>
              )}
              <Button
                aria-expanded={open === week.week}
                onClick={() => {
                  setOpen(open === week.week ? null : week.week);
                }}
              >
                {open === week.week ? t('history.hide') : t('history.show')}
              </Button>
            </div>
            {open === week.week && (
              <WeekDetail
                careerId={careerId}
                week={week.week}
                secret={secret}
                pronouns={pronouns}
              />
            )}
          </li>
        ))}
      </ol>
    </div>
  );
}

function WeekDetail({
  careerId,
  week,
  secret,
  pronouns,
}: {
  readonly careerId: string;
  readonly week: number;
  readonly secret: string;
  readonly pronouns: PronounSet;
}) {
  const { t } = useTranslation();
  const report = useGetHistoryWeek(careerId, week, { request: careerAuth(secret) });
  if (report.isError) {
    return <p role="alert">{t('history.weekFailed')}</p>;
  }

  return report.data === undefined ? (
    <output className="block">{t('history.loading')}</output>
  ) : (
    <WeekReport report={report.data} pronouns={pronouns} />
  );
}
