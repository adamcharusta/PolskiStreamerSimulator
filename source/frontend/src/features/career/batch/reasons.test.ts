import { describe, expect, it } from 'vitest';

import { pln } from '@/format';
import i18n from '@/i18n';

import { reasonText } from './reasons';

const t = i18n.t.bind(i18n);
const plan = { category: 'fps', categoryRule: 'bonus' };

describe('policy reasons in Polish', () => {
  it.each([
    [
      { code: 'rest-low-energy', args: { energy: '20.000000', threshold: '30', ...plan } },
      'Energia 20 poniżej 30 — odpoczynek. Kategoria FPS: mocna strona.',
    ],
    [
      { code: 'rest-high-burnout', args: { burnout: '75.000000', limit: '70.000000', ...plan } },
      'Wypalenie 75 powyżej 70 — odpoczynek. Kategoria FPS: mocna strona.',
    ],
    [
      {
        code: 'train-scheduled',
        args: { everyWeeks: '4', category: 'chess', categoryRule: 'rotation' },
      },
      'Co 4 tygodnie trening. Kategoria Szachy: rotacja.',
    ],
    [
      {
        code: 'network-scheduled',
        args: { everyWeeks: '3', category: 'gambling', categoryRule: 'fixed' },
      },
      'Co 3 tygodnie networking. Kategoria Hazard: stała specjalizacja.',
    ],
    [
      {
        code: 'fallback-rest',
        args: { preferred: 'improveProduction', category: 'moba', categoryRule: 'least-bored' },
      },
      '„Ulepszenie sprzętu” niedostępne — odpoczynek. Kategoria MOBA: najmniej nudna.',
    ],
    [
      { code: 'gamble-best-odds', args: { probability: '0.600000', threshold: '0.500000' } },
      'Szansa 60% (próg 50%) — ryzyk-fizyk.',
    ],
    [
      { code: 'take-guaranteed', args: { bestProbability: '0.400000' } },
      'Najlepsza szansa 40% to za mało — pewny wybór.',
    ],
    [{ code: 'take-guaranteed', args: {} }, 'Do wyboru tylko pewna opcja.'],
    [
      { code: 'only-gambles', args: { probability: '0.400000' } },
      'Same ryzykowne opcje — szansa 40%.',
    ],
    [{ code: 'brand-new-reason', args: {} }, 'Decyzja automatu.'],
  ])('explains %j', (reason, text) => {
    expect(reasonText(t, reason)).toBe(text);
  });

  it('formats money and equipment in an upgrade reason', () => {
    expect(
      reasonText(t, {
        code: 'upgrade-equipment',
        args: { slot: 'microphone', pricePln: '600', cashPln: '2000', ...plan },
      }),
    ).toBe(
      `Mikrofon za ${pln(600)} przy kasie ${pln(2000)} — ulepszenie. Kategoria FPS: mocna strona.`,
    );
  });
});
