import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  ActionType,
  type CareerView,
  Category,
  EquipmentSlot,
  MainAction,
  type Platform,
  type WagerTier,
} from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { StepHeading } from '@/components/ui/StepHeading';

import {
  actionBlock,
  migrationTargets,
  offeredActions,
  optionFor,
  strongestCategory,
  wagerTiers,
} from '../plans';
import { RetryBanner } from '../RetryBanner';
import { useActionCommand } from '../useCareerCommand';
import { ChoiceOdds } from './ChoiceOdds';
import { PlanRisks } from './PlanRisks';
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
  const [wager, setWager] = useState<WagerTier | null>(null);
  const [target, setTarget] = useState<Platform | null>(null);
  const tiers = action === null ? [] : wagerTiers(plans, action, category);
  const stake = wager !== null && tiers.includes(wager) ? wager : null;
  const targets =
    action === MainAction.planPlatformMigration ? migrationTargets(plans, category) : [];
  const chosen =
    action === null ? undefined : optionFor(plans, action, category, slot, stake, target);
  const ready = chosen?.disabledReason === null;
  // While a step is sent or waits for its retry, the shown choice is the one the server answers.
  const locked = command.pending || command.stalled;
  const reason = (code: string) => t(`disabled.${code}`, { defaultValue: t('disabled.unknown') });

  return (
    <div className="grid gap-3">
      <StepHeading>{t('plan.title')}</StepHeading>

      <fieldset className="choices" disabled={locked}>
        <legend className="font-bold">{t('plan.action')}</legend>
        {offeredActions(plans).map((option) => {
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
                  setTarget(null);
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

      <fieldset className="choices" disabled={locked}>
        <legend className="font-bold">{t('plan.category')}</legend>
        {Object.values(Category).map((option) => (
          <div key={option} className="choice">
            <label>
              <input
                type="radio"
                name="category"
                value={option}
                checked={category === option}
                aria-describedby={`plan-category-${option}`}
                onChange={() => {
                  setCategory(option);
                }}
              />
              {t(`category.${option}`)}
            </label>
            <span id={`plan-category-${option}`} className="muted">
              {t(`categoryHint.${option}`)}
            </span>
          </div>
        ))}
      </fieldset>

      {action === MainAction.improveProduction && (
        <fieldset className="choices" disabled={locked}>
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

      {tiers.length > 0 && action !== null && (
        <fieldset className="choices" disabled={locked}>
          <legend className="font-bold">{t('wager.title')}</legend>
          {[null, ...tiers].map((option) => {
            const block = optionFor(plans, action, category, slot, option)?.disabledReason ?? null;
            const name = option ?? 'none';
            const hint = `plan-wager-${name}`;
            return (
              <label key={name} className="choice">
                <input
                  type="radio"
                  name="wager"
                  value={name}
                  checked={stake === option}
                  disabled={block !== null}
                  aria-describedby={block === null ? undefined : hint}
                  onChange={() => {
                    setWager(option);
                  }}
                />
                <span className="font-bold">{t(`wager.${name}`)}</span>
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

      {targets.length > 0 && action !== null && (
        <fieldset className="choices" disabled={locked}>
          <legend className="font-bold">{t('platform.target')}</legend>
          {targets.map((option) => {
            const block =
              optionFor(plans, action, category, slot, null, option)?.disabledReason ?? null;
            const hint = `plan-target-${option}`;
            return (
              <label key={option} className="choice">
                <input
                  type="radio"
                  name="target"
                  value={option}
                  checked={target === option}
                  disabled={block !== null}
                  aria-describedby={block === null ? undefined : hint}
                  onChange={() => {
                    setTarget(option);
                  }}
                />
                <span className="font-bold">{t(`platform.${option}`)}</span>
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

      {chosen !== undefined && <PlanRisks option={chosen} />}

      {chosen?.odds && (
        <section className="choice" aria-label={t('plan.odds')}>
          <ChoiceOdds choice={chosen.odds} />
        </section>
      )}

      <RetryBanner command={command} />
      <div className="flex flex-wrap gap-2">
        <Button
          variant="primary"
          disabled={!ready || command.stalled}
          pending={command.pending}
          onClick={() => {
            if (action !== null) {
              command.run({
                expectedVersion: career.version,
                type: ActionType.startWeek,
                mainAction: action,
                category,
                upgradeSlot: action === MainAction.improveProduction ? slot : null,
                wager: stake,
                targetPlatform: action === MainAction.planPlatformMigration ? target : null,
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
          <WeekReport report={career.lastReport} pronouns={career.pronouns} />
        </details>
      )}
    </div>
  );
}
