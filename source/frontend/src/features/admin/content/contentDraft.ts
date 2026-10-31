import { PronounSet } from '@/api/generated/game.schemas';
import { formatContent } from '@/content/icu';

/** A numeric value of the balance document by its JSON path (`a.b[2]`). */
export interface Constant {
  readonly path: string;
  readonly value: number;
}

type Json = null | boolean | number | string | Json[] | { [key: string]: Json };

/** Every number of the balance document, in document order. */
export function constants(balance: unknown): Constant[] {
  const found: Constant[] = [];
  const walk = (node: unknown, path: string) => {
    if (typeof node === 'number') {
      found.push({ path, value: node });
    } else if (Array.isArray(node)) {
      for (const [index, item] of node.entries()) {
        walk(item, `${path}[${String(index)}]`);
      }
    } else if (node !== null && typeof node === 'object') {
      for (const [key, value] of Object.entries(node)) {
        walk(value, path === '' ? key : `${path}.${key}`);
      }
    }
  };
  walk(balance, '');
  return found;
}

function segments(path: string): (string | number)[] {
  return path.split('.').flatMap((part) =>
    part
      .split(/[[\]]/u)
      .filter((piece) => piece !== '')
      .map((piece, index) => (index > 0 ? Number(piece) : piece)),
  );
}

/** A copy of the balance document with the numbers of <paramref>changes</paramref> set at their paths. */
export function withConstants(balance: unknown, changes: ReadonlyMap<string, number>): unknown {
  const copy = structuredClone(balance) as Json;
  for (const [path, value] of changes) {
    const parts = segments(path);
    let node: Json = copy;
    for (const part of parts.slice(0, -1)) {
      node = (node as Record<string | number, Json>)[part] ?? null;
    }

    const last = parts.at(-1);
    if (last !== undefined && node !== null && typeof node === 'object') {
      (node as Record<string | number, Json>)[last] = value;
    }
  }

  return copy;
}

/** The title, text and choice labels of an event in one pronoun form; missing texts are left out. */
export function preview(event: unknown, pronouns: PronounSet): string[] {
  if (event === null || typeof event !== 'object') {
    return [];
  }

  const e = event as Record<string, unknown>;
  const texts = [e.title, e.text, e.textPl];
  const choices = Array.isArray(e.choices) ? (e.choices as Record<string, unknown>[]) : [];
  for (const choice of choices) {
    texts.push(choice.label ?? choice.labelPl);
  }

  return texts
    .filter((text): text is string => typeof text === 'string')
    .map((text) => formatContent(text, pronouns));
}

export const pronounForms = [PronounSet.woman, PronounSet.man, PronounSet.nonbinary] as const;

/** A schema-2 live event to start a new one from; the server's check lists what still needs filling in. */
export function eventTemplate(id: string): string {
  return JSON.stringify(
    {
      id,
      version: 1,
      phase: 'live',
      title: 'Nowe wydarzenie',
      text: 'Opis wydarzenia.',
      weight: 1,
      cooldownWeeks: 13,
      tags: [],
      eligibility: { fromWeek: 1, toWeek: 1040, categories: ['JustChatting'] },
      choices: [
        {
          id: 'ok',
          label: 'Odpowiedz',
          baseChance: 0.5,
          modifiers: [],
          allowsCritical: false,
          successText: 'Udało się.',
          failureText: 'Nie udało się.',
          outcomes: { success: [], failure: [] },
        },
      ],
    },
    null,
    2,
  );
}
