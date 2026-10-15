import { useTranslation } from 'react-i18next';

export function CreateCareerScreen() {
  const { t } = useTranslation();
  return <h1 className="heading text-3xl">{t('create.title')}</h1>;
}
