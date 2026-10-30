import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { putDraftEvent, removeDraftEvent } from '@/api/generated/game';
import type { ContentDraftView, OwnerSessionView } from '@/api/generated/game.schemas';
import { isApiError } from '@/api/http';
import { Button } from '@/components/ui/Button';

import { csrf } from '../csrf';
import { eventTemplate, preview, pronounForms } from './contentDraft';

/** The draft's events: a filtered list, the selected event's JSON with a preview, save, remove and add. */
export function EventEditor({
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
  const [selected, setSelected] = useState<string | null>(null);
  const [text, setText] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const [pending, setPending] = useState(false);
  const changes = new Map(
    draft.diff.items.filter((i) => i.section === 'event').map((i) => [i.id, i]),
  );
  const shown = draft.events.filter((e) =>
    `${e.id} ${e.title ?? ''} ${e.phase ?? ''}`.toLowerCase().includes(filter.trim().toLowerCase()),
  );

  let parsed: unknown = null;
  try {
    parsed = JSON.parse(text) as unknown;
  } catch {
    parsed = null;
  }

  const open = (id: string, json: string) => {
    setSelected(id);
    setText(json);
    setProblem(null);
  };

  const save = async () => {
    if (parsed === null || typeof parsed !== 'object' || selected === null) {
      setProblem('admin.content.events.notJson');
      return;
    }

    const id = (parsed as { id?: unknown }).id;
    if (typeof id !== 'string' || id === '') {
      setProblem('admin.content.events.noId');
      return;
    }

    setPending(true);
    setProblem(null);
    try {
      const saved = await putDraftEvent(
        id,
        { revision: draft.revision, event: parsed },
        csrf(session),
      );
      setSelected(id);
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

  const remove = async () => {
    if (selected === null) {
      return;
    }

    setPending(true);
    try {
      const saved = await removeDraftEvent(selected, { revision: draft.revision }, csrf(session));
      setSelected(null);
      setText('');
      await onChange(saved);
    } catch {
      setProblem('admin.content.failed');
    } finally {
      setPending(false);
    }
  };

  return (
    <section className="grid gap-2" aria-label={t('admin.content.events.title')}>
      <h3 className="font-bold m-0">{t('admin.content.events.title')}</h3>
      <div className="flex flex-wrap items-end gap-2">
        <div>
          <label htmlFor="event-filter" className="block font-bold">
            {t('admin.content.events.filter')}
          </label>
          <input
            id="event-filter"
            className="field"
            value={filter}
            onChange={(event) => {
              setFilter(event.target.value);
            }}
          />
        </div>
        <Button
          onClick={() => {
            const id = `nowe-${String(draft.events.length + 1)}`;
            open(id, eventTemplate(id));
          }}
        >
          {t('admin.content.events.add')}
        </Button>
      </div>
      <ul
        className="grid gap-1 max-h-64 overflow-y-auto"
        aria-label={t('admin.content.events.list')}
      >
        {shown.map((e) => {
          const change = changes.get(e.id);
          return (
            <li key={e.id}>
              <Button
                onClick={() => {
                  open(e.id, JSON.stringify(e.event, null, 2));
                }}
                aria-pressed={selected === e.id}
              >
                {e.id} v{e.version ?? '?'} · {e.phase ?? ''} · {e.title ?? ''}
              </Button>{' '}
              {change !== undefined && (
                <span className="font-bold">
                  {t(change.mechanical ? 'admin.content.mechanical' : 'admin.content.textOnly')}
                </span>
              )}
            </li>
          );
        })}
      </ul>
      {selected !== null && (
        <div className="grid gap-2">
          <label htmlFor="event-json" className="font-bold">
            {t('admin.content.events.json', { id: selected })}
          </label>
          <textarea
            id="event-json"
            className="field font-mono text-sm"
            rows={16}
            spellCheck={false}
            value={text}
            onChange={(event) => {
              setText(event.target.value);
            }}
          />
          <section className="grid gap-1" aria-label={t('admin.content.events.preview')}>
            <h4 className="font-bold m-0">{t('admin.content.events.preview')}</h4>
            {parsed === null ? (
              <p className="bad m-0">{t('admin.content.events.notJson')}</p>
            ) : (
              pronounForms.map((form) => (
                <div key={form}>
                  <p className="m-0 font-bold">{t(`admin.content.events.form.${form}`)}</p>
                  <ul className="m-0">
                    {preview(parsed, form).map((line, index) => (
                      <li key={`${String(index)}:${line}`}>{line}</li>
                    ))}
                  </ul>
                </div>
              ))
            )}
          </section>
          <div className="flex flex-wrap gap-2">
            <Button variant="primary" pending={pending} onClick={() => void save()}>
              {t('admin.content.events.save')}
            </Button>
            {draft.events.some((e) => e.id === selected) && (
              <Button pending={pending} onClick={() => void remove()}>
                {t('admin.content.events.remove')}
              </Button>
            )}
          </div>
        </div>
      )}
      {problem !== null && (
        <p role="alert" className="bad m-0">
          {t(problem)}
        </p>
      )}
    </section>
  );
}
