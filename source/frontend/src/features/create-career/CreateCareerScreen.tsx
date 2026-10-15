import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { type ReactNode, useRef, useState } from 'react';
import { useForm, useWatch } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { createCareer, getGetCareerQueryKey, useGetCareerOptions } from '@/api/generated/game';
import type {
  BackgroundOptionView,
  CareerOptionsView,
  CreateCareerRequest,
  CreatedCareer,
} from '@/api/generated/game.schemas';
import { type ApiRequestError, isApiError } from '@/api/http';
import { canStoreCareer, rememberCareer } from '@/careers/knownCareers';
import { Button } from '@/components/ui/Button';
import { Panel } from '@/components/ui/Panel';
import { count, multiplier, pln } from '@/format';

import { type CreateCareerForm, createCareerSchema } from './schema';

/** The category with the highest starting skill, or null when none stands out. */
function strongest(option: BackgroundOptionView): string | null {
  const [best, next] = Object.entries(option.skill).toSorted((a, b) => b[1] - a[1]);
  return best === undefined || best[1] === next?.[1] ? null : best[0];
}

/** Only a rejected nickname has a place on the form; every other rejection shows the generic error. */
function rejectsNickname(error: ApiRequestError | null): boolean {
  return error !== null && isApiError(error) && 'nickname' in error.errors;
}

export function CreateCareerScreen() {
  const { t } = useTranslation();
  const options = useGetCareerOptions();
  let body: ReactNode = <output className="block">{t('create.loading')}</output>;
  if (options.data) {
    body = <CreateCareerForm options={options.data} />;
  } else if (options.isError) {
    body = (
      <div>
        <p role="alert">{t('create.optionsFailed')}</p>
        <Button onClick={() => void options.refetch()}>{t('career.retry')}</Button>
      </div>
    );
  }
  return (
    <div className="grid gap-3">
      <h1 className="heading text-3xl">{t('create.title')}</h1>
      {body}
    </div>
  );
}

function CreateCareerForm({ options }: { readonly options: CareerOptionsView }) {
  const { t } = useTranslation();
  const [storageError, setStorageError] = useState(false);
  // Set before the request, so a second activation in the same moment cannot create a second career.
  const submitting = useRef(false);
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const form = useForm<CreateCareerForm, unknown, CreateCareerRequest>({
    resolver: zodResolver(createCareerSchema),
    defaultValues: {
      nickname: '',
      background: 'dormRoomNewcomer',
      difficulty: 'normal',
      pronouns: 'woman',
    },
  });
  const creation = useMutation<CreatedCareer, ApiRequestError, CreateCareerRequest>({
    mutationFn: (request) => createCareer(request),
    retry: false,
    onSuccess: async (created) => {
      rememberCareer({
        careerId: created.careerId,
        nickname: created.career.nickname,
        secret: created.secret,
        createdAt: new Date().toISOString(),
      });
      queryClient.setQueryData(getGetCareerQueryKey(created.careerId), created.career);
      await navigate({ to: '/kariera/$careerId', params: { careerId: created.careerId } });
    },
    onError: (error) => {
      submitting.current = false;
      if (rejectsNickname(error)) {
        form.setError('nickname', { message: 'create.errors.nickname' });
      }
    },
  });
  // useWatch, not watch(): the React Compiler lint rules reject watch() (eslint-plugin-react-hooks 7).
  const watched = useWatch({ control: form.control });
  const nickname = watched.nickname ?? '';
  const background = watched.background ?? 'dormRoomNewcomer';
  const difficulty = watched.difficulty ?? 'normal';
  const pronouns = watched.pronouns ?? 'woman';
  const nicknameError = form.formState.errors.nickname?.message;

  return (
    <form
      noValidate
      className="grid gap-3"
      onSubmit={(event) => {
        void form.handleSubmit((request) => {
          if (submitting.current) {
            return;
          }
          if (!canStoreCareer()) {
            setStorageError(true);
            return;
          }
          setStorageError(false);
          submitting.current = true;
          creation.mutate(request);
        })(event);
      }}
    >
      <div className="panel">
        <label htmlFor="nickname" className="block font-bold">
          {t('create.nickname')}
        </label>
        <input
          id="nickname"
          className="field w-full"
          autoComplete="off"
          aria-describedby="nickname-hint"
          aria-invalid={nicknameError ? true : undefined}
          {...form.register('nickname')}
        />
        <p id="nickname-hint" className="muted">
          {nicknameError ? (
            <span className="bad">{t(nicknameError)}</span>
          ) : (
            t('create.nicknameHint')
          )}
        </p>
      </div>

      <fieldset className="choices panel">
        <legend className="panel-title">{t('create.background')}</legend>
        {options.backgrounds.map((option) => {
          const best = strongest(option);
          return (
            <label key={option.background} className="choice">
              <input type="radio" value={option.background} {...form.register('background')} />
              <span className="font-bold">{t(`background.${option.background}`)}</span>
              <span>{t('create.cash', { value: pln(option.cashPln) })}</span>
              <span>{t('create.followers', { value: count(option.followers) })}</span>
              <span>
                {best === null
                  ? t('create.strongestNone')
                  : t('create.strongest', { value: t(`category.${best}`) })}
              </span>
              <span>{t('create.equipment', { value: option.equipment.microphone })}</span>
              <span>{t('create.multiplier', { value: multiplier(option.scoreMultiplier) })}</span>
            </label>
          );
        })}
      </fieldset>

      <fieldset className="choices panel">
        <legend className="panel-title">{t('create.difficulty')}</legend>
        {options.difficulties.map((option) => (
          <label key={option.difficulty} className="choice">
            <input type="radio" value={option.difficulty} {...form.register('difficulty')} />
            <span className="font-bold">{t(`difficulty.${option.difficulty}`)}</span>
            <span>{t('create.multiplier', { value: multiplier(option.scoreMultiplier) })}</span>
          </label>
        ))}
      </fieldset>

      <fieldset className="choices panel">
        <legend className="panel-title">{t('create.pronouns')}</legend>
        {options.pronouns.map((pronouns) => (
          <label key={pronouns} className="choice">
            <input type="radio" value={pronouns} {...form.register('pronouns')} />
            {t(`pronouns.${pronouns}`)}
          </label>
        ))}
      </fieldset>

      <Panel title={t('create.summary')}>
        <p>
          {nickname.trim() || '—'} · {t(`background.${background}`)} ·{' '}
          {t(`difficulty.${difficulty}`)} · {t(`pronouns.${pronouns}`)}
        </p>
        {storageError && (
          <p role="alert" className="bad">
            {t('create.errors.storage')}
          </p>
        )}
        {!storageError && creation.error !== null && !rejectsNickname(creation.error) && (
          <p role="alert" className="bad">
            {t('create.errors.generic')}
          </p>
        )}
        <Button type="submit" variant="primary" pending={creation.isPending}>
          {t('create.submit')}
        </Button>
      </Panel>
    </form>
  );
}
