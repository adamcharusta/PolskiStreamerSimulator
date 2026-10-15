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
  return new QueryClient({
    defaultOptions: {
      queries: { retry: shouldRetry, retryDelay, refetchOnWindowFocus: false },
      mutations: { retry: shouldRetry, retryDelay },
    },
  });
}
