import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  ActionType,
  type CareerView,
  Category,
  EquipmentSlot,
  MainAction,
} from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';

import { actionBlock, optionFor, strongestCategory } from '../plans';
import { RetryBanner } from '../RetryBanner';
import { useActionCommand } from '../useCareerCommand';
import { WeekReport } from './WeekReport';

export interface PlanWeekProps {
  readonly career: CareerView;
  readonly secret: string;
  /** Placed next to the start button (the batch launcher). */
  readonly children?: ReactNode;
}

export function PlanWeek({ career, secret, children }: PlanWeekProps) {
  const { t } = useTranslation();
  const plans = career.next.plans;
  const command = useActionCommand(career.careerId, secret);
  const [action, setAction] = useState<MainAction | null>(null);
  const [category, setCategory] = useState<Category>(() => strongestCategory(career.stats));
  const [slot, setSlot] = useState<EquipmentSlot | null>(null);
  const chosen = action === null ? undefined : optionFor(plans, action, category, slot);
  const ready = chosen?.disabledReason === null;
  const reason = (code: string) => t(`disabled.${code}`, { defaultValue: t('disabled.unknown') });

  return (
    <div className="grid gap-3">
      <StepHeading>{t('plan.title')}</StepHeading>

      <fieldset className="choices">
        <legend className="font-bold">{t('plan.action')}</legend>
        {Object.values(MainAction).map((option) => {
          const block = actionBlock(plans, option, category);
          const hint = `plan-action-${option}`;
          return (
            <label key={option} className="choice">
              <input
                type="radio"
                name="action"
                value={option}
                checked={action === option}
                disabled={block !== null}
                aria-describedby={block === null ? undefined : hint}
                onChange={() => {
                  setAction(option);
                  setSlot(null);
                }}
              />
              <span className="font-bold">{t(`action.${option}`)}</span>
              {block !== null && (
                <span id={hint} className="muted">
                  {reason(block)}
                </span>
              )}
            </label>
          );
        })}
      </fieldset>

      <fieldset className="choices">
        <legend className="font-bold">{t('plan.category')}</legend>
        {Object.values(Category).map((option) => (
          <label key={option} className="choice">
            <input
              type="radio"
              name="category"
              value={option}
              checked={category === option}
              onChange={() => {
                setCategory(option);
              }}
            />
            {t(`category.${option}`)}
          </label>
        ))}
      </fieldset>

      {action === MainAction.improveProduction && (
        <fieldset className="choices">
          <legend className="font-bold">{t('plan.slot')}</legend>
          {Object.values(EquipmentSlot).map((option) => {
            const block = optionFor(plans, action, category, option)?.disabledReason ?? null;
            const hint = `plan-slot-${option}`;
            return (
              <label key={option} className="choice">
                <input
                  type="radio"
                  name="slot"
                  value={option}
                  checked={slot === option}
                  disabled={block !== null}
                  aria-describedby={block === null ? undefined : hint}
                  onChange={() => {
                    setSlot(option);
                  }}
                />
                <span className="font-bold">{t(`slot.${option}`)}</span>
                <span className="muted">
                  {t('stat.tier', { value: career.stats.equipment[option] })}
                </span>
                {block !== null && (
                  <span id={hint} className="muted">
                    {reason(block)}
                  </span>
                )}
              </label>
            );
          })}
        </fieldset>
      )}

      <RetryBanner command={command} />
      <div className="flex flex-wrap gap-2">
        <Button
          variant="primary"
          disabled={!ready}
          pending={command.pending}
          onClick={() => {
            if (action !== null) {
              command.run({
                expectedVersion: career.version,
                type: ActionType.startWeek,
                mainAction: action,
                category,
                upgradeSlot: action === MainAction.improveProduction ? slot : null,
              });
            }
          }}
        >
          {t('plan.start')}
        </Button>
        {children}
      </div>

      {career.lastReport !== null && (
        <details className="panel">
          <summary>{t('plan.lastReport', { week: career.lastReport.week })}</summary>
          <WeekReport report={career.lastReport} />
        </details>
      )}
    </div>
  );
}
