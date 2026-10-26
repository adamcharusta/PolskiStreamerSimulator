import {
  type CareerStatsView,
  Category,
  type EquipmentSlot,
  MainAction,
  type PlanOptionView,
  type Platform,
  type WagerTier,
} from '@/api/generated/game.schemas';

/**
 * The option the player is about to choose; the slot matters only for an upgrade, the wager tier (null for none)
 * and the target platform (null for none) only where the server lists them.
 */
export function optionFor(
  plans: readonly PlanOptionView[],
  action: MainAction,
  category: Category,
  slot: EquipmentSlot | null,
  wager: WagerTier | null = null,
  target: Platform | null = null,
): PlanOptionView | undefined {
  return plans.find(
    (option) =>
      option.plan.mainAction === action &&
      option.plan.category === category &&
      (action !== MainAction.improveProduction || option.plan.upgradeSlot === slot) &&
      option.plan.wager === wager &&
      (action !== MainAction.planPlatformMigration || option.plan.targetPlatform === target),
  );
}

/** The wager tiers the server lists for an action in a category (only Gambling streams have them), in its order. */
export function wagerTiers(
  plans: readonly PlanOptionView[],
  action: MainAction,
  category: Category,
): WagerTier[] {
  return plans
    .filter((option) => option.plan.mainAction === action && option.plan.category === category)
    .flatMap((option) => (option.plan.wager === null ? [] : [option.plan.wager]));
}

/** The platforms a migration plan can target in a category, in the server's order. */
export function migrationTargets(plans: readonly PlanOptionView[], category: Category): Platform[] {
  return plans
    .filter(
      (option) =>
        option.plan.mainAction === MainAction.planPlatformMigration &&
        option.plan.category === category,
    )
    .flatMap((option) => (option.plan.targetPlatform === null ? [] : [option.plan.targetPlatform]));
}

/** The actions the career's ruleset offers, each once, in the server's order (an M1 career has no networking). */
export function offeredActions(plans: readonly PlanOptionView[]): MainAction[] {
  return [...new Set(plans.map((option) => option.plan.mainAction))];
}

/**
 * The reasons that explain a blocked action best come first (#76): the week's own limits, then energy and cash, then
 * the action's requirements; any other reason ranks after these.
 */
const reasonPriority = [
  'decision-pending',
  'rest-only',
  'forced-rest',
  'not-enough-energy',
  'negative-cash',
  'unaffordable',
  'needs-cash',
  'too-early',
  'needs-followers',
  'too-soon',
  'platform-banned',
  'no-sponsor-fits',
  'max-tier',
];

const rank = (reason: string) => {
  const index = reasonPriority.indexOf(reason);
  return index === -1 ? reasonPriority.length : index;
};

/** Why an action cannot be chosen in a category (the most relevant reason of its options), or null when one is legal. */
export function actionBlock(
  plans: readonly PlanOptionView[],
  action: MainAction,
  category: Category,
): string | null {
  const options = plans.filter(
    (option) => option.plan.mainAction === action && option.plan.category === category,
  );
  if (options.some((option) => option.disabledReason === null)) {
    return null;
  }

  const reasons = options.map((option) => option.disabledReason ?? 'unknown');
  return reasons.toSorted((a, b) => rank(a) - rank(b))[0] ?? 'unknown';
}

export function strongestCategory(stats: CareerStatsView): Category {
  const [first] = Object.values(Category).toSorted((a, b) => stats.skill[b] - stats.skill[a]);
  return first ?? Category.justChatting;
}
