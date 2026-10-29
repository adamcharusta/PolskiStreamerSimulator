import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  getListAuditQueryKey,
  getListModeratedEntriesQueryKey,
  hideEntry,
  restoreEntry,
  useListModeratedEntries,
  useListSeasons,
} from '@/api/generated/game';
import type { ModeratedEntryView, OwnerSessionView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { count, date } from '@/format';

import { csrf } from './csrf';

/** Every entry of a season with hide and restore; each change needs a reason (spec M4-D §5). */
export function ModerationSection({ session }: { readonly session: OwnerSessionView }) {
  const { t } = useTranslation();
  const [season, setSeason] = useState<number | undefined>();
  const seasons = useListSeasons();
  const entries = useListModeratedEntries(season === undefined ? undefined : { season });
  const title = t('admin.moderation.title');
  let body = <output className="block">{t('admin.loading')}</output>;
  if (entries.isError) {
    body = <p role="alert">{t('admin.failed')}</p>;
  } else if (entries.data?.entries.length === 0) {
    body = <p className="m-0">{t('admin.moderation.empty')}</p>;
  } else if (entries.data !== undefined) {
    body = (
      <ul className="grid gap-2" aria-label={t('admin.moderation.list')}>
        {entries.data.entries.map((entry) => (
          <EntryRow key={entry.entryId} entry={entry} session={session} />
        ))}
      </ul>
    );
  }

  return (
    <section className="panel grid gap-2" aria-label={title}>
      <h2 className="panel-title">{title}</h2>
      {seasons.data !== undefined && seasons.data.length > 0 && (
        <div>
          <label htmlFor="admin-season" className="block font-bold">
            {t('leaderboard.season')}
          </label>
          <select
            id="admin-season"
            className="field"
            value={season ?? entries.data?.season.number ?? ''}
            onChange={(event) => {
              setSeason(Number(event.target.value));
            }}
          >
            {seasons.data.map((option) => (
              <option key={option.number} value={option.number}>
                {t(option.current ? 'leaderboard.seasonCurrent' : 'leaderboard.seasonOption', {
                  season: option.number,
                })}
              </option>
            ))}
          </select>
        </div>
      )}
      {body}
    </section>
  );
}

function EntryRow({
  entry,
  session,
}: {
  readonly entry: ModeratedEntryView;
  readonly session: OwnerSessionView;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const hidden = entry.hiddenAt !== null;
  const reasonId = `reason-${entry.entryId}`;

  const submit = async () => {
    if (reason.trim() === '') {
      setProblem('admin.moderation.reasonRequired');
      return;
    }

    setPending(true);
    setProblem(null);
    try {
      const change = hidden ? restoreEntry : hideEntry;
      await change(entry.entryId, { reason: reason.trim() }, csrf(session));
      setOpen(false);
      setReason('');
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getListModeratedEntriesQueryKey().slice(0, 1) }),
        queryClient.invalidateQueries({ queryKey: getListAuditQueryKey().slice(0, 1) }),
      ]);
    } catch {
      setProblem('admin.moderation.failed');
    } finally {
      setPending(false);
    }
  };

  return (
    <li className="grid gap-1 border-b pb-2">
      <p className="m-0">
        <span className="font-bold">{entry.nickname}</span> —{' '}
        {t('admin.moderation.summary', {
          score: count(entry.score),
          weeks: entry.weeksPlayed,
          at: date(entry.submittedAt),
        })}{' '}
        {hidden
          ? t('admin.moderation.hidden', { reason: entry.hiddenReason ?? '' })
          : t('admin.moderation.rank', { rank: entry.rank ?? 0 })}
      </p>
      <p className="m-0 muted font-mono text-sm">
        {t('admin.moderation.receipt', {
          ruleset: entry.rulesetId,
          version: entry.finalStateVersion,
          hash: entry.finalStateHash.slice(0, 12),
        })}
      </p>
      {open ? (
        <form
          noValidate
          className="grid gap-1"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          <label htmlFor={reasonId} className="font-bold">
            {t('admin.moderation.reason')}
          </label>
          <input
            id={reasonId}
            className="field"
            maxLength={500}
            value={reason}
            onChange={(event) => {
              setReason(event.target.value);
            }}
          />
          <div className="flex flex-wrap gap-2">
            <Button type="submit" variant="primary" pending={pending}>
              {t(hidden ? 'admin.moderation.confirmRestore' : 'admin.moderation.confirmHide', {
                nickname: entry.nickname,
              })}
            </Button>
            <Button
              onClick={() => {
                setOpen(false);
                setProblem(null);
              }}
            >
              {t('saves.cancel')}
            </Button>
          </div>
        </form>
      ) : (
        <p className="m-0">
          <Button
            onClick={() => {
              setOpen(true);
            }}
          >
            {t(hidden ? 'admin.moderation.restore' : 'admin.moderation.hide', {
              nickname: entry.nickname,
            })}
          </Button>
        </p>
      )}
      {problem !== null && (
        <p role="alert" className="bad m-0">
          {t(problem)}
        </p>
      )}
    </li>
  );
}
