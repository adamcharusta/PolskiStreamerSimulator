import { useTranslation } from 'react-i18next';

import { useLatestCommand } from './useCareerCommand';

/** Why the latest step was refused (for example another tab moved the career); the stage already shows the fresh step. */
export function CommandNotice({ careerId }: { readonly careerId: string }) {
  const { t } = useTranslation();
  const latest = useLatestCommand(careerId);
  if (latest.status !== 'error' || latest.error === null || latest.error.isNetwork) {
    return null;
  }

  return (
    <output className="panel block">
      {t(`career.errors.${latest.error.code}`, { defaultValue: t('career.errors.generic') })}
    </output>
  );
}
