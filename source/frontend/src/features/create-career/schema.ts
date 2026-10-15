import { z } from 'zod';

import { Background, Difficulty, PronounSet } from '@/api/generated/game.schemas';

// The server is the authority (TD-92); this mirror only saves a round trip. Letters of any script, digits, space, _ - .
const nicknamePattern = /^[\p{L}\p{Nd} _.-]{3,20}$/u;

export const createCareerSchema = z.object({
  nickname: z.string().trim().regex(nicknamePattern, { error: 'create.errors.nickname' }),
  background: z.enum(Background),
  difficulty: z.enum(Difficulty),
  pronouns: z.enum(PronounSet),
});

export type CreateCareerForm = z.input<typeof createCareerSchema>;
