import { AlertDialog } from 'radix-ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import type { BatchOutcome, CareerView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';

import { RetryBanner } from '../RetryBanner';
import { useBatchCommand } from '../useCareerCommand';

export interface BatchLauncherProps {
  readonly career: CareerView;
  readonly secret: string;
  readonly onDone: (outcome: BatchOutcome) => void;
}

/** One request plays up to 13 weeks; each week is already saved, so the dialog only closes when it is done. */
export function BatchLauncher({ career, secret, onDone }: BatchLauncherProps) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
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
          {command.pending && <output className="block">{t('batch.running')}</output>}
          <RetryBanner command={command} />
          <div className="mt-3 flex flex-wrap gap-2">
            <Button
              variant="primary"
              disabled={command.stalled}
              pending={command.pending}
              onClick={() => {
                command.run({ expectedVersion: career.version, weeks: 13 });
              }}
            >
              {t('batch.confirm')}
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
