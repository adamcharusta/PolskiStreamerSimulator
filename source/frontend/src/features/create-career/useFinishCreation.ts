import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useState } from 'react';

import { track } from '@/analytics/analytics';
import { createCareer, getGetCareerQueryKey } from '@/api/generated/game';
import type { CreatedCareer } from '@/api/generated/game.schemas';
import type { ApiRequestError } from '@/api/http';
import { rememberCareer } from '@/careers/knownCareers';
import { clearPendingCreation, type PendingCreation } from '@/careers/pendingCreation';

export const createCareerMutationKey = ['createCareer'] as const;

/** A coded answer that a repeat of the same creation cannot change; a lost connection, 429 or 5xx may still pass. */
export function isFinal(error: ApiRequestError): boolean {
  return error.status !== null && error.status < 500 && error.status !== 429;
}

/**
 * Sends a pending creation and stores its answer. A lost connection is retried with the same creation ID, so the
 * server stores one career (TD-121). When this browser cannot store the answer, `unsaved` holds it, so the screen
 * can show the recovery key, the only way back to that career.
 */
export function useFinishCreation() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [unsaved, setUnsaved] = useState<CreatedCareer | null>(null);
  const creation = useMutation<CreatedCareer, ApiRequestError, PendingCreation>({
    mutationKey: createCareerMutationKey,
    mutationFn: (pending) => createCareer({ ...pending.request, creationId: pending.creationId }),
    onSuccess: async (created) => {
      try {
        rememberCareer({
          careerId: created.careerId,
          nickname: created.career.nickname,
          secret: created.secret,
          recoveryKey: created.recoveryKey,
          createdAt: new Date().toISOString(),
        });
      } catch {
        // A repeat would replace the key shown now, so the pending creation goes too.
        clearPendingCreation();
        setUnsaved(created);
        return;
      }

      clearPendingCreation();
      track('career_created', {
        background: created.career.background,
        difficulty: created.career.difficulty,
        ranked: created.career.ranked ?? false,
      });
      queryClient.setQueryData(getGetCareerQueryKey(created.careerId), created.career);
      await navigate({ to: '/kariera/$careerId/klucz', params: { careerId: created.careerId } });
    },
    onError: (error) => {
      if (isFinal(error)) {
        clearPendingCreation();
      }
    },
  });
  return { creation, unsaved };
}
