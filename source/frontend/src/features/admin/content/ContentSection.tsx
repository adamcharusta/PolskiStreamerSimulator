import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  createContentDraft,
  discardContentDraft,
  getGetContentAdminQueryKey,
  getListContentPublicationsQueryKey,
  publishContent,
  useGetContentAdmin,
} from '@/api/generated/game';
import type {
  ContentAdminView,
  ContentDraftView,
  OwnerSessionView,
} from '@/api/generated/game.schemas';
import { isApiError } from '@/api/http';
import { Button } from '@/components/ui/Button';
import { date } from '@/format';

import { csrf } from '../csrf';
import { BalanceEditor } from './BalanceEditor';
import { EventEditor } from './EventEditor';
import { PublicationHistory } from './PublicationHistory';
import { SimulationPanel } from './SimulationPanel';

/** The owner's content draft, its check and publication, and the publication history (spec M4-E §8). */
export function ContentSection({ session }: { readonly session: OwnerSessionView }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const content = useGetContentAdmin();
  const [problem, setProblem] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  // Kept here: a publication deletes the draft, and its panel with it.
  const [notice, setNotice] = useState<string | null>(null);
  const title = t('admin.content.title');

  /** Shows the draft a write returned at once (null after a discard); without one, reloads the content view. */
  const refresh = (draft?: ContentDraftView | null) => {
    void queryClient.invalidateQueries({ queryKey: getListContentPublicationsQueryKey() });
    if (draft === undefined) {
      return queryClient.invalidateQueries({ queryKey: getGetContentAdminQueryKey() });
    }

    queryClient.setQueryData<ContentAdminView>(getGetContentAdminQueryKey(), (view) =>
      view === undefined ? view : { ...view, draft },
    );
    return Promise.resolve();
  };

  const create = async () => {
    setPending(true);
    setProblem(null);
    try {
      await refresh(await createContentDraft({}, csrf(session)));
    } catch {
      setProblem('admin.content.failed');
    } finally {
      setPending(false);
    }
  };

  let body = <output className="block">{t('admin.loading')}</output>;
  if (content.isError) {
    body = <p role="alert">{t('admin.failed')}</p>;
  } else if (content.data !== undefined) {
    const { current, draft } = content.data;
    body = (
      <>
        <p className="m-0">
          {t('admin.content.current', {
            number: current.number,
            kind: t(`admin.content.kind.${current.kind}`),
            date: date(current.publishedAt),
            digest: current.digest.slice(0, 12),
          })}
        </p>
        {draft === null ? (
          <p className="m-0">
            <Button variant="primary" pending={pending} onClick={() => void create()}>
              {t('admin.content.create')}
            </Button>
          </p>
        ) : (
          <Draft draft={draft} session={session} onChange={refresh} onNotice={setNotice} />
        )}
        {notice !== null && <output className="block">{notice}</output>}
        {problem !== null && (
          <p role="alert" className="bad m-0">
            {t(problem)}
          </p>
        )}
        <PublicationHistory session={session} currentNumber={current.number} onChange={refresh} />
      </>
    );
  }

  return (
    <section className="panel grid gap-3" aria-label={title}>
      <h2 className="panel-title">{title}</h2>
      {body}
    </section>
  );
}

