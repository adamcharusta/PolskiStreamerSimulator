import type { TFunction } from 'i18next';

import type { ReportLineView, RollView } from '@/api/generated/game.schemas';
import { percent, signed, stat } from '@/format';
import i18n from '@/i18n';

/** The follower change from events, a percentage of the followers rather than a stat (M3). */
const FOLLOWERS_EVENT = 'followers:event';

/** The share of followers that followed a platform move (M3-B); the exact count is in the report's migration. */
const FOLLOWERS_MIGRATION = 'followers:migration';

const camel = (pascal: string): string => pascal.slice(0, 1).toLowerCase() + pascal.slice(1);

/** `income:subscriptions` → "Suby", `cost:sponsor-penalty` → "Kara od sponsora"; an unknown code shows as itself rather than disappearing. */
export function lineLabel(t: TFunction, code: string): string {
  const key = `report.line.${code.replace(':', '.')}`;
  return i18n.exists(key) ? t(key) : code;
}

/** `stat:Energy` → "Energia"; `stat:Skill.Fps` → "Umiejętności · FPS". */
export function statLabel(t: TFunction, code: string): string {
  const [name = '', category] = code.replace(/^stat:/u, '').split('.');
  const key = `stat.${camel(name)}`;
  if (!i18n.exists(key)) {
    return code;
  }

  if (category === undefined) {
    return t(key);
  }

  const categoryKey = `category.${camel(category)}`;
  return `${t(key)} · ${t(categoryKey)}`;
}

/**
 * Why a stat changed: the week's action, the stream, practice, or an M1 event by its title. M3 events carry their text in the content snapshot, not in the translations, so they show as "wydarzenie".
 */
export function causeLabel(t: TFunction, detail: string | null): string | null {
  if (detail === null) {
    return null;
  }

  if (i18n.exists(`report.cause.${detail}`)) {
    return t(`report.cause.${detail}`);
  }

  return i18n.exists(`event.${detail}.title`)
    ? t(`event.${detail}.title`)
    : t('report.cause.event');
}

/** One stat change line: `Energia +25 (akcja tygodnia)`, or the follower percentage from events. */
export function statChangeText(t: TFunction, line: ReportLineView): string {
  const delta = signed(line.delta ?? 0, stat);
  if (line.code === FOLLOWERS_EVENT) {
    return t('report.followersEvent', { value: delta });
  }

  if (line.code === FOLLOWERS_MIGRATION) {
    return t('report.followersMigration', { value: percent(line.delta ?? 0) });
  }

  const cause = causeLabel(t, line.detail);
  const change = `${statLabel(t, line.code)} ${delta}`;
  return cause === null ? change : `${change} (${cause})`;
}

/** The roll against its threshold, or that a guaranteed choice needed none. */
export function rollText(t: TFunction, roll: RollView): string {
  return roll.roll === null
    ? t('roll.noRoll')
    : t('roll.rolled', { roll: roll.roll, threshold: roll.threshold });
}

/** A roll in a report: its outcome in words, then the roll. */
export function rollLine(t: TFunction, roll: RollView): string {
  const outcome = t(`roll.outcome.${roll.outcome}`);
  return `${outcome} — ${rollText(t, roll)}`;
}
