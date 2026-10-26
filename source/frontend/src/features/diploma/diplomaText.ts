import latinRegular from '@fontsource/comic-relief/files/comic-relief-latin-400-normal.woff?url';
import latinBold from '@fontsource/comic-relief/files/comic-relief-latin-700-normal.woff?url';
import extRegular from '@fontsource/comic-relief/files/comic-relief-latin-ext-400-normal.woff?url';
import extBold from '@fontsource/comic-relief/files/comic-relief-latin-ext-700-normal.woff?url';

import type { CareerView, ScoreView } from '@/api/generated/game.schemas';
import { formatContent } from '@/content/icu';
import { count, percent, pln } from '@/format';

import type { DiplomaFonts, DiplomaText } from './diploma';

async function bytes(url: string): Promise<Uint8Array> {
  const response = await fetch(url);
  return new Uint8Array(await response.arrayBuffer());
}

/** The bundled Comic Relief font files: the basic Latin and the Latin Extended subsets of both weights. */
export async function loadFonts(): Promise<DiplomaFonts> {
  const [latin, latinExt, latinB, latinExtB] = await Promise.all([
    bytes(latinRegular),
    bytes(extRegular),
    bytes(latinBold),
    bytes(extBold),
  ]);
  return { regular: { latin, latinExt }, bold: { latin: latinB, latinExt: latinExtB } };
}

/** The diploma text of an ended career, translated and formatted for its pronouns. */
export function diplomaText(
  career: CareerView,
  t: (key: string, options?: Record<string, unknown>) => string,
  today: Date,
  score: ScoreView,
): DiplomaText {
  const earned = career.achievements
    .filter((a) => a.earned)
    .map((a) => formatContent(a.name ?? a.id, career.pronouns));
  return {
    title: t('diploma.title'),
    nickname: career.nickname,
    subtitle: t('diploma.subtitle', {
      background: t(`background.${career.background}`),
      difficulty: t(`difficulty.${career.difficulty}`),
    }),
    lines: [
      t('diploma.weeks', { weeks: career.lastSettledWeek }),
      career.ending === null ? '' : t(`career.ending.${career.ending}`),
      t('diploma.final', {
        followers: count(career.stats.followers),
        cash: pln(career.stats.cashPln),
      }),
    ].filter((line) => line !== ''),
    score: t('diploma.score', {
      score: count(score.finalScore),
      followers: percent(score.followers),
      wealth: percent(score.wealth),
      achievements: percent(score.achievements),
    }),
    achievements:
      earned.length === 0
        ? t('diploma.noAchievements')
        : t('diploma.achievements', { list: earned.join(', ') }),
    footer: t('diploma.footer', { date: today.toLocaleDateString('pl-PL') }),
  };
}
