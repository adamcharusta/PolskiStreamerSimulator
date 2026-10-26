import { AlertDialog } from 'radix-ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import type { BatchOutcome, CareerView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';

import { RetryBanner } from '../RetryBanner';
import { useBatchCommand } from '../useCareerCommand';
import {
  type BatchLength,
  batchLengths,
  normalised,
  saveStrategy,
  storedStrategy,
  strategyErrors,
} from './strategy';
import { StrategyForm } from './StrategyForm';

export interface BatchLauncherProps {
  readonly career: CareerView;
  readonly secret: string;
  readonly onDone: (outcome: BatchOutcome) => void;
}

/**
 * One request plays 4, 13 or 52 weeks under the player's strategy; each week is already saved, so the dialog only
 * closes when it is done. The strategy is remembered per career in this browser.
 */
export function BatchLauncher({ career, secret, onDone }: BatchLauncherProps) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [weeks, setWeeks] = useState<BatchLength>(13);
  const [strategy, setStrategy] = useState(() => storedStrategy(career.careerId));
  const valid = strategyErrors(strategy).length === 0;
  const command = useBatchCommand(
    career.careerId,
    secret,
    (outcome) => {
      setOpen(false);
      onDone(outcome);
    },
    // A refusal is explained on the stage (CommandNotice), which the dialog would cover.
    () => {
      setOpen(false);
    },
  );
  // Running, or waiting for the retry of a batch lost in transit: only the same command may answer it.
  const busy = command.pending || command.stalled;
  return (
    <AlertDialog.Root
      open={open}
      onOpenChange={(next) => {
        if (!busy) {
          setOpen(next);
        }
      }}
    >
      <AlertDialog.Trigger asChild>
        <Button>{t('batch.launch')}</Button>
      </AlertDialog.Trigger>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="dialog-overlay" />
        <AlertDialog.Content className="dialog panel">
          <AlertDialog.Title className="panel-title">{t('batch.confirmTitle')}</AlertDialog.Title>
          <AlertDialog.Description>{t('batch.confirmBody')}</AlertDialog.Description>
          <fieldset className="choices" disabled={busy}>
            <legend className="font-bold">{t('batch.length')}</legend>
            {batchLengths.map((length) => (
              <label key={length} className="choice">
                <input
                  type="radio"
                  name="batch-weeks"
                  value={length}
                  checked={weeks === length}
                  onChange={() => {
                    setWeeks(length);
                  }}
                />
                {t('batch.weeks', { count: length })}
              </label>
            ))}
          </fieldset>
          <details>
            <summary>{t('strategy.title')}</summary>
            <StrategyForm
              value={strategy}
              onChange={setStrategy}
              disabled={busy}
              economy={career.rulesetId === 'm3-systems.2'}
            />
          </details>
          {command.pending && <output className="block">{t('batch.running')}</output>}
          <RetryBanner command={command} />
          <div className="mt-3 flex flex-wrap gap-2">
            <Button
              variant="primary"
              disabled={command.stalled || !valid}
              pending={command.pending}
              onClick={() => {
                const sent = normalised(strategy);
                saveStrategy(career.careerId, sent);
                command.run({ expectedVersion: career.version, weeks, strategy: sent });
              }}
            >
              {t('batch.confirm', { count: weeks })}
            </Button>
            <AlertDialog.Cancel asChild>
              <Button disabled={busy}>{t('batch.cancel')}</Button>
            </AlertDialog.Cancel>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
