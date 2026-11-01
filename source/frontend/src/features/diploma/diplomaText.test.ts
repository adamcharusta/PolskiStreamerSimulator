import { describe, expect, it } from 'vitest';

import type { ScoreView } from '@/api/generated/game.schemas';
import i18n from '@/i18n';
import { useTextFilters } from '@/textFilters/textFilters';

import { careerView } from '../../../test/fixtures';
import { diplomaText } from './diplomaText';

const score = { finalScore: 1000, followers: 0.5, wealth: 0.3, achievements: 0.2 } as ScoreView;
const t = (key: string, options?: Record<string, unknown>) => i18n.t(key, options);

describe('diploma text', () => {
  it('masks the nickname and every line in Streamer Friendly mode', () => {
    const career = careerView({ nickname: 'Kurwiszon', status: 'ended', ending: 'completed' });
    expect(diplomaText(career, t, new Date(2026, 10, 1), score).nickname).toBe('Kurwiszon');

    useTextFilters.getState().setFilter('streamerFriendly', true);
    const text = diplomaText(career, t, new Date(2026, 10, 1), score);

    expect(text.nickname).toBe('K********');
    expect(JSON.stringify(text)).not.toMatch(/kurw/iu);
  });
});
