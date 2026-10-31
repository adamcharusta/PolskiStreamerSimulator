import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  getGetOwnerSessionQueryKey,
  ownerLogin,
  ownerLoginCode,
  ownerLogout,
  useGetOwnerSession,
} from '@/api/generated/game';
import type { OwnerSessionView } from '@/api/generated/game.schemas';
import { isApiError } from '@/api/http';
import { Button } from '@/components/ui/Button';

import { AuditSection } from './AuditSection';
import { ContentSection } from './content/ContentSection';
import { csrf } from './csrf';
import { ExtensionSection } from './ExtensionSection';
import { ModerationSection } from './ModerationSection';

/** `/admin`: the owner's sign-in and panel (spec M4-D §7). The API decides access; this route is only lazy loaded. */
export function AdminScreen() {
  const { t } = useTranslation();
  const session = useGetOwnerSession({ query: { retry: false } });
  let body = <output className="block">{t('admin.loading')}</output>;
  if (session.data) {
    body = <Panel session={session.data} />;
  } else if (session.isError) {
    body = session.error.status === 401 ? <SignIn /> : <p role="alert">{t('admin.failed')}</p>;
  }

  return (
    <div className="grid gap-3">
      <h1 className="heading text-3xl">{t('admin.title')}</h1>
      {body}
    </div>
  );
}

function signInError(error: unknown): string {
  if (!isApiError(error)) {
    return 'admin.signIn.errors.generic';
  }

  const known: Record<string, string> = {
    'owner-login-failed': 'admin.signIn.errors.failed',
    'owner-locked-out': 'admin.signIn.errors.locked',
    'rate-limited': 'admin.signIn.errors.rateLimited',
  };
  return known[error.code] ?? 'admin.signIn.errors.generic';
}

function SignIn() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [step, setStep] = useState<'password' | 'code'>('password');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [recovery, setRecovery] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const submit = async () => {
    setProblem(null);
    setPending(true);
    try {
      if (step === 'password') {
        await ownerLogin({ username, password });
        setPassword('');
        setStep('code');
      } else {
        const session = await ownerLoginCode(
          recovery ? { recoveryCode: code.trim() } : { code: code.replaceAll(/\s/gu, '') },
        );
        queryClient.setQueryData(getGetOwnerSessionQueryKey(), session);
      }
    } catch (error) {
      setProblem(signInError(error));
      if (step === 'code' && isApiError(error) && error.code === 'owner-login-failed') {
        setCode('');
      }
    } finally {
      setPending(false);
    }
  };

  return (
    <form
      noValidate
      className="panel grid gap-2 max-w-md"
      aria-label={t('admin.signIn.title')}
      onSubmit={(event) => {
        event.preventDefault();
        void submit();
      }}
    >
      <h2 className="panel-title">{t('admin.signIn.title')}</h2>
      {step === 'password' ? (
        <>
          <label htmlFor="owner-username" className="font-bold">
            {t('admin.signIn.username')}
          </label>
          <input
            id="owner-username"
            className="field"
            autoComplete="username"
            value={username}
            onChange={(event) => {
              setUsername(event.target.value);
            }}
          />
          <label htmlFor="owner-password" className="font-bold">
            {t('admin.signIn.password')}
          </label>
          <input
            id="owner-password"
            type="password"
            className="field"
            autoComplete="current-password"
            value={password}
            onChange={(event) => {
              setPassword(event.target.value);
            }}
          />
        </>
      ) : (
        <>
          <p className="m-0">
            {t(recovery ? 'admin.signIn.recoveryIntro' : 'admin.signIn.codeIntro')}
          </p>
          <label htmlFor="owner-code" className="font-bold">
            {t(recovery ? 'admin.signIn.recoveryCode' : 'admin.signIn.code')}
          </label>
          <input
            id="owner-code"
            className="field font-mono"
            autoComplete="one-time-code"
            inputMode={recovery ? 'text' : 'numeric'}
            value={code}
            onChange={(event) => {
              setCode(event.target.value);
            }}
          />
          <p className="m-0">
            <Button
              onClick={() => {
                setRecovery(!recovery);
                setCode('');
                setProblem(null);
              }}
            >
              {t(recovery ? 'admin.signIn.useCode' : 'admin.signIn.useRecovery')}
            </Button>
          </p>
        </>
      )}
      {problem !== null && (
        <p role="alert" className="bad">
          {t(problem)}
        </p>
      )}
      <Button type="submit" variant="primary" pending={pending}>
        {t(step === 'password' ? 'admin.signIn.next' : 'admin.signIn.submit')}
      </Button>
    </form>
  );
}

function Panel({ session }: { readonly session: OwnerSessionView }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [pending, setPending] = useState(false);
  return (
    <>
      <section className="panel flex flex-wrap items-center gap-3" aria-label={t('admin.account')}>
        <span>{t('admin.signedInAs', { username: session.username })}</span>
        <Button
          pending={pending}
          onClick={() => {
            setPending(true);
            void ownerLogout(csrf(session))
              .catch(() => null)
              .finally(() => {
                setPending(false);
                void queryClient.resetQueries({ queryKey: getGetOwnerSessionQueryKey() });
              });
          }}
        >
          {t('admin.logout')}
        </Button>
        {session.recoveryCodesLeft < 3 && (
          <p role="alert" className="bad m-0 w-full">
            {t('admin.fewCodes', { count: session.recoveryCodesLeft })}
          </p>
        )}
      </section>
      <ModerationSection session={session} />
      <ExtensionSection session={session} />
      <ContentSection session={session} />
      <AuditSection />
    </>
  );
}
