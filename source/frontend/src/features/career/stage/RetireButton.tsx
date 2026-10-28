import { AlertDialog } from 'radix-ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ActionType, type CareerView } from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';

import { RetryBanner } from '../RetryBanner';
import { useActionCommand } from '../useCareerCommand';

/** Early retirement between weeks, behind a confirmation because it cannot be undone. */
export function RetireButton({
  career,
  secret,
}: {
  readonly career: CareerView;
  readonly secret: string;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const command = useActionCommand(career.careerId, secret, () => {
    setOpen(false);
  });
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
        <Button>{t('retire.action')}</Button>
      </AlertDialog.Trigger>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="dialog-overlay" />
        <AlertDialog.Content className="dialog panel">
          <AlertDialog.Title className="panel-title">{t('retire.title')}</AlertDialog.Title>
          <AlertDialog.Description>{t('retire.body')}</AlertDialog.Description>
          <RetryBanner command={command} />
          <div className="mt-3 flex flex-wrap gap-2">
            <Button
              variant="primary"
              disabled={command.stalled}
              pending={command.pending}
              onClick={() => {
                command.run({ expectedVersion: career.version, type: ActionType.retire });
              }}
            >
              {t('retire.confirm')}
            </Button>
            <AlertDialog.Cancel asChild>
              <Button disabled={busy}>{t('retire.cancel')}</Button>
            </AlertDialog.Cancel>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
