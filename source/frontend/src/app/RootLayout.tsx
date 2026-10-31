import { Link, Outlet } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { ConsentPanel } from '@/analytics/ConsentPanel';
import { SettingsDialog } from '@/features/settings/SettingsDialog';

import { ConnectionBanner } from './ConnectionBanner';

export function RootLayout() {
  const { t } = useTranslation();
  return (
    <div className="shell">
      <a className="sr-only" href="#main">
        {t('app.skip')}
      </a>
      <header className="flex flex-wrap items-center justify-between gap-2 py-2">
        <Link to="/" className="heading text-xl text-inherit no-underline">
          {t('app.title')}
        </Link>
        <SettingsDialog />
      </header>
      <ConnectionBanner />
      <ConsentPanel />
      <main id="main">
        <Outlet />
      </main>
      <div className="py-3 text-center">
        <span className="visitor-counter" aria-hidden="true">
          {t('decor.visitors')}
        </span>
      </div>
    </div>
  );
}
