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
const events = (JSON.parse(raw('events.json')) as { events: ContentEvent[] }).events;
const network = (JSON.parse(raw('balance.json')) as { networkCollaborate: Choice })
  .networkCollaborate;

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
  ...[network.label, network.successText, network.failureText]
    .filter((m): m is string => m !== undefined)
    .map((m) => ['network', m] as [string, string]),
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
