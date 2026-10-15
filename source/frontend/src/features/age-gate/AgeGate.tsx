import { AlertDialog } from 'radix-ui';
import { type ReactNode, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { Button } from '@/components/ui/Button';

const gateKey = 'pss.ageGate.v1';
const noticeKey = 'pss.onlineNotice.v1';
const gameThemes = ['gambling', 'adult', 'privacy', 'language'] as const;

type GateState = 'asking' | 'confirmed' | 'declined';

function storedValue(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function rememberValue(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // The explicit choice still applies for this page when storage is blocked.
  }
}

/**
 * A presentation gate, not age verification (content and experience §2): nothing of the game — routes, copy or
 * requests — renders before an explicit confirmation. Escape and outside clicks never close it.
 */
export function AgeGate({ children }: { readonly children: ReactNode }) {
  const { t } = useTranslation();
  const [state, setState] = useState<GateState>(() =>
    storedValue(gateKey) === 'confirmed' ? 'confirmed' : 'asking',
  );
  const [noticeOpen, setNoticeOpen] = useState(() => storedValue(noticeKey) !== 'dismissed');

  if (state === 'declined') {
    return (
      <main className="shell grid min-h-screen place-content-center gap-2 text-center">
        {/* The dialog took focus with it; the farewell receives it so that it is announced. */}
        <h1
          ref={(heading) => {
            heading?.focus();
          }}
          tabIndex={-1}
          className="heading text-3xl"
        >
          {t('gate.exitTitle')}
        </h1>
        <p>{t('gate.exitBody')}</p>
      </main>
    );
  }

  if (state === 'confirmed') {
    return (
      <>
        {noticeOpen && (
          <div className="topbar justify-between">
            <output>{t('notice.online')}</output>
            <Button
              onClick={() => {
                rememberValue(noticeKey, 'dismissed');
                setNoticeOpen(false);
              }}
            >
              {t('notice.dismiss')}
            </Button>
          </div>
        )}
        {children}
      </>
    );
  }

  return (
    <AlertDialog.Root open>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="dialog-overlay" />
        <AlertDialog.Content
          className="dialog panel"
          onEscapeKeyDown={(event) => {
            event.preventDefault();
          }}
        >
          <AlertDialog.Title className="panel-title text-2xl">{t('gate.title')}</AlertDialog.Title>
          <AlertDialog.Description>{t('gate.body')}</AlertDialog.Description>
          <ul className="list-disc space-y-1 pl-5">
            {gameThemes.map((name) => (
              <li key={name}>{t(`gate.themes.${name}`)}</li>
            ))}
          </ul>
          <p className="muted">{t('gate.note')}</p>
          <div className="flex flex-wrap gap-2">
            <AlertDialog.Action asChild>
              <Button
                variant="primary"
                onClick={() => {
                  rememberValue(gateKey, 'confirmed');
                  setState('confirmed');
                }}
              >
                {t('gate.confirm')}
              </Button>
            </AlertDialog.Action>
            <AlertDialog.Cancel asChild>
              <Button
                onClick={() => {
                  setState('declined');
                }}
              >
                {t('gate.leave')}
              </Button>
            </AlertDialog.Cancel>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
