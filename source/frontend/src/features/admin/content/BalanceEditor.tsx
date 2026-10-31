import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { putDraftBalance } from '@/api/generated/game';
import type { ContentDraftView, OwnerSessionView } from '@/api/generated/game.schemas';
import { isApiError } from '@/api/http';
import { Button } from '@/components/ui/Button';

import { csrf } from '../csrf';
import { constants, withConstants } from './contentDraft';

/** Every numeric balance constant by path; the server's check enforces each constant's bounds. */
export function BalanceEditor({
  draft,
  session,
  onChange,
}: {
  readonly draft: ContentDraftView;
  readonly session: OwnerSessionView;
  readonly onChange: (draft?: ContentDraftView | null) => Promise<unknown>;
}) {
  const { t } = useTranslation();
  const [filter, setFilter] = useState('');
  const [edits, setEdits] = useState<ReadonlyMap<string, string>>(new Map());
  const [problem, setProblem] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const all = constants(draft.balance);
  const shown = all.filter((c) => c.path.toLowerCase().includes(filter.trim().toLowerCase()));

  const save = async () => {
    const changes = new Map<string, number>();
    for (const [path, raw] of edits) {
      const value = Number(raw.replace(',', '.'));
      if (raw.trim() === '' || !Number.isFinite(value)) {
        setProblem('admin.content.balance.notNumber');
        return;
      }

      changes.set(path, value);
    }

    setPending(true);
    setProblem(null);
    try {
      const saved = await putDraftBalance(
        { revision: draft.revision, balance: withConstants(draft.balance, changes) as object },
        csrf(session),
      );
      setEdits(new Map());
      await onChange(saved);
    } catch (error) {
      setProblem(
        isApiError(error) && error.code === 'draft-stale'
          ? 'admin.content.stale'
          : 'admin.content.failed',
      );
    } finally {
      setPending(false);
    }
  };

  return (
    <section className="grid gap-2" aria-label={t('admin.content.balance.title')}>
      <h3 className="font-bold m-0">{t('admin.content.balance.title')}</h3>
      <div>
        <label htmlFor="balance-filter" className="block font-bold">
          {t('admin.content.balance.filter')}
        </label>
        <input
          id="balance-filter"
          className="field"
          value={filter}
          onChange={(event) => {
            setFilter(event.target.value);
          }}
        />
      </div>
      <div className="grid gap-1 max-h-64 overflow-y-auto md:grid-cols-2">
        {shown.map((c) => {
          const id = `constant-${c.path.replaceAll(/[^\w]/gu, '-')}`;
          return (
            <div key={c.path} className="flex items-center gap-2">
              <label htmlFor={id} className="font-mono text-sm grow">
                {c.path}
              </label>
              <input
                id={id}
                className="field w-28"
                inputMode="decimal"
                value={edits.get(c.path) ?? String(c.value)}
                onChange={(event) => {
                  const next = new Map(edits);
                  next.set(c.path, event.target.value);
                  setEdits(next);
                }}
              />
            </div>
          );
        })}
      </div>
      <p className="m-0">
        <Button
          variant="primary"
          pending={pending}
          disabled={edits.size === 0}
          onClick={() => void save()}
        >
          {t('admin.content.balance.save', { count: edits.size })}
        </Button>
      </p>
      {problem !== null && (
        <p role="alert" className="bad m-0">
          {t(problem)}
        </p>
      )}
    </section>
  );
}
