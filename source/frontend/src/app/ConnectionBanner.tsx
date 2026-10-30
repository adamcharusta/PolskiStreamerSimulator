import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { http, isApiError } from '@/api/http';

/** A lost connection or a server error; a coded 4xx answer means the server is there. */
function unreachable(error: unknown): boolean {
  return isApiError(error) && (error.isNetwork || (error.status ?? 0) >= 500);
}

/**
 * Says that play is paused while the API cannot be reached (spec M4-F §3, TD-28). Nothing is queued offline: the
 * screens keep the last confirmed state and their own retry. A `/health` probe every 15 seconds clears the banner.
 */
export function ConnectionBanner({ probeInterval = 15_000 }: { readonly probeInterval?: number }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [down, setDown] = useState(() => typeof navigator !== 'undefined' && !navigator.onLine);

  useEffect(() => {
    const onResult = (failed: boolean) => {
      setDown(failed);
    };
    const queries = queryClient.getQueryCache().subscribe((event) => {
      if (event.type === 'updated' && event.action.type === 'error') {
        onResult(unreachable(event.action.error));
      } else if (event.type === 'updated' && event.action.type === 'success') {
        onResult(false);
      }
    });
    const mutations = queryClient.getMutationCache().subscribe((event) => {
      const state = event.mutation?.state;
      if (event.type === 'updated' && state?.status === 'error') {
        onResult(unreachable(state.error));
      } else if (event.type === 'updated' && state?.status === 'success') {
        onResult(false);
      }
    });
    const offline = () => {
      setDown(true);
    };
    globalThis.addEventListener('offline', offline);
    return () => {
      queries();
      mutations();
      globalThis.removeEventListener('offline', offline);
    };
  }, [queryClient]);

  useEffect(() => {
    if (!down) {
      return;
    }

    const timer = setInterval(() => {
      http
        .get('/health', { timeout: 5000 })
        .then(() => {
          setDown(false);
        })
        .catch(() => null);
    }, probeInterval);
    return () => {
      clearInterval(timer);
    };
  }, [down, probeInterval]);

  if (!down) {
    return null;
  }

  return (
    // A polite live region: the screen's own failure message stays the alert that takes attention.
    <output className="block topbar bad">{t('connection.down')}</output>
  );
}
