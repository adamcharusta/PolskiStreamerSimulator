import { Link } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useListSeasons } from '@/api/generated/game';
import { knownCareers } from '@/careers/knownCareers';
import { Panel } from '@/components/ui/Panel';
import { PendingCreationPanel } from '@/features/create-career/PendingCreationPanel';
import { date } from '@/format';

export function HomeScreen() {
  const { t } = useTranslation();
  const careers = knownCareers();
  return (
    <div className="grid gap-3">
      <h1 className="heading text-3xl">{t('app.title')}</h1>
      <PendingCreationPanel />
      <Panel title={t('home.careers')}>
        {careers.length === 0 ? (
          <p>{t('home.empty')}</p>
        ) : (
          <ul className="grid list-none gap-2 p-0">
            {careers.map((career) => (
              <li key={career.careerId} className="choice justify-between">
                <span className="font-bold">{career.nickname}</span>
                <span className="muted">{t('home.started', { date: date(career.createdAt) })}</span>
                <Link
                  className="btn btn-secondary"
                  to="/kariera/$careerId"
                  params={{ careerId: career.careerId }}
                  aria-label={t('home.continueNamed', { nickname: career.nickname })}
                >
                  {t('home.continue')}
                </Link>
              </li>
            ))}
          </ul>
        )}
        <p className="my-3">
          <Link className="btn btn-primary" to="/nowa-kariera">
            {t('home.new')}
          </Link>
          <span className="new-label" aria-hidden="true">
            {t('decor.new')}
          </span>
        </p>
        <p className="my-3 flex flex-wrap gap-2">
          <Link className="btn btn-secondary" to="/odzyskaj">
            {t('home.recover')}
          </Link>
          <Link className="btn btn-secondary" to="/ranking">
            {t('home.leaderboard')}
          </Link>
          <Link className="btn btn-secondary" to="/importuj">
            {t('home.import')}
          </Link>
        </p>
        <p className="muted">{t('home.accessNotice')}</p>
        <ExtendedDeadlines />
      </Panel>
    </div>
  );
}

/**
 * Closed seasons whose deadline the owner extended and which still accept results (spec M4-F §3). The list is a
 * notice: when it cannot be loaded the home screen simply shows none.
 */
function ExtendedDeadlines() {
  const { t } = useTranslation();
  const seasons = useListSeasons({ query: { retry: false } });
  const [now] = useState(Date.now);
  const open = (seasons.data ?? []).filter(
    (season) =>
      season.extended &&
      season.submissionDeadline !== null &&
      Date.parse(season.submissionDeadline) > now,
  );
  if (open.length === 0) {
    return null;
  }

  return (
    <ul className="m-0" aria-label={t('home.extendedLabel')}>
      {open.map((season) => (
        <li key={season.number}>
          {t('home.extended', {
            season: season.number,
            date: date(season.submissionDeadline ?? ''),
          })}
        </li>
      ))}
    </ul>
  );
}
