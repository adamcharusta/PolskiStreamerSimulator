import {
  type CareerStatsView,
  Category,
  type EquipmentSlot,
  MainAction,
  type PlanOptionView,
} from '@/api/generated/game.schemas';

/** The option the player is about to choose; the slot matters only for an upgrade. */
export function optionFor(
  plans: readonly PlanOptionView[],
  action: MainAction,
  category: Category,
  slot: EquipmentSlot | null,
): PlanOptionView | undefined {
  return plans.find(
    (option) =>
      option.plan.mainAction === action &&
      option.plan.category === category &&
      (action !== MainAction.improveProduction || option.plan.upgradeSlot === slot),
  );
}

/** The actions the career's ruleset offers, each once, in the server's order (an M1 career has no networking). */
export function offeredActions(plans: readonly PlanOptionView[]): MainAction[] {
  return [...new Set(plans.map((option) => option.plan.mainAction))];
}

/** Why an action cannot be chosen in a category, or null when at least one of its options is legal. */
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

  return options[0]?.disabledReason ?? 'unknown';
}

export function strongestCategory(stats: CareerStatsView): Category {
  const [first] = Object.values(Category).toSorted((a, b) => stats.skill[b] - stats.skill[a]);
  return first ?? Category.justChatting;
}
