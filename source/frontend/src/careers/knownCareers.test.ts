import { describe, expect, it } from 'vitest';

import {
  careerAuth,
  careerSecret,
  forgetCareer,
  knownCareers,
  rememberCareer,
} from './knownCareers';

const career = (careerId: string) => ({
  careerId,
  nickname: `Nick ${careerId}`,
  secret: `secret-${careerId}`,
  createdAt: '2026-10-15T12:00:00.000Z',
});

describe('known careers', () => {
  it('remembers the newest career first and only once', () => {
    rememberCareer(career('a'));
    rememberCareer(career('b'));
    rememberCareer(career('a'));

    expect(knownCareers().map((c) => c.careerId)).toEqual(['a', 'b']);
  });

  it('forgets a career and its secret', () => {
    rememberCareer(career('a'));

    forgetCareer('a');

    expect(knownCareers()).toEqual([]);
    expect(careerSecret('a')).toBeNull();
  });

  it('ignores malformed known-careers storage', () => {
    localStorage.setItem('pss.careers.v1', 'not json');
    expect(knownCareers()).toEqual([]);

    localStorage.setItem('pss.careers.v1', JSON.stringify([{ careerId: 'x' }, career('b'), 42]));
    expect(knownCareers()).toEqual([career('b')]);

    localStorage.setItem(
      'pss.careers.v1',
      JSON.stringify([{ ...career('a'), createdAt: 'invalid-date' }, career('b')]),
    );
    expect(knownCareers()).toEqual([career('b')]);
  });

  it('sends a career secret as a bearer token', () => {
    expect(careerAuth('s3cret')).toEqual({ headers: { Authorization: 'Bearer s3cret' } });
  });
});
