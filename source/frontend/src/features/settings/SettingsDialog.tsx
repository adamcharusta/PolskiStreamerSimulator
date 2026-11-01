import { useParams } from '@tanstack/react-router';
import { Dialog } from 'radix-ui';
import { useTranslation } from 'react-i18next';

import { useConsent } from '@/analytics/analytics';
import { PronounSet } from '@/api/generated/game.schemas';
import { useDisplayPronouns } from '@/careers/displayPronouns';
import { Button } from '@/components/ui/Button';
import { useCareer } from '@/features/career/useCareer';
import { useTextFilters } from '@/textFilters/textFilters';
import { themes } from '@/theme/theme';
import { useTheme } from '@/theme/useTheme';

export function SettingsDialog() {
  const { t } = useTranslation();
  const consent = useConsent((state) => state.consent);
  const setConsent = useConsent((state) => state.setConsent);
  const theme = useTheme((state) => state.theme);
  const setTheme = useTheme((state) => state.setTheme);
  const streamerFriendly = useTextFilters((state) => state.streamerFriendly);
  const sensitive = useTextFilters((state) => state.sensitive);
  const setFilter = useTextFilters((state) => state.setFilter);
  return (
    <Dialog.Root>
      <Dialog.Trigger asChild>
        <Button>{t('app.settings')}</Button>
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="dialog-overlay" />
        <Dialog.Content className="dialog panel" aria-describedby={undefined}>
          <Dialog.Title className="panel-title">{t('app.settings')}</Dialog.Title>
          <fieldset className="choices">
            <legend>{t('theme.legend')}</legend>
            {themes.map((name) => (
              <label key={name} className="choice">
                <input
                  type="radio"
                  name="theme"
                  value={name}
                  checked={theme === name}
                  onChange={() => {
                    setTheme(name);
                  }}
                />
                {t(`theme.${name}`)}
              </label>
            ))}
          </fieldset>
          <CareerPronouns />
          <fieldset className="choices">
            <legend>{t('filters.legend')}</legend>
            <label className="choice">
              <input
                type="checkbox"
                checked={streamerFriendly}
                aria-describedby="filters-streamer-friendly"
                onChange={(event) => {
                  setFilter('streamerFriendly', event.target.checked);
                }}
              />
              {t('filters.streamerFriendly')}
            </label>
            <p id="filters-streamer-friendly" className="m-0 muted">
              {t('filters.streamerFriendlyHint')}
            </p>
            <label className="choice">
              <input
                type="checkbox"
                checked={sensitive}
                aria-describedby="filters-sensitive"
                onChange={(event) => {
                  setFilter('sensitive', event.target.checked);
                }}
              />
              {t('filters.sensitive')}
            </label>
            <p id="filters-sensitive" className="m-0 muted">
              {t('filters.sensitiveHint')}
            </p>
          </fieldset>
          <fieldset className="choices">
            <legend>{t('analytics.settingsLegend')}</legend>
            <label className="choice">
              <input
                type="checkbox"
                checked={consent === 'granted'}
                onChange={(event) => {
                  setConsent(event.target.checked ? 'granted' : 'denied');
                }}
              />
              {t('analytics.settingsLabel')}
            </label>
            <p className="m-0 muted">{t('analytics.settingsHint')}</p>
          </fieldset>
          <Dialog.Close asChild>
            <Button>{t('theme.close')}</Button>
          </Dialog.Close>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

/** On a career's screens: the pronouns this browser shows for it (presentation only, content and experience §1). */
function CareerPronouns() {
  const { careerId } = useParams({ strict: false });
  return careerId === undefined ? null : <PronounChoice careerId={careerId} />;
}

function PronounChoice({ careerId }: { readonly careerId: string }) {
  const { t } = useTranslation();
  const current = useCareer(careerId).query.data?.pronouns;
  const setPronouns = useDisplayPronouns((state) => state.setPronouns);
  if (current === undefined) {
    return null;
  }

  return (
    <fieldset className="choices" aria-describedby="settings-pronouns-hint">
      <legend>{t('filters.pronounsLegend')}</legend>
      {Object.values(PronounSet).map((option) => (
        <label key={option} className="choice">
          <input
            type="radio"
            name="career-pronouns"
            value={option}
            checked={current === option}
            onChange={() => {
              setPronouns(careerId, option);
            }}
          />
          {t(`pronouns.${option}`)}
        </label>
      ))}
      <p id="settings-pronouns-hint" className="m-0 muted">
        {t('filters.pronounsHint')}
      </p>
    </fieldset>
  );
}
