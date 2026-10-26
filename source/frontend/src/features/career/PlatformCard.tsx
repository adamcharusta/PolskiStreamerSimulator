import { useTranslation } from 'react-i18next';

import type { CareerView } from '@/api/generated/game.schemas';

/** The current platform, the weeks on it, the active policy strikes with their last week and the banned platforms (M3-B). */
export function PlatformCard({ career }: { readonly career: CareerView }) {
  const { t } = useTranslation();
  const title = t('platform.title');
  return (
    <section className="panel grid gap-1" aria-label={title}>
      <h2 className="panel-title">{title}</h2>
      <p className="m-0 font-bold">
        {t('platform.current', { platform: t(`platform.${career.platform}`) })}
      </p>
      <p className="muted m-0">{t('platform.since', { week: career.platformSinceWeek })}</p>
      <h3 className="font-bold">{t('platform.strikes')}</h3>
      {career.strikes.length === 0 ? (
        <p className="m-0">{t('platform.noStrikes')}</p>
      ) : (
        <ul aria-label={t('platform.strikes')}>
          {career.strikes.map((strike, index) => (
            <li key={`${strike.platform}-${String(strike.givenWeek)}-${String(index)}`}>
              {t('platform.strike', {
                platform: t(`platform.${strike.platform}`),
                week: strike.expiresAfterWeek,
              })}
            </li>
          ))}
        </ul>
      )}
      {career.bannedPlatforms.length > 0 && (
        <p className="bad m-0">
          {t('platform.banned')}:{' '}
          {career.bannedPlatforms.map((platform) => t(`platform.${platform}`)).join(', ')}
        </p>
      )}
    </section>
  );
}
