import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/components/ui/Button';

/** The clipboard exists only in a secure context and may be denied; either way the key stays on screen to copy. */
async function copy(text: string): Promise<'copied' | 'failed'> {
  try {
    await navigator.clipboard.writeText(text);
    return 'copied';
  } catch {
    return 'failed';
  }
}

/** A recovery key with copy and download; the file holds the key and one line on how to use it. */
export function KeyBox({
  recoveryKey,
  nickname,
}: {
  readonly recoveryKey: string;
  readonly nickname: string;
}) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState<'idle' | 'copied' | 'failed'>('idle');
  return (
    <div className="grid gap-2">
      <output className="key-box" aria-label={t('recovery.keyTitle')}>
        {recoveryKey}
      </output>
      <div className="flex flex-wrap gap-2">
        <Button
          onClick={() => {
            void copy(recoveryKey).then(setCopied);
          }}
        >
          {t('recovery.copy')}
        </Button>
        <Button
          onClick={() => {
            const text = [
              t('recovery.fileIntro', { nickname }),
              recoveryKey,
              t('recovery.fileHow'),
              '',
            ].join('\n');
            const url = URL.createObjectURL(new Blob([text], { type: 'text/plain' }));
            const link = document.createElement('a');
            link.href = url;
            link.download = `klucz-${nickname}.txt`;
            link.click();
            URL.revokeObjectURL(url);
          }}
        >
          {t('recovery.download')}
        </Button>
      </div>
      {copied !== 'idle' && <p role="status">{t(`recovery.${copied}`)}</p>}
    </div>
  );
}