function Draft({
  draft,
  session,
  onChange,
  onNotice,
}: {
  readonly draft: ContentDraftView;
  readonly session: OwnerSessionView;
  readonly onChange: (draft?: ContentDraftView | null) => Promise<unknown>;
  readonly onNotice: (notice: string | null) => void;
}) {
  const { t } = useTranslation();
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const { diff } = draft;
  const blocked = draft.problems.length > 0 || diff.unchanged;

  /** Runs a write; it returns the draft to show, null when the draft is gone, or undefined to reload everything. */
  const run = async (
    action: () => Promise<ContentDraftView | null | undefined>,
    failure: string,
  ) => {
    setPending(true);
    setProblem(null);
    onNotice(null);
    try {
      await onChange(await action());
    } catch (error) {
      setProblem(
        isApiError(error) && error.code === 'draft-stale' ? 'admin.content.stale' : failure,
      );
    } finally {
      setPending(false);
    }
  };

  const publish = () =>
    run(
      async () => {
        if (reason.trim() === '') {
          throw new Error('reason');
        }

        const published = await publishContent(
          { revision: draft.revision, reason: reason.trim() },
          csrf(session),
        );
        setReason('');
        onNotice(t('admin.content.published', { number: published.number }));
        return;
      },
      reason.trim() === '' ? 'admin.moderation.reasonRequired' : 'admin.content.publishFailed',
    );

  return (
    <div className="grid gap-3">
      <div className="grid gap-1">
        <p className="m-0">
          {t('admin.content.draft', {
            base: draft.baseNumber,
            revision: draft.revision,
            by: draft.updatedBy,
            date: date(draft.updatedAt),
          })}
        </p>
        {draft.behindCurrent && (
          <p role="alert" className="bad m-0">
            {t('admin.content.behind')}
          </p>
        )}
        <p className="m-0">
          <Button
            pending={pending}
            onClick={() =>
              void run(async () => {
                await discardContentDraft({ revision: draft.revision }, csrf(session));
                return null;
              }, 'admin.content.failed')
            }
          >
            {t('admin.content.discard')}
          </Button>
        </p>
      </div>

      <section className="grid gap-1" aria-label={t('admin.content.check')}>
        <h3 className="font-bold m-0">{t('admin.content.check')}</h3>
        {draft.problems.length === 0 ? (
          <p className="m-0">{t('admin.content.noProblems')}</p>
        ) : (
          <ul className="bad m-0" aria-label={t('admin.content.problems')}>
            {draft.problems.map((item) => (
              <li key={item} className="font-mono text-sm">
                {item}
              </li>
            ))}
          </ul>
        )}
        <p className="m-0">
          {diff.unchanged
            ? t('admin.content.unchanged')
            : t(diff.mechanical ? 'admin.content.newSeason' : 'admin.content.sameSeason')}
        </p>
        {(diff.items.length > 0 || diff.values.length > 0) && (
          <ul className="m-0" aria-label={t('admin.content.diff')}>
            {diff.items.map((item) => (
              <li key={`${item.section}:${item.id}`}>
                {t(`admin.content.change.${item.change}`)} {item.id} —{' '}
                {t(item.mechanical ? 'admin.content.mechanical' : 'admin.content.textOnly')}
              </li>
            ))}
            {diff.values.map((value) => (
              <li key={value.path} className="font-mono text-sm">
                {value.path}: {value.before ?? '∅'} → {value.after ?? '∅'}
              </li>
            ))}
          </ul>
        )}
      </section>

      <EventEditor draft={draft} session={session} onChange={onChange} />
      <BalanceEditor draft={draft} session={session} onChange={onChange} />
      <SimulationPanel disabled={draft.problems.length > 0} session={session} />

      <form
        noValidate
        className="grid gap-1 max-w-md"
        aria-label={t('admin.content.publishTitle')}
        onSubmit={(event) => {
          event.preventDefault();
          void publish();
        }}
      >
        <h3 className="font-bold m-0">{t('admin.content.publishTitle')}</h3>
        <label htmlFor="publish-reason" className="font-bold">
          {t('admin.moderation.reason')}
        </label>
        <input
          id="publish-reason"
          className="field"
          maxLength={500}
          value={reason}
          onChange={(event) => {
            setReason(event.target.value);
          }}
        />
        <Button type="submit" variant="primary" pending={pending} disabled={blocked}>
          {t('admin.content.publish')}
        </Button>
      </form>
      {problem !== null && (
        <p role="alert" className="bad m-0">
          {t(problem)}
        </p>
      )}
    </div>
  );
}
