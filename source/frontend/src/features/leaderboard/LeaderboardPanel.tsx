import { useQueryClient } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { track } from '@/analytics/analytics';
import {
  getGetLeaderboardEntryQueryKey,
  useGetLeaderboardEntry,
  usePublishResult,
} from '@/api/generated/game';
import type { CareerView, EntryStatusView } from '@/api/generated/game.schemas';
import { careerAuth } from '@/careers/knownCareers';
import { Button } from '@/components/ui/Button';
import { count, date } from '@/format';

import { leaderboardNickname } from './nickname';

/** The ending's ranking panel: publish the result under a nickname, or show the entry or why there is none. */
export function LeaderboardPanel({
  career,
  secret,
}: {
  readonly career: CareerView;
  readonly secret: string;
}) {
  const { t } = useTranslation();
  const status = useGetLeaderboardEntry(career.careerId, {
    request: careerAuth(secret),
  });
  const title = t('leaderboard.panel.title');
  let body = <output className="block">{t('leaderboard.panel.loading')}</output>;
  if (status.data) {
    body = <PanelBody career={career} secret={secret} status={status.data} />;
  } else if (status.isError) {
    body = <p role="alert">{t('leaderboard.panel.failed')}</p>;
  }
  return (
    <section className="panel grid gap-2" aria-label={title}>
      <h3 className="panel-title">{title}</h3>
      {body}
    </section>
  );
}

function PanelBody({
  career,
  secret,
  status,
}: {
  readonly career: CareerView;
  readonly secret: string;
  readonly status: EntryStatusView;
}) {
  const { t } = useTranslation();
  const season = status.season ?? 0;
  switch (status.status) {
    case 'published': {
      const entry = status.entry;
      return (
        <>
          {entry != null && (
            <p className="font-bold">
              {entry.rank == null
                ? t('leaderboard.panel.publishedHidden', {
                    nickname: entry.nickname,
                    score: count(entry.score),
                  })
                : t('leaderboard.panel.published', {
                    nickname: entry.nickname,
                    score: count(entry.score),
                    rank: entry.rank,
                    season,
                  })}
            </p>
          )}
          <SeasonLink season={season} />
        </>
      );
    }
    case 'open': {
      return <PublishForm career={career} secret={secret} status={status} />;
    }
    case 'closed': {
      return (
        <>
          <p>
            {t('leaderboard.panel.closed', {
              season,
              date: status.submissionDeadline == null ? '' : date(status.submissionDeadline),
            })}
          </p>
          <SeasonLink season={season} />
        </>
      );
    }
    case 'unranked': {
      return <p>{t('leaderboard.panel.unranked')}</p>;
    }
    default: {
      // Not ended: the ending screen shows this panel only once the career has ended.
      return null;
    }
  }
}

function PublishForm({
  career,
  secret,
  status,
}: {
  readonly career: CareerView;
  readonly secret: string;
  readonly status: EntryStatusView;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [nickname, setNickname] = useState('');
  const [invalid, setInvalid] = useState(false);
  // Publication is idempotent by career, so the default network retries are safe.
  const publish = usePublishResult({
    request: careerAuth(secret),
    mutation: {
      onSuccess: (published) => {
        track('result_published');
        queryClient.setQueryData(getGetLeaderboardEntryQueryKey(career.careerId), published);
      },
    },
  });
  return (
    <form
      noValidate
      className="grid gap-2"
      onSubmit={(event) => {
        event.preventDefault();
        const valid = leaderboardNickname(nickname);
        setInvalid(valid === null);
        if (valid !== null && !publish.isPending) {
          publish.mutate({ id: career.careerId, data: { nickname: valid } });
        }
      }}
    >
      <p>
        {t('leaderboard.panel.intro', {
          score: count(career.score?.finalScore ?? 0),
          season: status.season,
        })}
      </p>
      {status.submissionDeadline != null && (
        <p className="muted">
          {t('leaderboard.panel.deadline', { date: date(status.submissionDeadline) })}
        </p>
      )}
      <label htmlFor="leaderboard-nickname" className="font-bold">
        {t('leaderboard.panel.nickname')}
      </label>
      <input
        id="leaderboard-nickname"
        className="field uppercase"
        autoComplete="off"
        maxLength={5}
        aria-describedby="leaderboard-nickname-hint"
        aria-invalid={invalid ? true : undefined}
        value={nickname}
        onChange={(event) => {
          setNickname(event.target.value);
        }}
      />
      <p id="leaderboard-nickname-hint" className="muted">
        {invalid ? (
          <span className="bad">{t('leaderboard.panel.invalid')}</span>
        ) : (
          t('leaderboard.panel.nicknameHint')
        )}
      </p>
      {publish.isError && (
        <p role="alert" className="bad">
          {t('leaderboard.panel.publishFailed')}
        </p>
      )}
      <p>
        <Button type="submit" variant="primary" pending={publish.isPending}>
          {t('leaderboard.panel.submit')}
        </Button>
      </p>
    </form>
  );
}

function SeasonLink({ season }: { readonly season: number }) {
  const { t } = useTranslation();
  return (
    <p>
      <Link className="btn btn-secondary" to="/ranking" search={{ sezon: season }}>
        {t('leaderboard.panel.show')}
      </Link>
    </p>
  );
}
