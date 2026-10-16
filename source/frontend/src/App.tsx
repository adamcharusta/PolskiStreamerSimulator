import { type QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider } from '@tanstack/react-router';
import { useState } from 'react';

import { createQueryClient } from './app/queryClient';
import { type AppRouter, createAppRouter } from './app/router';
import { AgeGate } from './features/age-gate/AgeGate';

export interface AppProps {
  readonly router?: AppRouter;
  readonly queryClient?: QueryClient;
}

export function App(props: AppProps) {
  const [router] = useState(() => props.router ?? createAppRouter());
  const [queryClient] = useState(() => props.queryClient ?? createQueryClient());
  return (
    <QueryClientProvider client={queryClient}>
      <AgeGate>
        <RouterProvider router={router} />
      </AgeGate>
    </QueryClientProvider>
  );
}
