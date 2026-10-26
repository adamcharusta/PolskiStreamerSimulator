import { useTranslation } from 'react-i18next';

import {
  ActionType,
  type CareerView,
  type ContractView,
  type OfferView,
} from '@/api/generated/game.schemas';
import { Button } from '@/components/ui/Button';
import { formatContent } from '@/content/icu';
import { count, pln, signed, stat } from '@/format';

import { blockedKey } from './offers';
import { RetryBanner } from './RetryBanner';
import { useActionCommand } from './useCareerCommand';

export interface OffersCardProps {
  readonly career: CareerView;
  readonly secret: string;
}

/** The sponsor offers with every term and an accept and decline button, then the active deals (M3-B). */
export function OffersCard({ career, secret }: OffersCardProps) {
  const { t } = useTranslation();
  const command = useActionCommand(career.careerId, secret);
  const locked = command.pending || command.stalled;
  if (career.offers.length === 0 && career.contracts.length === 0) {
    return null;
  }

  const respond = (offer: OfferView, accept: boolean) => {
    command.run({
      expectedVersion: career.version,
      type: ActionType.respondOffer,
      offerId: offer.offerId,
      accept,
    });
  };

  return (
    <section className="panel grid gap-2" aria-label={t('offer.title')}>
      <h2 className="panel-title">{t('offer.title')}</h2>
      <RetryBanner command={command} />
      {career.offers.length > 0 && (
        <>
          <h3 className="font-bold">{t('offer.offers')}</h3>
          <ul className="grid gap-2" aria-label={t('offer.offers')}>
            {career.offers.map((offer) => (
              <li key={offer.offerId} className="choice grid gap-1">
                <OfferTerms career={career} offer={offer} />
                <div className="flex flex-wrap gap-2">
                  <Button
                    variant="primary"
                    disabled={locked || offer.acceptBlockedReason !== null}
                    aria-describedby={
                      offer.acceptBlockedReason === null
                        ? undefined
                        : `offer-blocked-${offer.offerId}`
                    }
                    onClick={() => {
                      respond(offer, true);
                    }}
                  >
                    {t('offer.accept')}
                  </Button>
                  <Button
                    disabled={locked}
                    onClick={() => {
                      respond(offer, false);
                    }}
                  >
                    {t('offer.decline')}
                  </Button>
                </div>
                {offer.acceptBlockedReason !== null && (
                  <span id={`offer-blocked-${offer.offerId}`} className="muted">
                    {t(blockedKey(offer.acceptBlockedReason), {
                      defaultValue: t('offer.blocked.unknown'),
                    })}
                  </span>
                )}
              </li>
            ))}
          </ul>
        </>
      )}
      {career.contracts.length > 0 && (
        <>
          <h3 className="font-bold">{t('offer.contracts')}</h3>
          <ul aria-label={t('offer.contracts')}>
            {career.contracts.map((deal) => (
              <ContractLine key={deal.contractId} deal={deal} />
            ))}
          </ul>
        </>
      )}
    </section>
  );
}

function OfferTerms({ career, offer }: { readonly career: CareerView; readonly offer: OfferView }) {
  const { t } = useTranslation();
  const { prerequisites } = offer;
  const conditions = [
    prerequisites.minFollowers > 0 &&
      t('offer.minFollowers', { value: count(prerequisites.minFollowers) }),
    prerequisites.minCredibility !== null &&
      t('offer.minCredibility', { value: stat(prerequisites.minCredibility) }),
    prerequisites.maxDrama !== null && t('offer.maxDrama', { value: stat(prerequisites.maxDrama) }),
    prerequisites.minDrama !== null && t('offer.minDrama', { value: stat(prerequisites.minDrama) }),
  ].filter((text) => text !== false);
  return (
    <>
      <span className="font-bold">
        {formatContent(offer.name, career.pronouns)} · {t(`offer.kind.${offer.kind}`)}
      </span>
      <span>{formatContent(offer.pitch, career.pronouns)}</span>
      <span>{t('offer.payment', { amount: pln(offer.paymentPln) })}</span>
      {offer.termWeeks !== null && offer.dueEveryWeeks !== null && (
        <span>{t('offer.term', { weeks: offer.termWeeks, every: offer.dueEveryWeeks })}</span>
      )}
      {offer.requiredCategory !== null && (
        <span>{t('offer.category', { category: t(`category.${offer.requiredCategory}`) })}</span>
      )}
      {offer.platforms.length > 0 && (
        <span>
          {t('offer.platforms', {
            platforms: offer.platforms.map((platform) => t(`platform.${platform}`)).join(', '),
          })}
        </span>
      )}
      {conditions.length > 0 && (
        <span className="muted">{t('offer.prerequisites', { list: conditions.join(', ') })}</span>
      )}
      {offer.incompatibleWith.length > 0 && (
        <span className="muted">
          {t('offer.incompatible', {
            list: offer.incompatibleWith
              .map((activity) => t(`offer.incompatibility.${activity}`, { defaultValue: activity }))
              .join(', '),
          })}
        </span>
      )}
      <span className="muted">
        {t('offer.breach', {
          cash: pln(offer.breachCashPln),
          credibility: signed(offer.breachCredibility, stat),
        })}
      </span>
      <span className="muted">{t('offer.expires', { week: offer.expiresAfterWeek })}</span>
    </>
  );
}

function ContractLine({ deal }: { readonly deal: ContractView }) {
  const { t } = useTranslation();
  return (
    <li>
      <span className="font-bold">{deal.name}</span> · {t(`offer.kind.${deal.kind}`)} ·{' '}
      {t('offer.payment', { amount: pln(deal.paymentPln) })} ·{' '}
      {deal.dueThisWeek ? (
        <strong>{t('offer.dueNow')}</strong>
      ) : (
        t('offer.nextDue', { week: deal.nextDueWeek })
      )}
      {deal.missed > 0 && ` · ${t('offer.missed', { count: deal.missed })}`}
    </li>
  );
}
