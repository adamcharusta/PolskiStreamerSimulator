import {
  CategoryPlan,
  MainAction,
  RiskAppetite,
  type StrategyInput,
} from '@/api/generated/game.schemas';

/** The batch lengths the server accepts (GDD: 4, 13 or 52 weeks). */
export const batchLengths = [4, 13, 52] as const;

export type BatchLength = (typeof batchLengths)[number];

/** The actions a routine step may hold (spec 2026-10-26 §5). */
export const routineActions = [
  MainAction.noAction,
  MainAction.trainCategory,
  MainAction.manageCommunity,
  MainAction.networkCollaborate,
  MainAction.pitchFulfillSponsor,
] as const;

/** The strategy the dialog starts from: the server's defaults, close to the Balanced policy. */
export const defaultStrategy: StrategyInput = {
  categoryPlan: CategoryPlan.strongest,
  focus: null,
  routine: [
    MainAction.noAction,
    MainAction.noAction,
    MainAction.noAction,
    MainAction.trainCategory,
  ],
  restBelowEnergy: 30,
  restAboveBurnout: 70,
  reservePln: 0,
  upgrades: true,
  risk: RiskAppetite.balanced,
  acceptOffers: true,
  wager: null,
  sideBusiness: false,
};

const storageKey = (careerId: string) => `pss.strategy.${careerId}`;

/** Why the strategy would be refused, as the server's codes; empty when it is valid. */
export function strategyErrors(s: StrategyInput): string[] {
  const errors: string[] = [];
  if (s.categoryPlan === CategoryPlan.focus && s.focus === null) {
    errors.push('focus-missing');
  }

  if (s.routine.length === 0 || s.routine.length > 8) {
    errors.push('routine-length');
  }

  if (!Number.isInteger(s.restBelowEnergy) || s.restBelowEnergy < 0 || s.restBelowEnergy > 80) {
    errors.push('energy-range');
  }

  if (
    !Number.isInteger(s.restAboveBurnout) ||
    s.restAboveBurnout < 50 ||
    s.restAboveBurnout > 100
  ) {
    errors.push('burnout-range');
  }

  if (!Number.isInteger(s.reservePln) || s.reservePln < 0 || s.reservePln > 1_000_000) {
    errors.push('reserve-range');
  }

  return errors;
}

/** The strategy sent in the request: a focus only with the focus plan. */
export function normalised(s: StrategyInput): StrategyInput {
  return s.categoryPlan === CategoryPlan.focus ? s : { ...s, focus: null };
}

/** The last strategy this browser used for the career, or the defaults (also when storage is blocked). */
export function storedStrategy(careerId: string): StrategyInput {
  try {
    const raw = localStorage.getItem(storageKey(careerId));
    if (raw === null) {
      return defaultStrategy;
    }

    const parsed = { ...defaultStrategy, ...(JSON.parse(raw) as Partial<StrategyInput>) };
    return strategyErrors(parsed).length === 0 ? parsed : defaultStrategy;
  } catch {
    return defaultStrategy;
  }
}

/** Remembers the strategy per career; the server keeps every strategy it played in the batch requests. */
export function saveStrategy(careerId: string, s: StrategyInput): void {
  try {
    localStorage.setItem(storageKey(careerId), JSON.stringify(s));
  } catch {
    // Without storage the dialog starts from the defaults next time.
  }
}
