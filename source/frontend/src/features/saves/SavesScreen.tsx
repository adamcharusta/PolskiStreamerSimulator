import { useQueryClient } from '@tanstack/react-query';
import { Link, useParams } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  exportCareer,
  getGetCareerQueryKey,
  getGetHistoryQueryKey,
  getListSaveSlotsQueryKey,
  useListSaveSlots,
  useRestoreSlot,
  useSaveToSlot,
} from '@/api/generated/game';
import type { CareerView, SaveSlotView } from '@/api/generated/game.schemas';
import { careerAuth } from '@/careers/knownCareers';
import { Button } from '@/components/ui/Button';
import { useCareer } from '@/features/career/useCareer';
import { count, date, pln } from '@/format';

import { saveFileName } from './saveFile';

/** `/kariera/$careerId/zapisy`: the three manual slots and the export of an unranked career (spec M4-C §7). */
export function SavesScreen() {
  const { careerId } = useParams({ from: '/kariera/$careerId/zapisy' });
  const { t } = useTranslation();
  const { secret, query } = useCareer(careerId);
  const career = query.data;
  let body = <output className="block">{t('saves.loading')}</output>;
  if (secret === null || query.isError) {
    body = <p role="alert">{t('saves.loadFailed')}</p>;
  } else if (career?.ranked === true) {
    body = <p>{t('saves.ranked')}</p>;
  } else if (career !== undefined) {
    body = <Slots career={career} secret={secret} />;
  }

  return (
    <div className="grid gap-3">
      <h1 className="heading text-3xl">{t('saves.title')}</h1>
      <p>
        <Link to="/kariera/$careerId" params={{ careerId }}>
          {t('saves.back')}
        </Link>
      </p>
      {body}
    </div>
  );
}

type Pending = { readonly kind: 'save' | 'restore'; readonly slot: number } | null;

