import { useTranslation } from 'react-i18next';

import type { CareerView } from '@/api/generated/game.schemas';

import { Stage } from './Stage';
import { StatsRail } from './StatsRail';
import { TopBar } from './TopBar';

/** Layout A: the top bar, the wide stage and the statistics rail (a drawer on narrow screens). */
export function CareerDesk({
  career,
  secret,
}: {
  readonly career: CareerView;
  readonly secret: string;
}) {
  const { t } = useTranslation();
  return (
    <div className="grid gap-3">
      <h1 className="sr-only">
        {t('career.summaryLabel')}: {career.nickname}
      </h1>
      <TopBar career={career} />
      <div className="desk">
        <Stage key={career.careerId} career={career} secret={secret} />
        <StatsRail career={career} />
      </div>
    </div>
  );
}
