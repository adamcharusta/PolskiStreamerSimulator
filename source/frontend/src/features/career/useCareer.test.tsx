import { QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';

import { createQueryClient } from '@/app/queryClient';

import { serveCareer } from '../../../test/api';
import { careerView } from '../../../test/fixtures';
import { rememberTestCareer, TEST_CAREER_ID } from '../../../test/render';
import { server } from '../../../test/server';
import { useCareer } from './useCareer';

function Wrapper({ children }: { readonly children: ReactNode }) {
  return (
    <QueryClientProvider client={createQueryClient({ retryDelay: 0 })}>
      {children}
    </QueryClientProvider>
  );
}

describe('useCareer', () => {
  it('treats a career without a stored secret as not found and asks nothing', () => {
    let requests = 0;
    server.use(
      http.all('*', () => {
        requests += 1;
        return HttpResponse.error();
      }),
    );

    const { result } = renderHook(() => useCareer(TEST_CAREER_ID), { wrapper: Wrapper });

    expect(result.current.secret).toBeNull();
    expect(result.current.query.fetchStatus).toBe('idle');
    expect(requests).toBe(0);
  });

  it('reads the career with its stored secret', async () => {
    rememberTestCareer();
    const seen = serveCareer(careerView({ version: 3 }));

    const { result } = renderHook(() => useCareer(TEST_CAREER_ID), { wrapper: Wrapper });

    await waitFor(() => {
      expect(result.current.query.data?.version).toBe(3);
    });
    expect(seen).toEqual(['Bearer secret-1']);
  });
});
