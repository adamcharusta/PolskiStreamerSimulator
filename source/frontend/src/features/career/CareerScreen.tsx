import { useTranslation } from 'react-i18next';

export function CareerScreen() {
  const { t } = useTranslation();
  return <output className="block">{t('career.loading')}</output>;
}
