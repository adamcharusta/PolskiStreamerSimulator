import { describe, expect, it } from 'vitest';

import { formatContent } from '@/content/icu';
import i18n from '@/i18n';

import { streamerFriendlyList } from './streamerFriendlyList';
import {
  masked,
  masker,
  maskStreamerFriendly,
  sensitiveTag,
  storedFilters,
  useTextFilters,
} from './textFilters';

describe('Streamer Friendly masking', () => {
  it('masks listed words in any letter case and keeps the first letter', () => {
    expect(maskStreamerFriendly('Kurwa, co za KURWA mać!')).toBe('K****, co za K**** mać!');
  });

  it('matches Polish letters and their plain forms alike', () => {
    expect(maskStreamerFriendly('ja pierdolę i ja pierdole')).toBe('ja p******* i ja p*******');
    expect(maskStreamerFriendly('Wypierdalaj')).toBe('W**********');
  });

  it('masks whole words only', () => {
    expect(maskStreamerFriendly('duplikat dupa dupą')).toBe('duplikat d*** d***');
    expect(maskStreamerFriendly('shitake shit')).toBe('shitake s***');
  });

  it('keeps punctuation and digits, and a starred entry also masks a word with digits after it', () => {
    expect(maskStreamerFriendly('(chuj)…chuj1 „kurwa”')).toBe('(c***)…c***1 „k****”');
  });

  it('masks a multiword phrase across any whitespace', () => {
    const mask = masker(['na pewno nie']);
    expect(mask('To na   pewno\tnie działa, ale nie na pewno.')).toBe(
      'To n*   p****\tn** działa, ale nie na pewno.',
    );
  });

  it('keeps a versioned list of lower-case entries', () => {
    expect(streamerFriendlyList.version).toBeGreaterThanOrEqual(1);
    for (const phrase of streamerFriendlyList.phrases) {
      expect(phrase).toBe(phrase.toLowerCase());
    }
  });

  it('applies to translations, content and announcements only while switched on', () => {
    const nickname = 'Kurwiszon';
    expect(masked(nickname)).toBe(nickname);
    expect(formatContent('Ja pierdolę!', 'woman')).toBe('Ja pierdolę!');

    useTextFilters.getState().setFilter('streamerFriendly', true);

    expect(masked(nickname)).toBe('K********');
    expect(formatContent('Ja pierdolę!', 'woman')).toBe('Ja p*******!');
    expect(i18n.t('career.summaryLabel') + ': ' + masked(nickname)).toContain('K********');
    expect(i18n.t('report.title', { week: 'kurwa' })).not.toContain('kurwa');
  });
});

describe('stored filters', () => {
  it('start off and survive a reload', () => {
    expect(storedFilters()).toEqual({ streamerFriendly: false, sensitive: false });
    useTextFilters.getState().setFilter('sensitive', true);
    expect(storedFilters()).toEqual({ streamerFriendly: false, sensitive: true });
  });

  it('read a malformed value as off', () => {
    localStorage.setItem('pss.textFilters.v1', '{"streamerFriendly":"yes"');
    expect(storedFilters()).toEqual({ streamerFriendly: false, sensitive: false });
  });
});

describe('Sensitive content wording', () => {
  it('names the tag to replace only while the filter is on', () => {
    expect(sensitiveTag(['adult-business'], false)).toBeNull();
    expect(sensitiveTag(['community'], true)).toBeNull();
    expect(sensitiveTag(null, true)).toBeNull();
    expect(sensitiveTag(['sponsor', 'privacy-incident'], true)).toBe('privacy-incident');
  });

  it('has a title, text and outcome for every tag', () => {
    for (const tag of ['adult-business', 'privacy-incident']) {
      for (const part of ['title', 'text', 'outcome']) {
        expect(i18n.exists(`filters.sensitiveText.${tag}.${part}`)).toBe(true);
      }
    }
  });
});
