import type { AxiosRequestConfig } from 'axios';

/** A career this browser can open: its secret is the only access (no recovery key until M4). */
export interface KnownCareer {
  readonly careerId: string;
  readonly nickname: string;
  readonly secret: string;
  readonly createdAt: string;
}

const storageKey = 'pss.careers.v1';

function isKnownCareer(value: unknown): value is KnownCareer {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const c = value as Record<string, unknown>;
  const hasFields = [c.careerId, c.nickname, c.secret, c.createdAt].every(
    (field) => typeof field === 'string' && field.length > 0,
  );
  return hasFields && Number.isFinite(Date.parse(c.createdAt as string));
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

export function forgetCareer(careerId: string): void {
  save(knownCareers().filter((known) => known.careerId !== careerId));
}

export function careerSecret(careerId: string): string | null {
  return knownCareers().find((known) => known.careerId === careerId)?.secret ?? null;
}

/** Request options for one career's calls; the generated client merges them into its request. */
export function careerAuth(secret: string): AxiosRequestConfig {
  return { headers: { Authorization: `Bearer ${secret}` } };
}
