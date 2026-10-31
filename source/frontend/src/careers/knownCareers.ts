import type { AxiosRequestConfig } from 'axios';

/**
 * A career this browser can open with its secret. The recovery key restores it on another device (TD-121); careers
 * created before M4-A, or recovered with a key the player kept elsewhere, may have none stored here.
 */
export interface KnownCareer {
  readonly careerId: string;
  readonly nickname: string;
  readonly secret: string;
  readonly createdAt: string;
  readonly recoveryKey?: string;
}

const storageKey = 'pss.careers.v1';

/** Check persistence before creating a server career whose only access key must be saved here. */
export function canStoreCareer(): boolean {
  try {
    const current = localStorage.getItem(storageKey);
    localStorage.setItem(storageKey, current ?? '[]');
    return true;
  } catch {
    return false;
  }
}

function isKnownCareer(value: unknown): value is KnownCareer {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const c = value as Record<string, unknown>;
  const hasFields = [c.careerId, c.nickname, c.secret, c.createdAt].every(
    (field) => typeof field === 'string' && field.length > 0,
  );
  const hasKey = c.recoveryKey === undefined || typeof c.recoveryKey === 'string';
  return hasFields && hasKey && Number.isFinite(Date.parse(c.createdAt as string));
}

/** Every well-formed stored career, newest first; malformed storage reads as no careers. */
export function knownCareers(): KnownCareer[] {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(storageKey) ?? '[]');
    return Array.isArray(parsed) ? parsed.filter((entry) => isKnownCareer(entry)) : [];
  } catch {
    return [];
  }
}

function save(careers: KnownCareer[]): void {
  localStorage.setItem(storageKey, JSON.stringify(careers));
}

export function rememberCareer(career: KnownCareer): void {
  save([career, ...knownCareers().filter((known) => known.careerId !== career.careerId)]);
}

export function knownCareer(careerId: string): KnownCareer | null {
  return knownCareers().find((known) => known.careerId === careerId) ?? null;
}

/** Stores a newly issued key next to the career's secret; throws when storage is unavailable. */
export function rememberRecoveryKey(careerId: string, recoveryKey: string): void {
  save(
    knownCareers().map((known) =>
      known.careerId === careerId ? { ...known, recoveryKey } : known,
    ),
  );
}

export function forgetCareer(careerId: string): void {
  save(knownCareers().filter((known) => known.careerId !== careerId));
}

export function careerSecret(careerId: string): string | null {
  return knownCareer(careerId)?.secret ?? null;
}

/** Request options for one career's calls; the generated client merges them into its request. */
export function careerAuth(secret: string): AxiosRequestConfig {
  return { headers: { Authorization: `Bearer ${secret}` } };
}
