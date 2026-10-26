import { useTranslation } from 'react-i18next';

import {
  Category,
  CategoryPlan,
  type MainAction,
  RiskAppetite,
  type StrategyInput,
  WagerTier,
} from '@/api/generated/game.schemas';

import { routineActions, strategyErrors } from './strategy';

export interface StrategyFormProps {
  readonly value: StrategyInput;
  readonly onChange: (next: StrategyInput) => void;
  readonly disabled: boolean;
  /** An M3-B career offers the wager and the side business. */
  readonly economy: boolean;
}

/** The player's strategy for automatic weeks (spec 2026-10-26 §5); every field is labelled and errors are announced. */
export function StrategyForm({ value, onChange, disabled, economy }: StrategyFormProps) {
  const { t } = useTranslation();
  const set = (patch: Partial<StrategyInput>) => {
    onChange({ ...value, ...patch });
  };
  const errors = strategyErrors(value);
  const number = (raw: string) => (raw === '' ? Number.NaN : Number(raw));
  const setStep = (index: number, action: MainAction) => {
    set({ routine: value.routine.map((step, i) => (i === index ? action : step)) });
  };

  return (
    <fieldset className="grid gap-2" disabled={disabled}>
      <legend className="font-bold">{t('strategy.title')}</legend>

      <label className="grid gap-1">
        {t('strategy.categoryPlan')}
        <select
          value={value.categoryPlan}
          onChange={(e) => {
            set({ categoryPlan: e.target.value as CategoryPlan });
          }}
        >
          {Object.values(CategoryPlan).map((plan) => (
            <option key={plan} value={plan}>
              {t(`strategy.plan.${plan}`)}
            </option>
          ))}
        </select>
      </label>

      {value.categoryPlan === CategoryPlan.focus && (
        <label className="grid gap-1">
          {t('strategy.focus')}
          <select
            value={value.focus ?? ''}
            onChange={(e) => {
              set({ focus: e.target.value === '' ? null : (e.target.value as Category) });
            }}
          >
            <option value="">{t('strategy.chooseFocus')}</option>
            {Object.values(Category).map((category) => (
              <option key={category} value={category}>
                {t(`category.${category}`)}
              </option>
            ))}
          </select>
        </label>
      )}

      <fieldset className="grid gap-1">
        <legend>{t('strategy.routine')}</legend>
        {value.routine.map((step, index) => (
          <label key={`step-${String(index)}`} className="grid gap-1">
            {t('strategy.step', { step: index + 1 })}
            <select
              value={step}
              onChange={(e) => {
                setStep(index, e.target.value as MainAction);
              }}
            >
              {routineActions.map((action) => (
                <option key={action} value={action}>
                  {t(`action.${action}`)}
                </option>
              ))}
            </select>
          </label>
        ))}
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            className="btn btn-secondary"
            disabled={value.routine.length >= 8}
            onClick={() => {
              set({ routine: [...value.routine, routineActions[0]] });
            }}
          >
            {t('strategy.addStep')}
          </button>
          <button
            type="button"
            className="btn btn-secondary"
            disabled={value.routine.length <= 1}
            onClick={() => {
              set({ routine: value.routine.slice(0, -1) });
            }}
          >
            {t('strategy.removeStep')}
          </button>
        </div>
      </fieldset>

      <label className="grid gap-1">
        {t('strategy.restBelowEnergy')}
        <input
          type="number"
          min={0}
          max={80}
          value={Number.isNaN(value.restBelowEnergy) ? '' : value.restBelowEnergy}
          onChange={(e) => {
            set({ restBelowEnergy: number(e.target.value) });
          }}
        />
      </label>
      <label className="grid gap-1">
        {t('strategy.restAboveBurnout')}
        <input
          type="number"
          min={50}
          max={100}
          value={Number.isNaN(value.restAboveBurnout) ? '' : value.restAboveBurnout}
          onChange={(e) => {
            set({ restAboveBurnout: number(e.target.value) });
          }}
        />
      </label>
      <label className="grid gap-1">
        {t('strategy.reserve')}
        <input
          type="number"
          min={0}
          max={1_000_000}
          step={100}
          value={Number.isNaN(value.reservePln) ? '' : value.reservePln}
          onChange={(e) => {
            set({ reservePln: number(e.target.value) });
          }}
        />
      </label>

      <label className="grid gap-1">
        {t('strategy.risk')}
        <select
          value={value.risk}
          onChange={(e) => {
            set({ risk: e.target.value as RiskAppetite });
          }}
        >
          {Object.values(RiskAppetite).map((risk) => (
            <option key={risk} value={risk}>
              {t(`strategy.riskLevel.${risk}`)}
            </option>
          ))}
        </select>
      </label>

      <label className="choice">
        <input
          type="checkbox"
          checked={value.upgrades}
          onChange={(e) => {
            set({ upgrades: e.target.checked });
          }}
        />
        {t('strategy.upgrades')}
      </label>
      <label className="choice">
        <input
          type="checkbox"
          checked={value.acceptOffers}
          onChange={(e) => {
            set({ acceptOffers: e.target.checked });
          }}
        />
        {t('strategy.acceptOffers')}
      </label>

      {economy && (
        <>
          <label className="grid gap-1">
            {t('strategy.wager')}
            <select
              value={value.wager ?? ''}
              onChange={(e) => {
                set({ wager: e.target.value === '' ? null : (e.target.value as WagerTier) });
              }}
            >
              <option value="">{t('wager.none')}</option>
              {Object.values(WagerTier).map((tier) => (
                <option key={tier} value={tier}>
                  {t(`wager.${tier}`)}
                </option>
              ))}
            </select>
          </label>
          <label className="choice">
            <input
              type="checkbox"
              checked={value.sideBusiness}
              onChange={(e) => {
                set({ sideBusiness: e.target.checked });
              }}
            />
            {t('strategy.sideBusiness')}
          </label>
        </>
      )}

      {errors.length > 0 && (
        <ul role="alert" className="bad">
          {errors.map((code) => (
            <li key={code}>{t(`strategy.error.${code}`)}</li>
          ))}
        </ul>
      )}
    </fieldset>
  );
}
