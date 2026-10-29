import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import type { CreatedCareer } from '@/api/generated/game.schemas';
import { KeyBox } from '@/features/recovery/KeyBox';

/** The career exists but this browser could not store its access: the key is the only way back (TD-121). */
export function UnsavedCareer({ created }: { readonly created: CreatedCareer }) {
  const { t } = useTranslation();
  return (
    <div className="panel grid gap-2" role="alert">
      <p className="bad">{t('create.errors.unsaved')}</p>
      <KeyBox recoveryKey={created.recoveryKey} nickname={created.career.nickname} />
      <p>
        <Link className="btn btn-secondary" to="/odzyskaj">
          {t('home.recover')}
        </Link>
      </p>
    </div>
  );
}
