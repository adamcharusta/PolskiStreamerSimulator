import type { CreateCareerRequest } from '@/api/generated/game.schemas';

/**
 * A creation sent (or about to be sent) whose answer this browser has not stored yet. It is written before the
 * request, so a closed tab or a lost answer can repeat the same creation ID later (TD-121).
 */
export interface PendingCreation {
  readonly creationId: string;
  readonly request: Omit<CreateCareerRequest, 'creationId'>;
  readonly startedAt: string;
}

const storageKey = 'pss.pendingCreation.v1';

/** The server answers a repeat for 24 hours; an older pending creation cannot be finished. */
export const pendingCreationLifetimeMs = 24 * 60 * 60 * 1000;

function isPending(value: unknown): value is PendingCreation {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const p = value as Record<string, unknown>;
  const request = p.request as Record<string, unknown> | null | undefined;
  return (
    typeof p.creationId === 'string' &&
    typeof p.startedAt === 'string' &&
    Number.isFinite(Date.parse(p.startedAt)) &&
    typeof request === 'object' &&
    request !== null &&
    ['nickname', 'background', 'difficulty', 'pronouns'].every(
      (field) => typeof request[field] === 'string',
    )
  );
}

/** Starts a creation; `null` when this browser cannot store it (and so could not store the career either). */
export function beginCreation(
  request: Omit<CreateCareerRequest, 'creationId'>,
): PendingCreation | null {
  const pending: PendingCreation = {
    creationId: crypto.randomUUID(),
    request,
    startedAt: new Date().toISOString(),
  };
  try {
    localStorage.setItem(storageKey, JSON.stringify(pending));
    return pending;
  } catch {
    return null;
  }
}

/** The pending creation that can still be finished; an expired or malformed one is dropped. */
export function pendingCreation(now: number = Date.now()): PendingCreation | null {
  try {
    const parsed: unknown = JSON.parse(localStorage.getItem(storageKey) ?? 'null');
    if (!isPending(parsed)) {
      return null;
    }

    if (now - Date.parse(parsed.startedAt) >= pendingCreationLifetimeMs) {
      clearPendingCreation();
      return null;
    }

    return parsed;
  } catch {
    return null;
  }
}

export function clearPendingCreation(): void {
  try {
    localStorage.removeItem(storageKey);
  } catch {
    // Nothing to clear when storage is unavailable.
  }
}
