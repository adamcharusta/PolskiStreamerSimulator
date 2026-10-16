import { useMutation, useMutationState, useQueryClient } from '@tanstack/react-query';
import { useRef } from 'react';

import { getGetCareerQueryKey, runBatch, submitAction } from '@/api/generated/game';
import type {
  ActionOutcome,
  ActionRequest,
  BatchOutcome,
  BatchRequest,
  CareerView,
} from '@/api/generated/game.schemas';
import type { ApiRequestError } from '@/api/http';
import { careerAuth } from '@/careers/knownCareers';

export const careerMutationKey = (careerId: string) => ['career', careerId] as const;

type WithCommandId<T> = T & { readonly commandId: string };

export interface CareerCommand<TBody> {
  /** Starts a new intent with a fresh command ID; ignored while a request of this command runs. */
  readonly run: (body: TBody) => void;
  /** Sends the last unanswered intent again with its command ID, so the server answers it instead of rolling again. */
  readonly retry: () => void;
  readonly pending: boolean;
  readonly failure: ApiRequestError | null;
  /** A lost connection left the last intent unanswered: the screen waits for its retry, not for a new choice. */
  readonly stalled: boolean;
}

/**
 * One kind of career step. The response's career view replaces the cached career at once; a coded failure refetches
 * the career (another tab may have moved it); a lost connection keeps the intent for a retry with the same ID.
 */
export function useCareerCommand<TBody, TResult>(
  careerId: string,
  send: (request: WithCommandId<TBody>) => Promise<TResult>,
  careerOf: (result: TResult) => CareerView,
  onDone?: (result: TResult) => void,
  /** A coded refusal (not a lost connection), after the career was refetched. */
  onRefused?: (error: ApiRequestError) => void,
): CareerCommand<TBody> {
  const queryClient = useQueryClient();
  const inFlight = useRef(false);
  const last = useRef<WithCommandId<TBody> | null>(null);
  const mutation = useMutation<TResult, ApiRequestError, WithCommandId<TBody>>({
    mutationKey: careerMutationKey(careerId),
    mutationFn: send,
    onSuccess: (result) => {
      last.current = null;
      queryClient.setQueryData(getGetCareerQueryKey(careerId), careerOf(result));
      onDone?.(result);
    },
    onError: async (error) => {
      if (!error.isNetwork) {
        last.current = null;
        await queryClient.invalidateQueries({ queryKey: getGetCareerQueryKey(careerId) });
        onRefused?.(error);
      }
    },
    onSettled: () => {
      inFlight.current = false;
    },
  });

  // A ref, not isPending: two clicks in one tick both see the same render.
  const submit = (request: WithCommandId<TBody>) => {
    if (inFlight.current) {
      return;
    }

    inFlight.current = true;
    last.current = request;
    mutation.mutate(request);
  };

  return {
    run: (body) => {
      submit({ ...body, commandId: crypto.randomUUID() });
    },
    retry: () => {
      if (last.current !== null) {
        submit(last.current);
      }
    },
    pending: mutation.isPending,
    failure: mutation.error,
    stalled: mutation.error?.isNetwork === true,
  };
}

export type ActionInput = Omit<ActionRequest, 'commandId'>;

export function useActionCommand(
  careerId: string,
  secret: string,
  onDone?: (outcome: ActionOutcome) => void,
) {
  return useCareerCommand<ActionInput, ActionOutcome>(
    careerId,
    (request) => submitAction(careerId, request, careerAuth(secret)),
    (outcome) => outcome.career,
    onDone,
  );
}

export interface LatestCommand {
  readonly status: 'idle' | 'pending' | 'success' | 'error';
  readonly error: ApiRequestError | null;
}

/** The newest step of this career, wherever it was started: the save status and the failure notice read it. */
export function useLatestCommand(careerId: string): LatestCommand {
  const states = useMutationState({
    filters: { mutationKey: careerMutationKey(careerId) },
    select: (mutation) => mutation.state,
  });
  const latest = states.at(-1);
  return {
    status: latest?.status ?? 'idle',
    error: (latest?.error as ApiRequestError | null | undefined) ?? null,
  };
}

export type BatchInput = Omit<BatchRequest, 'commandId'>;

export function useBatchCommand(
  careerId: string,
  secret: string,
  onDone?: (outcome: BatchOutcome) => void,
  onRefused?: (error: ApiRequestError) => void,
) {
  return useCareerCommand<BatchInput, BatchOutcome>(
    careerId,
    (request) => runBatch(careerId, request, careerAuth(secret)),
    (outcome) => outcome.career,
    onDone,
    onRefused,
  );
}
