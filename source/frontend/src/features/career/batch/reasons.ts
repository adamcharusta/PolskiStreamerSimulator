import type { TFunction } from 'i18next';

import type { ReasonView } from '@/api/generated/game.schemas';
import { percent, pln, stat } from '@/format';

import { blockedKey } from '../offers';

const numbers = new Set(['energy', 'burnout', 'drama', 'limit', 'threshold', 'everyWeeks']);
const probabilities = new Set(['probability', 'bestProbability', 'retention']);
const money = new Set(['pricePln', 'cashPln', 'reservePln']);
const labels: Record<string, string> = {
  slot: 'slot',
  category: 'category',
  preferred: 'action',
  categoryRule: 'reason.rule',
  platform: 'platform',
  tier: 'wager',
  action: 'action',
};

/** Policy values arrive as invariant strings (`20.000000`, `0.600000`, `microphone`); show them the Polish way. */
function formatArg(t: TFunction, code: string, name: string, value: string): string {
  if (probabilities.has(name) || (name === 'threshold' && code === 'gamble-best-odds')) {
    return percent(Number(value));
  }

  if (numbers.has(name)) {
    return stat(Number(value));
  }

  if (money.has(name)) {
    return pln(Number(value));
  }

  if (name === 'reason' && code === 'offer-declined') {
    return t(blockedKey(value), {
      defaultValue: t(`offer.incompatibility.${value}`, { defaultValue: value }),
    });
  }

  const prefix = labels[name];
  return prefix === undefined ? value : t(`${prefix}.${value}`, { defaultValue: value });
}

/** Why the automatic strategy decided (spec §7, gameplay §8): the decision, then the category rule when there is one. */
export function reasonText(t: TFunction, reason: ReasonView): string {
  const args = Object.fromEntries(
    Object.entries(reason.args).map(([name, value]) => [
      name,
      formatArg(t, reason.code, name, value),
    ]),
  );
  const key =
    reason.code === 'take-guaranteed' && !('bestProbability' in reason.args)
      ? 'take-guaranteed-only'
      : reason.code;
  const decision = t(`reason.${key}`, { ...args, defaultValue: t('reason.unknown') });
  const { category, categoryRule } = args;
  return category === undefined || categoryRule === undefined
    ? decision
    : `${decision} ${t('reason.category', { category, rule: categoryRule })}`;
}
