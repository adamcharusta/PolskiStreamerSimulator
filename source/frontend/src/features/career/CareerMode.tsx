import { useTranslation } from 'react-i18next';

import type { CareerView } from '@/api/generated/game.schemas';

/** "Rankingowa · Sezon N" or "Bez rankingu"; fixed for the career's life (TD-122). */
export function CareerMode({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  return career.ranked === true && career.season != null
    ? t('dashboard.ranked', { season: career.season })
    : t('dashboard.unranked');
}
