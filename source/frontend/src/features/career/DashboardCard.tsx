import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import type { CareerView } from '@/api/generated/game.schemas';
import { count, date } from '@/format';

import { CareerMode } from './CareerMode';
import { ageAt, nextMilestone } from './dashboard';

/** Where the career stands (content §3.4): week, age, platform, obligations due, next milestone and the history link. */
export function DashboardCard({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  const due = career.contracts.filter((deal) => deal.dueThisWeek);
  const next = nextMilestone(career.stats.followers);
  const title = t('dashboard.title');
  return (
    <section className="panel grid gap-1" aria-label={title}>
      <h2 className="panel-title">{title}</h2>
      <p className="m-0">
        {t('dashboard.week', { week: career.week, age: ageAt(career.week) })} ·{' '}
        {t(`platform.${career.platform}`)}
      </p>
      <p className="m-0 muted">
        <CareerMode career={career} />
      </p>
      <p className="m-0">
        {due.length === 0
          ? t('dashboard.noObligations')
          : t('dashboard.obligations', { list: due.map((deal) => deal.name).join(', ') })}
      </p>
      {next !== null && (
        <p className="m-0 muted">
          {t('dashboard.nextMilestone', {
            target: count(next),
            missing: count(next - career.stats.followers),
          })}
        </p>
      )}
      {career.expiresAt != null && (
        <p className="m-0 muted">{t('dashboard.expires', { date: date(career.expiresAt) })}</p>
      )}
      <p className="m-0 flex flex-wrap gap-3">
        <Link to="/kariera/$careerId/historia" params={{ careerId: career.careerId }}>
          {t('dashboard.history')}
        </Link>
        <Link to="/kariera/$careerId/klucz" params={{ careerId: career.careerId }}>
          {t('dashboard.key')}
        </Link>
      </p>
    </section>
  );
}
