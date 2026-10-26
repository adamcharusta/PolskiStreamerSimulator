import { useTranslation } from 'react-i18next';

import type { MigrationEstimateView, PlanOptionView } from '@/api/generated/game.schemas';
import { count, multiplier, percent, pln, signed, stat } from '@/format';

/** What the chosen plan risks, shown before the player commits: wager terms, policy exposure, a platform move and sponsor consequences. */
export function PlanRisks({ option }: { readonly option: PlanOptionView }) {
  const { t } = useTranslation();
  const { wager, policy, migration, sponsorConsequences } = option;
  const exposed = policy !== null && policy.exposure > 0;
  if (wager === null && !exposed && migration === null && sponsorConsequences.length === 0) {
    return null;
  }

  return (
    <section className="choice grid gap-1" aria-label={t('risk.title')}>
      <span className="font-bold">{t('risk.title')}</span>
      {wager !== null && (
        <>
          <span>
            {t('wager.terms', {
              stake: pln(wager.stakePln),
              chance: percent(wager.chance),
              payout: pln(wager.payoutPln),
              loss: pln(wager.lossPln),
            })}
          </span>
          <span className="muted">{t('wager.virtual')}</span>
        </>
      )}
      {exposed && (
        <span>
          {t('risk.policy', { chance: percent(policy.chance), exposure: policy.exposure })}
        </span>
      )}
      {migration !== null && <MigrationEstimate estimate={migration} />}
      {sponsorConsequences.map((consequence) => (
        <span key={consequence.contractId} className="bad">
          {t(`risk.${consequence.kind}`, {
            templateId: consequence.templateId,
            penalty: pln(consequence.penaltyPln),
            credibility: signed(consequence.credibility, stat),
          })}
          {consequence.terminates && ` — ${t('risk.terminates')}`}
        </span>
      ))}
    </section>
  );
}

/** A platform move: the announcement, or the retention and follower range with the target's factors. */
export function MigrationEstimate({ estimate }: { readonly estimate: MigrationEstimateView }) {
  const { t } = useTranslation();
  const platform = t(`platform.${estimate.to}`);
  return (
    <>
      <span>
        {estimate.executes
          ? t('risk.move', {
              platform,
              min: count(estimate.followersMin),
              max: count(estimate.followersMax),
              retentionMin: percent(estimate.retentionMin),
              retentionMax: percent(estimate.retentionMax),
            })
          : t('risk.announce', { platform })}
      </span>
      <span className="muted">
        {t('platform.factors', {
          reach: multiplier(estimate.target.reach),
          subYield: multiplier(estimate.target.subYield),
          adYield: multiplier(estimate.target.adYield),
          policyRisk: multiplier(estimate.target.policyRisk),
        })}
      </span>
    </>
  );
}
