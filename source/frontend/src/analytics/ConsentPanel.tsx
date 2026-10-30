import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/components/ui/Button';

import { startFlushing, useConsent } from './analytics';

/**
 * Asks once, after the 18+ gate, whether the player allows anonymous gameplay counts; both answers weigh the same and
 * nothing is collected until „Zgadzam się” (spec M4-F §2). Flushing runs only while consent is granted.
 */
export function ConsentPanel() {
  const { t } = useTranslation();
  const consent = useConsent((state) => state.consent);
  const setConsent = useConsent((state) => state.setConsent);

  useEffect(() => (consent === 'granted' ? startFlushing() : undefined), [consent]);

  if (consent !== 'undecided') {
    return null;
  }

  return (
    <section className="topbar grid gap-2" aria-label={t('analytics.title')}>
      <p className="m-0 font-bold">{t('analytics.title')}</p>
      <p className="m-0">{t('analytics.body')}</p>
      <div className="flex flex-wrap gap-2">
        <Button
          onClick={() => {
            setConsent('granted');
          }}
        >
          {t('analytics.accept')}
        </Button>
        <Button
          onClick={() => {
            setConsent('denied');
          }}
        >
          {t('analytics.decline')}
        </Button>
      </div>
    </section>
  );
}
