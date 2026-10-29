import { useIsMutating } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { type PendingCreation, pendingCreation } from '@/careers/pendingCreation';
import { Button } from '@/components/ui/Button';

import { UnsavedCareer } from './UnsavedCareer';
import { createCareerMutationKey, isFinal, useFinishCreation } from './useFinishCreation';

/**
 * Finishes a creation whose answer never reached this browser, for example after the tab was closed (TD-121): the
 * home screen sends the same creation ID once and then opens the key screen.
 */
export function PendingCreationPanel() {
  // Read once: the pending creation is cleared when this panel finishes it, and the panel must stay visible.
  const [pending] = useState<PendingCreation | null>(() => pendingCreation());
  const running = useIsMutating({ mutationKey: createCareerMutationKey });
  return pending === null ? null : <Finish pending={pending} othersRunning={running > 0} />;
}

function Finish({
  pending,
  othersRunning,
}: {
  readonly pending: PendingCreation;
  readonly othersRunning: boolean;
}) {
  const { t } = useTranslation();
  const { creation, unsaved } = useFinishCreation();
  const started = useRef(false);
  useEffect(() => {
    // The creation screen may still be waiting for its own answer; a second request would replace its key.
    if (started.current || othersRunning) {
      return;
    }

    started.current = true;
    creation.mutate(pending);
  }, [creation, pending, othersRunning]);

  if (unsaved !== null) {
    return <UnsavedCareer created={unsaved} />;
  }

  const nickname = pending.request.nickname;
  if (creation.error === null) {
    return <output className="panel block">{t('home.pendingTitle', { nickname })}</output>;
  }

  const closed = isFinal(creation.error);
  return (
    <div className="panel grid gap-2" role="alert">
      <p>{t(closed ? 'home.pendingClosed' : 'home.pendingFailed', { nickname })}</p>
      <div className="flex flex-wrap gap-2">
        {closed ? (
          <Link className="btn btn-secondary" to="/odzyskaj">
            {t('home.recover')}
          </Link>
        ) : (
          <Button
            pending={creation.isPending}
            onClick={() => {
              creation.mutate(pending);
            }}
          >
            {t('home.pendingRetry')}
          </Button>
        )}
      </div>
    </div>
  );
}
