import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { getGetCareerQueryKey, recoverCareer } from '@/api/generated/game';
import type { RecoveredCareer } from '@/api/generated/game.schemas';
import type { ApiRequestError } from '@/api/http';
import { canStoreCareer, rememberCareer } from '@/careers/knownCareers';
import { findRecoveryKey } from '@/careers/recoveryKey';
import { Button } from '@/components/ui/Button';

function failureMessage(error: ApiRequestError): string {
  if (error.code === 'recovery-failed') {
    return 'recover.failed';
  }

  return error.code === 'rate-limited' ? 'recover.rateLimited' : 'recover.generic';
}

/** `/odzyskaj`: opens a career with its recovery key in this browser; the browser that had it loses access. */
export function RecoverScreen() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [key, setKey] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const submitting = useRef(false);
  const recovery = useMutation<RecoveredCareer, ApiRequestError, string>({
    mutationFn: (recoveryKey) => recoverCareer({ recoveryKey }),
    // Each recovery issues a new secret; a retried one after a lost answer simply issues the newest.
    onSuccess: async (recovered, recoveryKey) => {
      try {
        rememberCareer({
          careerId: recovered.careerId,
          nickname: recovered.career.nickname,
          secret: recovered.secret,
          recoveryKey,
          createdAt: new Date().toISOString(),
        });
      } catch {
        submitting.current = false;
        setProblem('recover.storage');
        return;
      }

      queryClient.setQueryData(getGetCareerQueryKey(recovered.careerId), recovered.career);
      await navigate({ to: '/kariera/$careerId', params: { careerId: recovered.careerId } });
    },
    onError: (error) => {
      submitting.current = false;
      setProblem(failureMessage(error));
    },
  });

  return (
    <div className="grid gap-3">
      <h1 className="heading text-3xl">{t('recover.title')}</h1>
      <form
        noValidate
        className="panel grid gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          const found = findRecoveryKey(key);
          if (found === null) {
            setProblem('recover.invalid');
            return;
          }

          if (!canStoreCareer()) {
            setProblem('recover.storage');
            return;
          }

          if (submitting.current) {
            return;
          }

          submitting.current = true;
          setProblem(null);
          recovery.mutate(found);
        }}
      >
        <p>{t('recover.intro')}</p>
        <label htmlFor="recovery-key" className="font-bold">
          {t('recover.keyLabel')}
        </label>
        <textarea
          id="recovery-key"
          className="field w-full font-mono"
          rows={3}
          autoComplete="off"
          spellCheck={false}
          value={key}
          onChange={(event) => {
            setKey(event.target.value);
          }}
        />
        <label htmlFor="recovery-file" className="font-bold">
          {t('recover.fileLabel')}
        </label>
        <input
          id="recovery-file"
          type="file"
          accept=".txt,text/plain"
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file === undefined) {
              return;
            }

            void file.text().then((text) => {
              const found = findRecoveryKey(text);
              setKey(found ?? '');
              setProblem(found === null ? 'recover.fileNoKey' : null);
            });
          }}
        />
        {problem !== null && (
          <p role="alert" className="bad">
            {t(problem)}
          </p>
        )}
        <Button type="submit" variant="primary" pending={recovery.isPending}>
          {t('recover.submit')}
        </Button>
      </form>
    </div>
  );
}