function Slots({ career, secret }: { readonly career: CareerView; readonly secret: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const auth = careerAuth(secret);
  const slots = useListSaveSlots(career.careerId, { request: auth });
  const [confirming, setConfirming] = useState<Pending>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const save = useSaveToSlot({
    request: auth,
    mutation: {
      onSuccess: (view, { slot }) => {
        queryClient.setQueryData(getListSaveSlotsQueryKey(career.careerId), view);
        setNotice(t('saves.saved', { slot }));
      },
      onError: (error) => {
        setProblem(error.code === 'stale-version' ? 'saves.stale' : 'saves.saveFailed');
      },
    },
  });
  const restore = useRestoreSlot({
    request: auth,
    mutation: {
      onSuccess: async (outcome, { slot }) => {
        queryClient.setQueryData(getGetCareerQueryKey(career.careerId), outcome.career);
        await queryClient.invalidateQueries({ queryKey: getGetHistoryQueryKey(career.careerId) });
        setNotice(t('saves.restored', { slot, week: outcome.career.week }));
      },
      onError: (error) => {
        setProblem(error.code === 'stale-version' ? 'saves.stale' : 'saves.restoreFailed');
      },
    },
  });

  const start = (kind: 'save' | 'restore', slot: SaveSlotView) => {
    setNotice(null);
    setProblem(null);
    if (kind === 'save' && slot.empty) {
      save.mutate({
        id: career.careerId,
        slot: slot.slot,
        data: { expectedVersion: career.version },
      });
      return;
    }

    setConfirming({ kind, slot: slot.slot });
  };

  const confirm = () => {
    if (confirming === null) {
      return;
    }

    const { kind, slot } = confirming;
    setConfirming(null);
    if (kind === 'save') {
      save.mutate({ id: career.careerId, slot, data: { expectedVersion: career.version } });
    } else {
      restore.mutate({
        id: career.careerId,
        slot,
        data: { commandId: crypto.randomUUID(), expectedVersion: career.version },
      });
    }
  };

  if (slots.isError) {
    return <p role="alert">{t('saves.loadFailed')}</p>;
  }

  if (slots.data === undefined) {
    return <output className="block">{t('saves.loading')}</output>;
  }

  return (
    <>
      <p>{t('saves.intro')}</p>
      <ul className="grid gap-2 md:grid-cols-3" aria-label={t('saves.slotsLabel')}>
        {slots.data.slots.map((slot) => (
          <li key={slot.slot} className="panel grid gap-2">
            <h2 className="panel-title">{t('saves.slot', { slot: slot.slot })}</h2>
            <SlotSummary slot={slot} />
            {confirming?.slot === slot.slot ? (
              <div className="grid gap-2" role="group" aria-label={t('saves.confirmLabel')}>
                <p className="bad">
                  {t(confirming.kind === 'save' ? 'saves.confirmSave' : 'saves.confirmRestore', {
                    slot: slot.slot,
                  })}
                </p>
                <div className="flex flex-wrap gap-2">
                  <Button variant="primary" onClick={confirm}>
                    {t(confirming.kind === 'save' ? 'saves.overwrite' : 'saves.load')}
                  </Button>
                  <Button
                    onClick={() => {
                      setConfirming(null);
                    }}
                  >
                    {t('saves.cancel')}
                  </Button>
                </div>
              </div>
            ) : (
              <div className="flex flex-wrap gap-2">
                <Button
                  pending={save.isPending && save.variables.slot === slot.slot}
                  disabled={restore.isPending}
                  onClick={() => {
                    start('save', slot);
                  }}
                >
                  {t('saves.saveHere', { slot: slot.slot })}
                </Button>
                {!slot.empty && (
                  <Button
                    pending={restore.isPending && restore.variables.slot === slot.slot}
                    disabled={save.isPending}
                    onClick={() => {
                      start('restore', slot);
                    }}
                  >
                    {t('saves.restore', { slot: slot.slot })}
                  </Button>
                )}
              </div>
            )}
          </li>
        ))}
      </ul>
      {notice !== null && <output className="block">{notice}</output>}
      {problem !== null && (
        <p role="alert" className="bad">
          {t(problem)}
        </p>
      )}
      <ExportPanel career={career} secret={secret} />
    </>
  );
}

function SlotSummary({ slot }: { readonly slot: SaveSlotView }) {
  const { t } = useTranslation();
  if (slot.empty || slot.savedAt === null) {
    return <p className="m-0 muted">{t('saves.empty')}</p>;
  }

  return (
    <dl className="m-0 grid gap-1">
      <div>
        <dt className="inline font-bold">{t('saves.week')}: </dt>
        <dd className="inline">{slot.week}</dd>
      </div>
      <div>
        <dt className="inline font-bold">{t('career.cash')}: </dt>
        <dd className="inline">{pln(slot.cashPln ?? 0)}</dd>
      </div>
      <div>
        <dt className="inline font-bold">{t('career.followers')}: </dt>
        <dd className="inline">{count(slot.followers ?? 0)}</dd>
      </div>
      <div>
        <dt className="inline font-bold">{t('saves.savedAt')}: </dt>
        <dd className="inline">{date(slot.savedAt)}</dd>
      </div>
      {slot.status === 'ended' && <p className="m-0 muted">{t('saves.endedSlot')}</p>}
    </dl>
  );
}

function ExportPanel({ career, secret }: { readonly career: CareerView; readonly secret: string }) {
  const { t } = useTranslation();
  const [state, setState] = useState<'idle' | 'pending' | 'failed'>('idle');
  return (
    <section className="panel grid gap-2" aria-label={t('saves.exportTitle')}>
      <h2 className="panel-title">{t('saves.exportTitle')}</h2>
      <p className="m-0">{t('saves.exportIntro')}</p>
      <div>
        <Button
          pending={state === 'pending'}
          onClick={() => {
            setState('pending');
            exportCareer(career.careerId, careerAuth(secret))
              .then((file) => {
                const url = URL.createObjectURL(
                  new Blob([JSON.stringify(file, null, 2)], { type: 'application/json' }),
                );
                const link = document.createElement('a');
                link.href = url;
                link.download = saveFileName(career.nickname, career.week);
                link.click();
                URL.revokeObjectURL(url);
                setState('idle');
              })
              .catch(() => {
                setState('failed');
              });
          }}
        >
          {t('saves.export')}
        </Button>
      </div>
      {state === 'failed' && <p role="alert">{t('saves.exportFailed')}</p>}
    </section>
  );
}
