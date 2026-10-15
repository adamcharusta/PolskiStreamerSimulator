import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';

import type { ApiRequestError } from '@/api/http';
import { Button } from '@/components/ui/Button';

export interface RetryableCommand {
  readonly failure: ApiRequestError | null;
  readonly pending: boolean;
  readonly retry: () => void;
}

/** After a lost connection: no result is shown, and the retry sends the same command again. */
export function RetryBanner({ command }: { readonly command: RetryableCommand }) {
  if (command.failure?.isNetwork !== true) {
    return null;
  }

  return <RetryPrompt command={command} />;
}

/** The step's own button is locked while the intent waits, so the retry takes focus when it appears. */
function RetryPrompt({ command }: { readonly command: RetryableCommand }) {
  const { t } = useTranslation();
  const retry = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    retry.current?.focus();
  }, []);

  return (
    <div role="alert" className="panel">
      <p className="bad">{t('career.networkFailed')}</p>
      <Button ref={retry} onClick={command.retry} pending={command.pending}>
        {t('career.retry')}
      </Button>
    </div>
  );
}
