import { useTranslation } from 'react-i18next';

export function CareerScreen() {
  const { t } = useTranslation();
  return <p role="status">{t('career.loading')}</p>;
}
