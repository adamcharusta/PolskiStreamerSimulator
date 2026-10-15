import { useTranslation } from 'react-i18next';

import type { CareerView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { count, pln } from '@/format';

import { useDesk } from './deskStore';
import { type LatestCommand, useLatestCommand } from './useCareerCommand';

function saveStatus(latest: LatestCommand): 'saved' | 'saving' | 'offline' {
  if (latest.status === 'pending') {
    return 'saving';
  }

  return latest.error?.isNetwork === true ? 'offline' : 'saved';
}

export function TopBar({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  const latest = useLatestCommand(career.careerId);
  const statsOpen = useDesk((state) => state.statsOpen);
  const toggleStats = useDesk((state) => state.toggleStats);
  return (
    <section className="topbar" aria-label={t('career.summaryLabel')}>
      <span className="font-bold">{career.nickname}</span>
      <span>{t('career.week', { week: career.week })}</span>
      <span>
        {t('career.cash')}: {pln(career.stats.cashPln)}
      </span>
      <span>
        {t('career.followers')}: {count(career.stats.followers)}
      </span>
      <span>
        {t('career.viewers')}: {count(career.stats.lastLiveViewers)}
      </span>
      <output>{t(`career.${saveStatus(latest)}`)}</output>
      <Button
        className="rail-toggle"
        aria-expanded={statsOpen}
        aria-controls="stats-rail"
        onClick={toggleStats}
      >
        {t('career.stats')}
      </Button>
    </section>
  );
}
