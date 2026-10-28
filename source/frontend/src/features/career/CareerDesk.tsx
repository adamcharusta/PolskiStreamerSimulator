import { useTranslation } from 'react-i18next';

import { type CareerView, NextStepKind } from '@/api/generated/game.schemas';

import { DashboardCard } from './DashboardCard';
import { OffersCard } from './OffersCard';
import { PlatformCard } from './PlatformCard';
import { Stage } from './Stage';
import { StatsRail } from './StatsRail';
import { TopBar } from './TopBar';

/** The ruleset whose careers have platforms, strikes, bans and sponsor deals (M3-B). */
const ECONOMY_RULESET = 'm3-systems.2';

/**
 * Layout A: the top bar, the wide stage and the statistics rail (a drawer on narrow screens). While a week is
 * planned, an M3-B career shows its platform and its sponsor offers and deals above the stage.
 */
export function CareerDesk({
  career,
  secret,
}: {
  readonly career: CareerView;
  readonly secret: string;
}) {
  const { t } = useTranslation();
  const planning = career.next.kind === NextStepKind.planWeek;
  return (
    <div className="grid gap-3">
      <h1 className="sr-only">
        {t('career.summaryLabel')}: {career.nickname}
      </h1>
      <TopBar career={career} />
      <div className="desk">
        <div className="grid min-w-0 content-start gap-3">
          <DashboardCard career={career} />
          {planning && career.rulesetId === ECONOMY_RULESET && <PlatformCard career={career} />}
          {planning && <OffersCard key={career.version} career={career} secret={secret} />}
          <Stage key={career.careerId} career={career} secret={secret} />
        </div>
        <StatsRail career={career} />
      </div>
    </div>
  );
}
