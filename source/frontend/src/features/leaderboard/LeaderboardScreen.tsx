import { useNavigate, useSearch } from '@tanstack/react-router';
import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useGetLeaderboard, useListSeasons } from '@/api/generated/game';
import type { LeaderboardView, SeasonView } from '@/api/generated/game.schemas';
import { count, date } from '@/format';

/** `/ranking`: the season picker and the season's top 100 (content §3, TD-122). */
export function LeaderboardScreen() {
  const { t } = useTranslation();
  const { sezon } = useSearch({ from: '/ranking' });
  const navigate = useNavigate({ from: '/ranking' });
  const seasons = useListSeasons();
  const board = useGetLeaderboard(sezon === undefined ? undefined : { season: sezon });
  const shown = board.data?.season.number ?? sezon;

  let body: ReactNode = <output className="block">{t('leaderboard.loading')}</output>;
  if (board.data) {
    body = <Table board={board.data} />;
  } else if (board.isError) {
    body = (
      <p role="alert">
        {t(board.error.status === 404 ? 'leaderboard.notFound' : 'leaderboard.failed')}
      </p>
    );
  }

  return (
    <div className="grid gap-3">
      <h1 className="heading text-3xl">{t('leaderboard.title')}</h1>
      {seasons.data !== undefined && seasons.data.length > 1 && (
        <div className="panel">
          <label htmlFor="season" className="block font-bold">
            {t('leaderboard.season')}
          </label>
          <select
            id="season"
            className="field"
            value={shown ?? ''}
            onChange={(event) => {
              void navigate({ search: { sezon: Number(event.target.value) } });
            }}
          >
            {seasons.data.map((season) => (
              <option key={season.number} value={season.number}>
                {t(season.current ? 'leaderboard.seasonCurrent' : 'leaderboard.seasonOption', {
                  season: season.number,
                })}
              </option>
            ))}
          </select>
        </div>
      )}
      {body}
    </div>
  );
}

function Table({ board }: { readonly board: LeaderboardView }) {
  const { t } = useTranslation();
  const { season } = board;
  return (
    <section className="panel grid gap-2">
      <SeasonState season={season} />
      {board.entries.length === 0 ? (
        <p>{t('leaderboard.empty')}</p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full">
            <caption className="panel-title text-left">
              {t('leaderboard.caption', { season: season.number })}
            </caption>
            <thead>
              <tr>
                <th scope="col">{t('leaderboard.column.rank')}</th>
                <th scope="col">{t('leaderboard.column.nickname')}</th>
                <th scope="col">{t('leaderboard.column.score')}</th>
                <th scope="col">{t('leaderboard.column.weeks')}</th>
                <th scope="col">{t('leaderboard.column.difficulty')}</th>
                <th scope="col">{t('leaderboard.column.ending')}</th>
              </tr>
            </thead>
            <tbody>
              {board.entries.map((entry) => (
                <tr key={entry.rank}>
                  <td>{entry.rank}</td>
                  <th scope="row">{entry.nickname}</th>
                  <td>{count(entry.score)}</td>
                  <td>{entry.weeksPlayed}</td>
                  <td>{t(`difficulty.${entry.difficulty}`)}</td>
                  <td>{t(`leaderboard.ending.${entry.ending}`)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function SeasonState({ season }: { readonly season: SeasonView }) {
  const { t } = useTranslation();
  // Read once per mount: render stays pure, and a page left open past the deadline updates on the next visit.
  const [now] = useState(Date.now);
  if (season.submissionDeadline == null) {
    return <p className="muted">{t('leaderboard.open')}</p>;
  }

  const deadline = date(season.submissionDeadline);
  const accepting = Date.parse(season.submissionDeadline) >= now;
  return (
    <p className="muted">
      {t(accepting ? 'leaderboard.closed' : 'leaderboard.closedFinal', { date: deadline })}
      {season.extended && ` ${t('leaderboard.extended')}`}
    </p>
  );
}
