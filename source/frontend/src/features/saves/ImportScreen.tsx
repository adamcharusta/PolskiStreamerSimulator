import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { getGetCareerQueryKey, importCareer } from '@/api/generated/game';
import type { CreatedCareer } from '@/api/generated/game.schemas';
import type { ApiRequestError } from '@/api/http';
import { canStoreCareer, rememberCareer } from '@/careers/knownCareers';
import { Button } from '@/components/ui/Button';
import { KeyBox } from '@/features/recovery/KeyBox';

import { parseSaveFile } from './saveFile';

const codes = new Set([
  'import-invalid',
  'import-newer',
  'import-unsupported',
  'payload-too-large',
  'rate-limited',
]);

function failureMessage(error: ApiRequestError): string {
  return codes.has(error.code) ? `import.errors.${error.code}` : 'import.errors.generic';
}

/** `/importuj`: a save file becomes a new unranked career with its own key; no existing career changes (spec M4-C §5). */
export function ImportScreen() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [save, setSave] = useState<unknown>(null);
  const [problem, setProblem] = useState<string | null>(null);
  const [unsaved, setUnsaved] = useState<CreatedCareer | null>(null);
  const submitting = useRef(false);
  const importing = useMutation<CreatedCareer, ApiRequestError, unknown>({
    mutationFn: (file) => importCareer({ save: file as object }),
    onSuccess: async (created) => {
      try {
        rememberCareer({
          careerId: created.careerId,
          nickname: created.career.nickname,
          secret: created.secret,
          recoveryKey: created.recoveryKey,
          createdAt: new Date().toISOString(),
        });
      } catch {
        setUnsaved(created);
        return;
      }

      queryClient.setQueryData(getGetCareerQueryKey(created.careerId), created.career);
      await navigate({ to: '/kariera/$careerId/klucz', params: { careerId: created.careerId } });
    },
    onError: (error) => {
      submitting.current = false;
      setProblem(failureMessage(error));
    },
  });

  if (unsaved !== null) {
    return (
      <div className="panel grid gap-2">
        <h1 className="heading text-2xl">{t('import.title')}</h1>
        <p role="alert">{t('import.notStored')}</p>
        <KeyBox recoveryKey={unsaved.recoveryKey} nickname={unsaved.career.nickname} />
      </div>
    );
  }

  return (
    <div className="grid gap-3">
      <h1 className="heading text-3xl">{t('import.title')}</h1>
      <form
        noValidate
        className="panel grid gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          if (save === null) {
            setProblem('import.errors.noFile');
            return;
          }

          if (!canStoreCareer()) {
            setProblem('import.errors.storage');
            return;
          }

          if (submitting.current) {
            return;
          }

          submitting.current = true;
          setProblem(null);
          importing.mutate(save);
        }}
      >
        <p>{t('import.intro')}</p>
        <label htmlFor="save-file" className="font-bold">
          {t('import.fileLabel')}
        </label>
        <input
          id="save-file"
          type="file"
          accept=".json,application/json"
          onChange={(event) => {
            const file = event.target.files?.[0];
            if (file === undefined) {
              return;
            }

            void file.text().then((text) => {
              const parsed = parseSaveFile(text);
              setSave(parsed);
              setProblem(parsed === null ? 'import.errors.notJson' : null);
            });
          }}
        />
        {problem !== null && (
          <p role="alert" className="bad">
            {t(problem)}
          </p>
        )}
        <Button type="submit" variant="primary" pending={importing.isPending}>
          {t('import.submit')}
        </Button>
      </form>
    </div>
  );
}
