import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

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
      </Panel>
    </div>
  );
}
