import { describe, expect, it } from 'vitest';

import { PronounSet } from '@/api/generated/game.schemas';

import { formatContent } from './icu';

interface Choice {
  label: string;
  successText?: string;
  failureText?: string;
}
interface ContentEvent {
  id: string;
  title: string;
  text: string;
  choices: Choice[];
}

const files = import.meta.glob<string>('../../../../content/m3/{events,balance}.json', {
  query: '?raw',
  import: 'default',
  eager: true,
});
const raw = (name: string): string => {
  const entry = Object.entries(files).find(([path]) => path.endsWith(`/${name}`));
  if (entry === undefined) {
    throw new Error(`content/m3/${name} was not found`);
  }

  return entry[1];
};
interface Sponsor {
  id: string;
  name: string;
  pitch: string;
}
interface Balance {
  networkCollaborate: Choice;
  sponsors: { pitch: Choice };
  platformBan: { title: string; text: string; choices: Record<string, string> };
  achievements: { id: string; name: string; description: string }[];
}

const { events, sponsors } = JSON.parse(raw('events.json')) as {
  events: ContentEvent[];
  sponsors: Sponsor[];
};
const balance = JSON.parse(raw('balance.json')) as Balance;
const network = balance.networkCollaborate;
const choiceTexts = (owner: string, c: Choice): [string, string][] =>
  [c.label, c.successText, c.failureText]
    .filter((m): m is string => m !== undefined)
    .map((m) => [owner, m]);

const messages: [string, string][] = [
  ...events.flatMap((e) => [
    [`${e.id}.title`, e.title] as [string, string],
    [`${e.id}.text`, e.text] as [string, string],
    ...e.choices.flatMap((c) =>
      [c.label, c.successText, c.failureText]
        .filter((m): m is string => m !== undefined)
        .map((m) => [`${e.id}.choice`, m] as [string, string]),
    ),
  ]),
  ...choiceTexts('network', network),
  ...sponsors.flatMap((s) => [
    [`sponsor ${s.id}.name`, s.name] as [string, string],
    [`sponsor ${s.id}.pitch`, s.pitch] as [string, string],
  ]),
  ...choiceTexts('sponsors.pitch', balance.sponsors.pitch),
  ['platformBan.title', balance.platformBan.title],
  ['platformBan.text', balance.platformBan.text],
  ...balance.achievements.flatMap((a) => [
    [`achievement ${a.id}.name`, a.name] as [string, string],
    [`achievement ${a.id}.description`, a.description] as [string, string],
  ]),
  ...Object.entries(balance.platformBan.choices).map(
    ([platform, label]) => [`platformBan.${platform}`, label] as [string, string],
  ),
];

describe('content/m3 messages', () => {
  it.each(messages)('%s formats for every pronoun set', (_, message) => {
    const forms = Object.values(PronounSet).map((p) => formatContent(message, p));
    expect(forms.filter((form) => form.includes('{'))).toEqual([]);
    expect(forms.filter((form) => form.trim() === '')).toEqual([]);
    const expectedForms = message.includes('{gender, select,') ? 3 : 1;
    expect(new Set(forms).size).toBeGreaterThanOrEqual(expectedForms);
  });
});
