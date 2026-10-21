import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  ActionType,
  type CareerView,
  NextStepKind,
  type RollView,
} from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';
import { formatContent } from '@/content/icu';

import { RetryBanner } from '../RetryBanner';
import { useActionCommand } from '../useCareerCommand';
import { ChoiceOdds } from './ChoiceOdds';

export interface LiveEventProps {
  readonly career: CareerView;
  readonly secret: string;
  readonly onRolled: (roll: RollView) => void;
}

/**
 * One live situation or mandatory decision without a timer: every response shows its chance and modifiers
 * before the player commits. M1 events carry a title key and plain text, M3 events ICU text in the career's pronouns.
 */
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

  const decision = career.next.kind === NextStepKind.resolveDecision;
  const format = (text: string) => formatContent(text, career.pronouns);
  const title =
    event.title === null
      ? t(event.titleKey ?? 'event.untitled', { defaultValue: t('event.untitled') })
      : format(event.title);

  return (
    <div className="grid gap-3">
      <p>
        <span className="badge-live">
          {decision ? t('event.decision') : `● ${t('event.live')}`}
        </span>
      </p>
      <StepHeading>{title}</StepHeading>
      <p>{format(event.text)}</p>
      <fieldset className="choices" disabled={command.pending || command.stalled}>
        <legend className="font-bold">{t('event.choices')}</legend>
        {event.choices.map((choice) => {
          const unavailable = choice.unavailableReason;
          const hint = `event-choice-${choice.choiceId}`;
          return (
            <label key={choice.choiceId} className="choice">
              <input
                type="radio"
                name="choice"
                value={choice.choiceId}
                checked={choiceId === choice.choiceId}
                disabled={unavailable !== null}
                aria-describedby={unavailable === null ? undefined : hint}
                onChange={() => {
                  setChoiceId(choice.choiceId);
                }}
              />
              <span className="font-bold">{format(choice.label)}</span>
              <ChoiceOdds choice={choice} />
              {unavailable !== null && (
                <span id={hint} className="muted">
                  {t(`choice.${unavailable}`, { defaultValue: t('disabled.unknown') })}
                </span>
              )}
            </label>
          );
        })}
      </fieldset>
      <RetryBanner command={command} />
      <Button
        variant="primary"
        disabled={choiceId === null || command.stalled}
        pending={command.pending}
        onClick={() => {
          if (choiceId !== null) {
            command.run({
              expectedVersion: career.version,
              type: decision ? ActionType.resolveDecision : ActionType.resolveEvent,
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
