import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import type { CareerView, ScoreView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';

import { diplomaText, loadFonts } from './diplomaText';

/** Builds the PDF in the browser and offers it as a download; nothing is sent anywhere. */
export function DiplomaButton({
  career,
  score,
}: {
  readonly career: CareerView;
  readonly score: ScoreView;
}) {
  const { t } = useTranslation();
  const [state, setState] = useState<'idle' | 'busy' | 'failed'>('idle');
  return (
    <div className="grid gap-1">
      <Button
        variant="primary"
        pending={state === 'busy'}
        onClick={() => {
          setState('busy');
          void loadFonts()
            .then(async (fonts) => {
              // pdf-lib is loaded only when a diploma is asked for, so it stays out of the main bundle.
              const { buildDiploma } = await import('./diploma');
              return buildDiploma(diplomaText(career, t, new Date(), score), fonts);
            })
            .then((pdf) => {
              const url = URL.createObjectURL(
                new Blob([pdf as BlobPart], { type: 'application/pdf' }),
              );
              const link = document.createElement('a');
              link.href = url;
              link.download = `dyplom-${career.nickname}.pdf`;
              link.click();
              URL.revokeObjectURL(url);
              setState('idle');
            })
            .catch(() => {
              setState('failed');
            });
        }}
      >
        {t('diploma.download')}
      </Button>
      {state === 'failed' && <p role="alert">{t('diploma.failed')}</p>}
    </div>
  );
}
