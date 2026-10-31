import { Dialog } from 'radix-ui';
import { useTranslation } from 'react-i18next';

import { useConsent } from '@/analytics/analytics';
import { Button } from '@/components/ui/Button';
import { themes } from '@/theme/theme';
import { useTheme } from '@/theme/useTheme';

export function SettingsDialog() {
  const { t } = useTranslation();
  const consent = useConsent((state) => state.consent);
  const setConsent = useConsent((state) => state.setConsent);
  const theme = useTheme((state) => state.theme);
  const setTheme = useTheme((state) => state.setTheme);
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
