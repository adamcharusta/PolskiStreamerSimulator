import { onlineManager, type QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { delay, HttpResponse } from 'msw';
import type { ReactNode } from 'react';
import { describe, expect, it } from 'vitest';

import { getGetCareerQueryKey } from '@/api/generated/game';
import { ActionType } from '@/api/generated/game.schemas';
import { createQueryClient } from '@/app/queryClient';

import { captureActions, problem, serveCareer } from '../../../test/api';
import { actionOutcome, careerView } from '../../../test/fixtures';
import { rememberTestCareer, TEST_CAREER_ID } from '../../../test/render';
import { useCareer } from './useCareer';
import { useActionCommand, useLatestCommand } from './useCareerCommand';

const settle = { expectedVersion: 1, type: ActionType.settleWeek };

function setup() {
  const queryClient: QueryClient = createQueryClient({ retryDelay: 0 });
  function Wrapper({ children }: { readonly children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  }
  // `pending` turns true only after a microtask, so tests wait for the confirmed career, not for `pending` to clear.
  const confirmed = (version: number) =>
    waitFor(() => {
      expect(queryClient.getQueryData(getGetCareerQueryKey(TEST_CAREER_ID))).toMatchObject({
        version,
      });
    });
  return { queryClient, Wrapper, confirmed };
}

describe('career commands', () => {
  it('sends the step with the career secret and stores the confirmed career', async () => {
    const received = captureActions(() =>
      HttpResponse.json(actionOutcome(careerView({ version: 2 }))),
    );
    const { queryClient, Wrapper } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), {
      wrapper: Wrapper,
    });

    act(() => {
      result.current.run(settle);
    });

    await waitFor(() => {
      expect(queryClient.getQueryData(getGetCareerQueryKey(TEST_CAREER_ID))).toMatchObject({
        version: 2,
      });
    });
    expect(received[0]).toMatchObject({ body: settle, authorization: 'Bearer secret-1' });
    expect(received[0]?.body.commandId).toMatch(/^[\da-f-]{36}$/u);
  });

  it('retries a network failure with the same command id', async () => {
    const received = captureActions((_, call) =>
      call === 1
        ? HttpResponse.error()
        : HttpResponse.json(actionOutcome(careerView({ version: 2 }))),
    );
    const { Wrapper, confirmed } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), {
      wrapper: Wrapper,
    });

    act(() => {
      result.current.run(settle);
    });

    await confirmed(2);
    expect(received).toHaveLength(2);
    expect(received[1]?.body.commandId).toBe(received[0]?.body.commandId);
    expect(result.current.failure).toBeNull();
  });

  it('keeps the intent for a manual retry once the automatic retries fail', async () => {
    const received = captureActions((_, call) =>
      call <= 3
        ? HttpResponse.error()
        : HttpResponse.json(actionOutcome(careerView({ version: 2 }))),
    );
    const { Wrapper, confirmed } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), {
      wrapper: Wrapper,
    });

    act(() => {
      result.current.run(settle);
    });
    await waitFor(() => {
      expect(result.current.failure?.isNetwork).toBe(true);
    });
    act(() => {
      result.current.retry();
    });

    await confirmed(2);
    expect(result.current.failure).toBeNull();
    expect(received).toHaveLength(4);
    expect(new Set(received.map((r) => r.body.commandId)).size).toBe(1);
  });

  it('sends the step and reports the lost connection when the browser says it is offline', async () => {
    const received = captureActions(() => HttpResponse.error());
    const { Wrapper } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), {
      wrapper: Wrapper,
    });
    onlineManager.setOnline(false);
    try {
      act(() => {
        result.current.run(settle);
      });

      await waitFor(() => {
        expect(result.current.failure?.isNetwork).toBe(true);
      });
      expect(result.current.stalled).toBe(true);
      expect(received).toHaveLength(3);
    } finally {
      onlineManager.setOnline(true);
    }
  });

  it('gives every new intent a new command id', async () => {
    const received = captureActions((_, call) =>
      HttpResponse.json(actionOutcome(careerView({ version: call + 1 }))),
    );
    const { Wrapper, confirmed } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), {
      wrapper: Wrapper,
    });

    act(() => {
      result.current.run(settle);
    });
    await confirmed(2);
    act(() => {
      result.current.run(settle);
    });

    await confirmed(3);
    expect(received).toHaveLength(2);
    expect(received[1]?.body.commandId).not.toBe(received[0]?.body.commandId);
  });

  it('ignores a second run while the first is pending', async () => {
    const received = captureActions(async () => {
      await delay(30);
      return HttpResponse.json(actionOutcome(careerView({ version: 2 })));
    });
    const { Wrapper, confirmed } = setup();
    const { result } = renderHook(() => useActionCommand(TEST_CAREER_ID, 'secret-1'), {
      wrapper: Wrapper,
    });

    act(() => {
      result.current.run(settle);
      result.current.run(settle);
    });

    await confirmed(2);
    expect(received).toHaveLength(1);
  });

  it('refetches the career after a stale version', async () => {
    rememberTestCareer();
    serveCareer(careerView({ version: 1 }), careerView({ version: 7 }));
    captureActions(() => problem(409, 'stale-version'));
    const { Wrapper } = setup();
    const { result } = renderHook(
      () => ({
        career: useCareer(TEST_CAREER_ID),
        command: useActionCommand(TEST_CAREER_ID, 'secret-1'),
        latest: useLatestCommand(TEST_CAREER_ID),
      }),
      { wrapper: Wrapper },
    );
    await waitFor(() => {
      expect(result.current.career.query.data?.version).toBe(1);
    });

    act(() => {
      result.current.command.run(settle);
    });

    await waitFor(() => {
      expect(result.current.career.query.data?.version).toBe(7);
    });
    expect(result.current.command.failure?.code).toBe('stale-version');
    expect(result.current.latest).toMatchObject({
      status: 'error',
      error: { code: 'stale-version' },
    });
  });
});
