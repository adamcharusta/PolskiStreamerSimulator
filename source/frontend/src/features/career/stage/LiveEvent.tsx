import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ActionType, type CareerView, type RollView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';
import { percent, signed, stat } from '@/format';

import { RetryBanner } from '../RetryBanner';
import { useActionCommand } from '../useCareerCommand';

export interface LiveEventProps {
  readonly career: CareerView;
  readonly secret: string;
  readonly onRolled: (roll: RollView) => void;
}

/** One live situation without a timer: every response shows its chance and modifiers before the player commits. */
export function LiveEvent({ career, secret, onRolled }: LiveEventProps) {
  const { t } = useTranslation();
  const [choiceId, setChoiceId] = useState<string | null>(null);
  const command = useActionCommand(career.careerId, secret, (outcome) => {
    if (outcome.roll !== null) {
      onRolled(outcome.roll);
    }
  });
  const event = career.next.event;
  if (event === null) {
    return null;
  }

  return (
    <div className="grid gap-3">
      <p>
        <span className="badge-live">● {t('event.live')}</span>
      </p>
      <StepHeading>{t(event.titleKey, { defaultValue: t('event.untitled') })}</StepHeading>
      <p>{event.textPl}</p>
      <fieldset className="choices">
        <legend className="font-bold">{t('event.choices')}</legend>
        {event.choices.map((choice) => (
          <label key={choice.choiceId} className="choice">
            <input
              type="radio"
              name="choice"
              value={choice.choiceId}
              checked={choiceId === choice.choiceId}
              onChange={() => {
                setChoiceId(choice.choiceId);
              }}
            />
            <span className="font-bold">{choice.labelPl}</span>
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
          </label>
        ))}
      </fieldset>
      <RetryBanner command={command} />
      <Button
        variant="primary"
        disabled={choiceId === null}
        pending={command.pending}
        onClick={() => {
          if (choiceId !== null) {
            command.run({
              expectedVersion: career.version,
              type: ActionType.resolveEvent,
              choiceId,
            });
          }
        }}
      >
        {t('event.choose')}
      </Button>
    </div>
  );
}
