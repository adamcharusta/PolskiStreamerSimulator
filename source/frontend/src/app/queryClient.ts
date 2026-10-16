import { QueryClient } from '@tanstack/react-query';

import { isApiError } from '@/api/http';

/** Only a lost connection is retried, at most twice; a coded API answer is final. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  return isApiError(error) && error.isNetwork && failureCount < 2;
}

const backoff = (attempt: number): number => Math.min(1000 * 2 ** attempt, 4000);

export function createQueryClient({
  retryDelay = backoff,
}: { retryDelay?: number | ((attempt: number) => number) } = {}) {
  // networkMode 'always': when the browser reports offline, a request fails as a lost connection (and the player
  // sees "Brak połączenia" with a retry) instead of pausing in a loading state that never ends.
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: shouldRetry,
        retryDelay,
        refetchOnWindowFocus: false,
        networkMode: 'always',
      },
      mutations: { retry: shouldRetry, retryDelay, networkMode: 'always' },
    },
  });
}
