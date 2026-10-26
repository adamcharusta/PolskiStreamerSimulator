import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import type { CareerView, EndingSummaryView, ScoreView } from '@/api/generated/game.schemas';
import { StepHeading } from '@/components/ui/StepHeading';
import { formatContent } from '@/content/icu';
import { count, multiplier, percent } from '@/format';

/** The ending: the narrative from the summary, the score breakdown with both multipliers, and the achievements. */
export function EndedView({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  return (
    <div className="grid gap-3">
      <StepHeading>{t('career.endedTitle')}</StepHeading>
      {career.ending !== null && <p>{t(`career.ending.${career.ending}`)}</p>}
      {career.summary !== null && <Narrative career={career} summary={career.summary} />}
      {career.score !== null && <ScoreTable score={career.score} />}
      <Achievements career={career} />
      <p>
        <Link className="btn btn-secondary" to="/">
          {t('career.backHome')}
        </Link>
      </p>
    </div>
  );
}

function Narrative({
  career,
  summary,
}: {
  readonly career: CareerView;
  readonly summary: EndingSummaryView;
}) {
  const { t } = useTranslation();
  const earned = career.achievements.filter((a) => a.earned);
  return (
    <section className="grid gap-1" aria-label={t('ending.narrativeTitle')}>
      <h3 className="font-bold">{t('ending.narrativeTitle')}</h3>
      <p>
        {t('ending.narrative', {
          weeks: summary.weeksPlayed,
          category: t(`category.${summary.dominantCategory}`),
          platform: t(`platform.${summary.platform}`),
          reputation: t(`ending.reputation.${summary.reputation}`),
        })}
      </p>
      {summary.bannedPlatforms.length > 0 && (
        <p>
          {t('ending.banned', {
            platforms: summary.bannedPlatforms.map((p) => t(`platform.${p}`)).join(', '),
          })}
        </p>
      )}
      {earned.length > 0 && (
        <p>
          {t('ending.highlights', {
            list: earned.map((a) => formatContent(a.name ?? a.id, career.pronouns)).join(', '),
          })}
        </p>
      )}
    </section>
  );
}

function ScoreTable({ score }: { readonly score: ScoreView }) {
  const { t } = useTranslation();
  const rows: [string, string, string][] = [
    [t('ending.score.followers'), percent(score.followers), count(score.followersPoints)],
    [t('ending.score.wealth'), percent(score.wealth), count(score.wealthPoints)],
    [t('ending.score.achievements'), percent(score.achievements), count(score.achievementPoints)],
  ];
  return (
    <table className="w-full" aria-label={t('ending.score.title')}>
      <caption className="panel-title text-left">{t('ending.score.title')}</caption>
      <thead>
        <tr>
          <th scope="col">{t('ending.score.component')}</th>
          <th scope="col">{t('ending.score.share')}</th>
          <th scope="col">{t('ending.score.points')}</th>
        </tr>
      </thead>
      <tbody>
        {rows.map(([name, share, points]) => (
          <tr key={name}>
            <th scope="row">{name}</th>
            <td>{share}</td>
            <td>{points}</td>
          </tr>
        ))}
        <tr>
          <th scope="row">{t('ending.score.base')}</th>
          <td />
          <td>{count(score.baseScore)}</td>
        </tr>
        <tr>
          <th scope="row">{t('ending.score.difficulty')}</th>
          <td />
          <td>×{multiplier(score.difficultyMultiplier)}</td>
        </tr>
        <tr>
          <th scope="row">{t('ending.score.background')}</th>
          <td />
          <td>×{multiplier(score.backgroundMultiplier)}</td>
        </tr>
        <tr>
          <th scope="row">{t('ending.score.final')}</th>
          <td />
          <td className="font-bold">{count(score.finalScore)}</td>
        </tr>
      </tbody>
    </table>
  );
}

function Achievements({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  if (career.achievements.length === 0) {
    return null;
  }

  const title = t('ending.achievements');
  return (
    <section aria-label={title}>
      <h3 className="font-bold">{title}</h3>
      <ul>
        {career.achievements.map((a) => (
          <li key={a.id} className={a.earned ? 'good' : 'muted'}>
            {a.earned ? '✓ ' : ''}
            {formatContent(
              a.name ?? t(`ending.m1.${a.id}`, { defaultValue: a.id }),
              career.pronouns,
            )}
            {a.description !== null && ` — ${formatContent(a.description, career.pronouns)}`} (
            {t('ending.points', { count: a.points })})
          </li>
        ))}
      </ul>
    </section>
  );
}
