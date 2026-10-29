import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  extendSeason,
  getListAuditQueryKey,
  getListSeasonsQueryKey,
  useListSeasons,
} from '@/api/generated/game';
import type { OwnerSessionView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { date } from '@/format';

import { csrf } from './csrf';

/** Moves a closed season's submission deadline later for everyone, with a reason (spec M4-B §4, M4-D §5). */
export function ExtensionSection({ session }: { readonly session: OwnerSessionView }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const seasons = useListSeasons();
  const closed = (seasons.data ?? []).filter((season) => season.closedAt !== null);
  const [chosen, setChosen] = useState<number | null>(null);
  const [until, setUntil] = useState('');
  const [reason, setReason] = useState('');
  const [notice, setNotice] = useState<string | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const title = t('admin.extension.title');
  const season = closed.find((s) => s.number === chosen) ?? closed[0];

  const submit = async () => {
    setNotice(null);
    if (season === undefined || until === '' || reason.trim() === '') {
      setProblem('admin.extension.incomplete');
      return;
    }

    setPending(true);
    setProblem(null);
    try {
      const extended = await extendSeason(
        season.number,
        { until: new Date(until).toISOString(), reason: reason.trim() },
        csrf(session),
      );
      setNotice(
        t('admin.extension.done', {
          season: extended.number,
          date: date(extended.submissionDeadline ?? until),
        }),
      );
      setReason('');
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: getListSeasonsQueryKey() }),
        queryClient.invalidateQueries({ queryKey: getListAuditQueryKey().slice(0, 1) }),
      ]);
    } catch {
      setProblem('admin.extension.failed');
    } finally {
      setPending(false);
    }
  };

  return (
    <section className="panel grid gap-2" aria-label={title}>
      <h2 className="panel-title">{title}</h2>
      {season === undefined ? (
        <p className="m-0">{t('admin.extension.none')}</p>
      ) : (
        <form
          noValidate
          className="grid gap-1 max-w-md"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          <label htmlFor="extension-season" className="font-bold">
            {t('leaderboard.season')}
          </label>
          <select
            id="extension-season"
            className="field"
            value={season.number}
            onChange={(event) => {
              setChosen(Number(event.target.value));
            }}
          >
            {closed.map((option) => (
              <option key={option.number} value={option.number}>
                {t('admin.extension.option', {
                  season: option.number,
                  date: date(option.submissionDeadline ?? option.closedAt ?? ''),
                })}
              </option>
            ))}
          </select>
          <label htmlFor="extension-until" className="font-bold">
            {t('admin.extension.until')}
          </label>
          <input
            id="extension-until"
            type="datetime-local"
            className="field"
            value={until}
            onChange={(event) => {
              setUntil(event.target.value);
            }}
          />
          <label htmlFor="extension-reason" className="font-bold">
            {t('admin.moderation.reason')}
          </label>
          <input
            id="extension-reason"
            className="field"
            maxLength={500}
            value={reason}
            onChange={(event) => {
              setReason(event.target.value);
            }}
          />
          <Button type="submit" variant="primary" pending={pending}>
            {t('admin.extension.submit')}
          </Button>
        </form>
      )}
      {notice !== null && <output className="block">{notice}</output>}
      {problem !== null && (
        <p role="alert" className="bad m-0">
          {t(problem)}
        </p>
      )}
    </section>
  );
}
