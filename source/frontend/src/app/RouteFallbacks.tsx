import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

/** An address that matches no route. */
export function NotFound() {
  const { t } = useTranslation();
  return (
    <section className="panel grid gap-2">
      <h1 className="heading text-2xl">{t('fallback.notFound')}</h1>
      <p>
        <Link className="btn btn-primary" to="/">
          {t('fallback.home')}
        </Link>
      </p>
    </section>
  );
}

/** A screen that failed while rendering; a plain link reloads the app from the home screen. */
export function RouteError() {
  const { t } = useTranslation();
  return (
    <section className="panel grid gap-2">
      <h1 className="heading text-2xl">{t('fallback.error')}</h1>
      <p>{t('fallback.errorBody')}</p>
      <p>
        <a className="btn btn-primary" href="/">
          {t('fallback.home')}
        </a>
      </p>
    </section>
  );
}
