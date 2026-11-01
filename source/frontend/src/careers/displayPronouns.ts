import { create } from 'zustand';

import { PronounSet } from '@/api/generated/game.schemas';

/**
 * Pronouns the player chose in settings for a career, kept in this browser only (content and experience §1). They
 * change the grammar of the text this browser shows and nothing else: the server keeps the pronouns chosen at
 * creation, so the action log, rolls, score and ranking eligibility are the same whatever is chosen here.
 */
const storageKey = 'pss.pronouns.v1';

type Overrides = Readonly<Record<string, PronounSet>>;

function isPronounSet(value: unknown): value is PronounSet {
  return typeof value === 'string' && (Object.values(PronounSet) as string[]).includes(value);
}

function storedOverrides(): Overrides {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(storageKey) ?? '{}');
    if (typeof value !== 'object' || value === null) {
      return {};
    }

    return Object.fromEntries(
      Object.entries(value).filter(([, pronouns]) => isPronounSet(pronouns)),
    );
  } catch {
    return {};
  }
}

interface DisplayPronounsState {
  readonly overrides: Overrides;
  readonly setPronouns: (careerId: string, pronouns: PronounSet) => void;
}

export const useDisplayPronouns = create<DisplayPronounsState>()((set, get) => ({
  overrides: storedOverrides(),
  setPronouns: (careerId, pronouns) => {
    const overrides = { ...get().overrides, [careerId]: pronouns };
    try {
      localStorage.setItem(storageKey, JSON.stringify(overrides));
    } catch {
      // The choice still applies for this page when the browser blocks storage.
    }
    set({ overrides });
  },
}));

/** The pronouns chosen in settings for a career, or undefined when the player kept the ones chosen at creation. */
export function usePronounOverride(careerId: string): PronounSet | undefined {
  return useDisplayPronouns((state) => state.overrides[careerId]);
}
