import { create } from 'zustand';

import { streamerFriendlyList } from './streamerFriendlyList';

/**
 * Presentation-only text filters (content and experience §2): Streamer Friendly masking and the Sensitive content
 * wording filter. Both default to off, live in this browser only and never reach the server, so events, rolls, score
 * and ranking eligibility are the same with them on or off.
 */
export interface TextFilters {
  readonly streamerFriendly: boolean;
  readonly sensitive: boolean;
}

const storageKey = 'pss.textFilters.v1';
const off: TextFilters = { streamerFriendly: false, sensitive: false };

/** The stored filters; a missing, unreadable or malformed value means both off. */
export function storedFilters(): TextFilters {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(storageKey) ?? 'null');
    if (typeof value !== 'object' || value === null) {
      return off;
    }

    const record = value as Record<string, unknown>;
    return {
      streamerFriendly: record.streamerFriendly === true,
      sensitive: record.sensitive === true,
    };
  } catch {
    return off;
  }
}

function saveFilters(filters: TextFilters): void {
  try {
    localStorage.setItem(storageKey, JSON.stringify(filters));
  } catch {
    // The choice still applies for this page when the browser blocks storage.
  }
}

interface TextFiltersState extends TextFilters {
  readonly setFilter: (name: keyof TextFilters, on: boolean) => void;
}

export const useTextFilters = create<TextFiltersState>()((set, get) => ({
  ...storedFilters(),
  setFilter: (name, on) => {
    const { streamerFriendly, sensitive } = get();
    const next = { streamerFriendly, sensitive, [name]: on };
    saveFilters(next);
    set(next);
  },
}));

const diacritics: Readonly<Record<string, string>> = {
  a: '[aą]',
  c: '[cć]',
  e: '[eę]',
  l: '[lł]',
  n: '[nń]',
  o: '[oó]',
  s: '[sś]',
  z: '[zźż]',
};

const wordChar = String.raw`[\p{L}\p{N}]`;

function entryPattern(entry: string): string {
  const prefix = entry.endsWith('*');
  const words = (prefix ? entry.slice(0, -1) : entry)
    .split(' ')
    .map((word) =>
      word.replaceAll(
        /./gu,
        (char) => diacritics[char] ?? char.replaceAll(/[.*+?^${}()|[\]\\]/gu, String.raw`\$&`),
      ),
    );
  return words.join(String.raw`\s+`) + (prefix ? `${wordChar}*` : '');
}

/** One pattern for the whole list: whole words only, any letter case, with or without Polish diacritics. */
function listPattern(phrases: readonly string[]): RegExp {
  const alternatives = phrases.map((phrase) => entryPattern(phrase)).join('|');
  return new RegExp(`(?<!${wordChar})(?:${alternatives})(?!${wordChar})`, 'giu');
}

/** A masker for a phrase list: each matched word keeps its first letter and the rest of its letters become `*`. */
export function masker(phrases: readonly string[]): (text: string) => string {
  const pattern = listPattern(phrases);
  return (text) =>
    text.replaceAll(pattern, (match) =>
      match.replaceAll(
        /(\p{L})(\p{L}*)/gu,
        (_, first: string, rest: string) => first + '*'.repeat(rest.length),
      ),
    );
}

/** Masks every word or phrase of the maintained Streamer Friendly list. */
export const maskStreamerFriendly = masker(streamerFriendlyList.phrases);

/** The text as the player sees it: masked when Streamer Friendly is on. Read at render time, after localization. */
export function masked(text: string): string {
  return useTextFilters.getState().streamerFriendly ? maskStreamerFriendly(text) : text;
}

const sensitiveTags = ['adult-business', 'privacy-incident'] as const;

export type SensitiveTag = (typeof sensitiveTags)[number];

/** The tag whose neutral wording replaces an event's descriptions, or null when the filter is off or none applies. */
export function sensitiveTag(
  tags: readonly string[] | null | undefined,
  filterOn: boolean,
): SensitiveTag | null {
  if (!filterOn || tags == null) {
    return null;
  }

  return sensitiveTags.find((tag) => tags.includes(tag)) ?? null;
}

/** For components: the current Sensitive content setting, re-rendering when it changes. */
export function useSensitiveFilter(): boolean {
  return useTextFilters((state) => state.sensitive);
}
