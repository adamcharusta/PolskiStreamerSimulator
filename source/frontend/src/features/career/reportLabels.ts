import type { TFunction } from 'i18next';

import i18n from '@/i18n';

const camel = (pascal: string): string => pascal.slice(0, 1).toLowerCase() + pascal.slice(1);

/** `income:subscriptions` → "Suby"; an unknown code shows as itself rather than disappearing. */
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

/** Why a stat changed: the week's action, the stream, practice, or an event by its title. */
export function causeLabel(t: TFunction, detail: string | null): string | null {
  if (detail === null) {
    return null;
  }

  if (i18n.exists(`report.cause.${detail}`)) {
    return t(`report.cause.${detail}`);
  }

  return i18n.exists(`event.${detail}.title`) ? t(`event.${detail}.title`) : detail;
}
