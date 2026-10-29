import { useMutation } from '@tanstack/react-query';
import { Link, useParams } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { issueRecoveryKey } from '@/api/generated/game';
import type { IssuedRecoveryKey } from '@/api/generated/game.schemas';
import type { ApiRequestError } from '@/api/http';
import {
  careerAuth,
  type KnownCareer,
  knownCareer,
  rememberRecoveryKey,
} from '@/careers/knownCareers';
import { Button } from '@/components/ui/Button';

import { KeyBox } from './KeyBox';

/** The career's recovery key after creation and from the dashboard; issues one for careers that have none. */
export function RecoveryKeyScreen() {
  const { careerId } = useParams({ from: '/kariera/$careerId/klucz' });
  const { t } = useTranslation();
  const known = knownCareer(careerId);
  return (
    <div className="panel grid gap-2">
      <h1 className="heading text-2xl">{t('recovery.keyTitle')}</h1>
      {known === null ? (
        <>
          <p>{t('recovery.unknownCareer')}</p>
          <p>
            <Link className="btn btn-secondary" to="/odzyskaj">
              {t('home.recover')}
            </Link>
          </p>
        </>
      ) : (
        <KnownKey known={known} />
      )}
    </div>
  );
}

function KnownKey({ known }: { readonly known: KnownCareer }) {
  const { t } = useTranslation();
  const [key, setKey] = useState(known.recoveryKey ?? null);
  const [notStored, setNotStored] = useState(false);
  const issue = useMutation<IssuedRecoveryKey, ApiRequestError>({
    mutationFn: () => issueRecoveryKey(known.careerId, careerAuth(known.secret)),
    // A lost answer may have replaced the key already; retrying issues another one, so the newest answer counts.
    retry: false,
    onSuccess: (issued) => {
      setKey(issued.recoveryKey);
      try {
        rememberRecoveryKey(known.careerId, issued.recoveryKey);
        setNotStored(false);
      } catch {
        setNotStored(true);
      }
    },
  });
  return (
    <>
      <p>{t('recovery.keyIntro', { nickname: known.nickname })}</p>
      {key === null ? (
        <p>{t('recovery.missing')}</p>
      ) : (
        <KeyBox recoveryKey={key} nickname={known.nickname} />
      )}
      <p className="bad">{t('recovery.keyWarning')}</p>
      {notStored && <p role="alert">{t('recovery.keyNotStored')}</p>}
      {issue.isError && <p role="alert">{t('recovery.issueFailed')}</p>}
      <div className="flex flex-wrap gap-2">
        <Link
          className={key === null ? 'btn btn-secondary' : 'btn btn-primary'}
          to="/kariera/$careerId"
          params={{ careerId: known.careerId }}
        >
          {t(key === null ? 'recovery.backToCareer' : 'recovery.saved')}
        </Link>
        <Button
          variant={key === null ? 'primary' : 'secondary'}
          pending={issue.isPending}
          aria-describedby={key === null ? undefined : 'rotate-hint'}
          onClick={() => {
            issue.mutate();
          }}
        >
          {t(key === null ? 'recovery.issue' : 'recovery.rotate')}
        </Button>
      </div>
      {key !== null && (
        <p id="rotate-hint" className="muted">
          {t('recovery.rotateHint')}
        </p>
      )}
    </>
  );
}
