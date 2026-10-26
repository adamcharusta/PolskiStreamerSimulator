import { useTranslation } from 'react-i18next';

import type { ChoiceView } from '@/api/generated/game.schemas';
import { percent, signed, stat } from '@/format';

import { MigrationEstimate } from './PlanRisks';

/** The chance of a rolled choice with its modifiers, critical outcome, viral chance and the platform move it makes, shown before the player commits. */
export function ChoiceOdds({ choice }: { readonly choice: ChoiceView }) {
  const { t } = useTranslation();
  return (
    <>
      <span>
        {t('event.chance', {
          value: choice.guaranteed ? t('event.certain') : percent(choice.probability),
        })}
      </span>
      {choice.modifiers.length > 0 && (
        <span className="muted">
          {t('event.modifiers')}:{' '}
          {choice.modifiers
            .map(
              (modifier) =>
                `${t(modifier.labelKey, { defaultValue: modifier.labelKey })} ${t('event.points', { value: signed(modifier.value * 100, stat) })}`,
            )
            .join(', ')}
        </span>
      )}
      {choice.allowsCritical && <span className="muted">{t('event.critical')}</span>}
      {choice.migration !== null && <MigrationEstimate estimate={choice.migration} />}
      {choice.viralOnCritical > 0 && (
        <span className="muted">
          {t('event.viralHint', { value: stat(choice.viralOnCritical * 100) })}
        </span>
      )}
    </>
  );
}
