/**
 * Why an offer cannot be accepted, as a translation key: `prerequisite:followers` → `offer.blocked.prerequisite-followers`
 * (a `:` would be read as the i18next namespace separator).
 */
export function blockedKey(reason: string): string {
  return `offer.blocked.${reason.replace(':', '-')}`;
}
