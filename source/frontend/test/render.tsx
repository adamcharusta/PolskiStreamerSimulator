import { QueryClientProvider } from '@tanstack/react-query';
import { createMemoryHistory } from '@tanstack/react-router';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactElement } from 'react';

import { App } from '@/App';
import { createQueryClient } from '@/app/queryClient';
import { createAppRouter } from '@/app/router';
import { type KnownCareer, rememberCareer } from '@/careers/knownCareers';

export const TEST_CAREER_ID = '0199e3a0-0000-7000-8000-000000000001';

/** The whole app at `path`; by default the 18+ gate and the online notice were already answered. */
export function renderApp(path = '/', { confirmed = true }: { confirmed?: boolean } = {}) {
  if (confirmed) {
    localStorage.setItem('pss.ageGate.v1', 'confirmed');
    localStorage.setItem('pss.onlineNotice.v1', 'dismissed');
  }

  const history = createMemoryHistory({ initialEntries: [path] });
  const router = createAppRouter(history);
  const queryClient = createQueryClient({ retryDelay: 0 });
  const user = userEvent.setup();
  render(<App router={router} queryClient={queryClient} />);
  return { router, queryClient, user, history };
}

/** One component with a query client and no retry delay. */
export function renderWithClient(ui: ReactElement) {
  const queryClient = createQueryClient({ retryDelay: 0 });
  const user = userEvent.setup();
  render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
  return { queryClient, user };
}

export function rememberTestCareer(overrides: Partial<KnownCareer> = {}): KnownCareer {
  const career: KnownCareer = {
    careerId: TEST_CAREER_ID,
    nickname: 'Streamer',
    secret: 'secret-1',
    createdAt: '2026-10-15T12:00:00.000Z',
    ...overrides,
  };
  rememberCareer(career);
  return career;
}
