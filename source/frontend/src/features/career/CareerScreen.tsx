import { Link, useNavigate, useParams } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { forgetCareer } from '@/careers/knownCareers';
import { Button } from '@/components/ui/Button';

import { CareerDesk } from './CareerDesk';
import { useCareer } from './useCareer';

export function CareerScreen() {
  const { careerId } = useParams({ from: '/kariera/$careerId' });
  const { t } = useTranslation();
  const { secret, query } = useCareer(careerId);

  if (secret === null || query.error?.code === 'career-not-found') {
    return <CareerNotFound careerId={careerId} />;
  }

  if (query.data !== undefined) {
    return <CareerDesk career={query.data} secret={secret} />;
  }

  if (query.isError) {
    return (
      <div role="alert" className="panel">
        <p>{t('career.loadFailed')}</p>
        <Button
          onClick={() => {
            void query.refetch();
          }}
        >
          {t('career.reload')}
        </Button>
      </div>
    );
  }

  return <output className="block">{t('career.loading')}</output>;
}

function CareerNotFound({ careerId }: { readonly careerId: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  return (
    <div className="panel grid gap-2">
      <h1 className="heading text-2xl">{t('career.notFound')}</h1>
      <p>{t('career.notFoundBody')}</p>
      <div className="flex flex-wrap gap-2">
        <Button
          onClick={() => {
            forgetCareer(careerId);
            void navigate({ to: '/' });
          }}
        >
          {t('career.forget')}
        </Button>
        <Link className="btn btn-secondary" to="/">
          {t('career.backHome')}
        </Link>
      </div>
    </div>
  );
}
