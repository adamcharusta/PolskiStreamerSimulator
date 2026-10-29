import type { AxiosRequestConfig } from 'axios';

import type { OwnerSessionView } from '@/api/generated/game.schemas';

/** The anti-forgery header every signed-in admin POST needs (spec M4-D §4); the cookie travels on its own. */
export function csrf(session: OwnerSessionView): AxiosRequestConfig {
  return { headers: { 'X-CSRF-TOKEN': session.csrfToken } };
}
