import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  exportContentPublication,
  revertContentPublication,
  useListContentPublications,
} from '@/api/generated/game';
import type { ContentPublicationView, OwnerSessionView } from '@/api/generated/game.schemas';
import { isApiError } from '@/api/http';
import { Button } from '@/components/ui/Button';
import { date } from '@/format';

import { csrf } from '../csrf';

/** Every publication, newest first, with revert (a new publication of that snapshot) and export (spec M4-E §6). */
export function PublicationHistory({
  session,
  currentNumber,
  onChange,
}: {
  readonly session: OwnerSessionView;
  readonly currentNumber: number;
  readonly onChange: () => Promise<unknown>;
}) {
  const { t } = useTranslation();
  const publications = useListContentPublications();
  const title = t('admin.content.history.title');
  return (
    <section className="grid gap-2" aria-label={title}>
      <h3 className="font-bold m-0">{title}</h3>
      {publications.data === undefined ? (
        <output className="block">{t('admin.loading')}</output>
      ) : (
        <ol className="grid gap-2">
          {publications.data.map((p) => (
            <Publication
              key={p.number}
              publication={p}
              current={p.number === currentNumber}
              session={session}
              onChange={onChange}
            />
          ))}
        </ol>
      )}
    </section>
  );
}

function Publication({
  publication,
  current,
  session,
  onChange,
}: {
  readonly publication: ContentPublicationView;
  readonly current: boolean;
  readonly session: OwnerSessionView;
  readonly onChange: () => Promise<unknown>;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const reasonId = `revert-reason-${String(publication.number)}`;

  const revert = async () => {
    if (reason.trim() === '') {
      setProblem('admin.moderation.reasonRequired');
      return;
    }

    setPending(true);
    setProblem(null);
    try {
      await revertContentPublication(publication.number, { reason: reason.trim() }, csrf(session));
      setOpen(false);
      setReason('');
      await onChange();
    } catch (error) {
      setProblem(
        isApiError(error) && error.code === 'content-unchanged'
          ? 'admin.content.history.same'
          : 'admin.content.publishFailed',
      );
    } finally {
      setPending(false);
    }
  };

  const download = async () => {
    const file = await exportContentPublication(publication.number);
    const url = URL.createObjectURL(
      new Blob([JSON.stringify(file, null, 2)], { type: 'application/json' }),
    );
    const link = document.createElement('a');
    link.href = url;
    link.download = `tresc-${String(publication.number)}-${publication.digest.slice(0, 12)}.json`;
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <li className="grid gap-1 border-b pb-2">
      <p className="m-0">
        <span className="font-bold">
          #{publication.number} {t(`admin.content.kind.${publication.kind}`)}
        </span>{' '}
        {t('admin.content.history.line', {
          actor: publication.actor,
          date: date(publication.publishedAt),
          reason: publication.reason,
          digest: publication.digest.slice(0, 12),
        })}
        {current && <> · {t('admin.content.history.current')}</>}
      </p>
      <div className="flex flex-wrap gap-2">
        {!current && !open && (
          <Button
            onClick={() => {
              setOpen(true);
            }}
          >
            {t('admin.content.history.revert', { number: publication.number })}
          </Button>
        )}
        <Button onClick={() => void download()}>
          {t('admin.content.history.export', { number: publication.number })}
        </Button>
      </div>
      {open && (
        <form
          noValidate
          className="grid gap-1 max-w-md"
          onSubmit={(event) => {
            event.preventDefault();
            void revert();
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
              {t('admin.content.history.confirmRevert', { number: publication.number })}
            </Button>
            <Button
              onClick={() => {
                setOpen(false);
              }}
            >
              {t('saves.cancel')}
            </Button>
          </div>
        </form>
      )}
      {problem !== null && (
        <p role="alert" className="bad m-0">
          {t(problem)}
        </p>
      )}
    </li>
  );
}
