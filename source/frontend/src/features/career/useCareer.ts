import { useGetCareer } from '@/api/generated/game';
import { careerAuth, careerSecret } from '@/careers/knownCareers';

/** The server-confirmed career. Without a stored secret this browser cannot open it, so nothing is requested. */
export function useCareer(careerId: string) {
  const secret = careerSecret(careerId);
  const query = useGetCareer(careerId, {
    query: { enabled: secret !== null },
    request: secret === null ? undefined : careerAuth(secret),
  });
  return { secret, query };
}
