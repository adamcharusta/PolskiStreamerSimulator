import { useGetCareer } from '@/api/generated/game';
import type { CareerView } from '@/api/generated/game.schemas';
import { usePronounOverride } from '@/careers/displayPronouns';
import { careerAuth, careerSecret } from '@/careers/knownCareers';

/**
 * The server-confirmed career. Without a stored secret this browser cannot open it, so nothing is requested. Pronouns
 * chosen in settings replace the creation pronouns in what the screens read; the server never sees them.
 */
export function useCareer(careerId: string) {
  const secret = careerSecret(careerId);
  const pronouns = usePronounOverride(careerId);
  const query = useGetCareer(careerId, {
    query: {
      enabled: secret !== null,
      select: pronouns === undefined ? undefined : (view: CareerView) => ({ ...view, pronouns }),
    },
    request: secret === null ? undefined : careerAuth(secret),
  });
  return { secret, query };
}
