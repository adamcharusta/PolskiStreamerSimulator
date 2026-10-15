import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import type { CareerView } from '@/api/generated/game.schemas';
import { StepHeading } from '@/components/ui/StepHeading';

/** The reason the career ended; the full ending screen with score and diploma arrives in M3. */
export function EndedView({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  return (
    <div className="grid gap-3">
      <StepHeading>{t('career.endedTitle')}</StepHeading>
      {career.ending !== null && <p>{t(`career.ending.${career.ending}`)}</p>}
      <p>
        <Link className="btn btn-secondary" to="/">
          {t('career.backHome')}
        </Link>
      </p>
    </div>
  );
}
