import { render, screen, waitFor } from '@testing-library/react';
import { HttpResponse } from 'msw';
import { useTranslation } from 'react-i18next';
import { describe, expect, it } from 'vitest';

import {
  Category,
  type ChoiceView,
  MainAction,
  type MigrationEstimateView,
  type OfferView,
  type PlanOptionView,
  Platform,
  SponsorKind,
  WagerTier,
} from '@/api/generated/game.schemas';
import pl from '@/locales/pl/translation.json';

import { captureActions } from '../../../test/api';
import {
  actionOutcome,
  careerView,
  planNext,
  planOption,
  weekReport,
} from '../../../test/fixtures';
import { renderWithClient } from '../../../test/render';
import { reasonText } from './batch/reasons';
import { OffersCard } from './OffersCard';
import { migrationTargets, optionFor, wagerTiers } from './plans';
import { PlatformCard } from './PlatformCard';
import { lineLabel, statChangeText } from './reportLabels';
import { ChoiceOdds } from './stage/ChoiceOdds';
import { PlanWeek } from './stage/PlanWeek';
import { WeekReport } from './stage/WeekReport';

function offer(overrides: Partial<OfferView> = {}): OfferView {
  return {
    offerId: 'bank-skarbonka:1',
    templateId: 'bank-skarbonka',
    name: 'Bank Skarbonka',
    pitch: 'Bank z okrągłym logo chce jednego odcinka.',
    kind: SponsorKind.oneOff,
    paymentPln: 315,
    termWeeks: null,
    dueEveryWeeks: null,
    requiredCategory: null,
    platforms: [],
    prerequisites: { minFollowers: 1000, minCredibility: 45, maxDrama: 35, minDrama: null },
    incompatibleWith: ['adult-business'],
    breachCashPln: 150,
    breachCredibility: -2,
    expiresAfterWeek: 2,
    acceptBlockedReason: null,
    ...overrides,
  };
}

const estimate = (overrides: Partial<MigrationEstimateView> = {}): MigrationEstimateView => ({
  from: Platform.czatnia,
  to: Platform.nurt,
  executes: true,
  retentionMin: 0.65,
  retentionMax: 0.75,
  followersMin: 6500,
  followersMax: 7500,
  target: { reach: 0.85, subYield: 1.25, adYield: 0.7, policyRisk: 0.8 },
  ...overrides,
});

/** Gambling plans with the four wager variants (High needs more cash) and a migration plan toward Nurt. */
function economyPlans(): PlanOptionView[] {
  const plans = planNext().plans;
  const gamble = (wager: WagerTier | null, disabled: string | null) =>
    planOption(
      {
        mainAction: MainAction.noAction,
        category: Category.gambling,
        trainTarget: null,
        upgradeSlot: null,
        wager,
        targetPlatform: null,
      },
      disabled,
      null,
      {
        wager:
          wager === WagerTier.medium
            ? { tier: WagerTier.medium, stakePln: 500, chance: 0.45, payoutPln: 1000, lossPln: 500 }
            : null,
        policy: { exposure: wager === null ? 1 : 3, chance: 0.009, threshold: 1 },
        sponsorConsequences:
          wager === WagerTier.low
            ? [
                {
                  contractId: 'herbata-spokoj:4',
                  templateId: 'herbata-spokoj',
                  kind: 'missedInstallment',
                  penaltyPln: 200,
                  credibility: -3,
                  terminates: false,
                },
              ]
            : [],
      },
    );
  const migrate = planOption(
    {
      mainAction: MainAction.planPlatformMigration,
      category: Category.gambling,
      trainTarget: null,
      upgradeSlot: null,
      wager: null,
      targetPlatform: Platform.nurt,
    },
    null,
    null,
    { migration: estimate() },
  );
  return [
    ...plans.filter(
      (o) => !(o.plan.mainAction === MainAction.noAction && o.plan.category === Category.gambling),
    ),
    gamble(null, null),
    gamble(WagerTier.low, null),
    gamble(WagerTier.medium, null),
    gamble(WagerTier.high, 'needs-cash'),
    migrate,
  ];
}

const economyCareer = () =>
  careerView({
    rulesetId: 'm3-systems.2',
    next: { ...planNext(), plans: economyPlans() },
  });

describe('M3-B plan helpers', () => {
  it('finds the wager variant and the migration target the player chose', () => {
    const plans = economyPlans();
    expect(wagerTiers(plans, MainAction.noAction, Category.gambling)).toEqual([
      WagerTier.low,
      WagerTier.medium,
      WagerTier.high,
    ]);
    expect(migrationTargets(plans, Category.gambling)).toEqual([Platform.nurt]);
    expect(
      optionFor(plans, MainAction.noAction, Category.gambling, null, WagerTier.high)
        ?.disabledReason,
    ).toBe('needs-cash');
    expect(
      optionFor(
        plans,
        MainAction.planPlatformMigration,
        Category.gambling,
        null,
        null,
        Platform.nurt,
      )?.migration?.to,
    ).toBe(Platform.nurt);
  });
});

describe('sponsor offers', () => {
  it('accepts an offer with a fresh command', async () => {
    const received = captureActions(() =>
      HttpResponse.json(actionOutcome(careerView({ version: 2 }))),
    );
    const { user } = renderWithClient(
      <OffersCard career={careerView({ offers: [offer()] })} secret="s" />,
    );
    expect(screen.getByText('Bank Skarbonka · Jednorazowa akcja')).toBeVisible();
    expect(screen.getByText(/Warunki: min\. 1000 obserwujących/u)).toBeVisible();
    await user.click(screen.getByRole('button', { name: pl.offer.accept }));
    await waitFor(() => {
      expect(received).toHaveLength(1);
    });
    expect(received[0]?.body).toMatchObject({
      type: 'respondOffer',
      offerId: 'bank-skarbonka:1',
      accept: true,
      expectedVersion: 1,
    });
  });

  it('declines an offer', async () => {
    const received = captureActions(() =>
      HttpResponse.json(actionOutcome(careerView({ version: 2 }))),
    );
    const { user } = renderWithClient(
      <OffersCard career={careerView({ offers: [offer()] })} secret="s" />,
    );
    await user.click(screen.getByRole('button', { name: pl.offer.decline }));
    await waitFor(() => {
      expect(received[0]?.body).toMatchObject({ type: 'respondOffer', accept: false });
    });
  });

  it('keeps a blocked offer visible with its reason', () => {
    renderWithClient(
      <OffersCard
        career={careerView({ offers: [offer({ acceptBlockedReason: 'contract-limit' })] })}
        secret="s"
      />,
    );
    const accept = screen.getByRole('button', { name: pl.offer.accept });
    expect(accept).toBeDisabled();
    expect(accept).toHaveAccessibleDescription(pl.offer.blocked['contract-limit']);
  });

  it('maps a prerequisite reason code to its dash key', () => {
    renderWithClient(
      <OffersCard
        career={careerView({ offers: [offer({ acceptBlockedReason: 'prerequisite:followers' })] })}
        secret="s"
      />,
    );
    expect(screen.getByText(pl.offer.blocked['prerequisite-followers'])).toBeVisible();
    expect(screen.queryByText('prerequisite:followers')).toBeNull();
  });

  it('lists the deals with the one due this week', () => {
    renderWithClient(
      <OffersCard
        career={careerView({
          contracts: [
            {
              contractId: 'herbata-spokoj:4',
              templateId: 'herbata-spokoj',
              name: 'Herbata Spokój',
              kind: SponsorKind.contract,
              paymentPln: 400,
              requiredCategory: null,
              platforms: [],
              nextDueWeek: 7,
              termEndWeek: 15,
              missed: 1,
              breachCashPln: 200,
              breachCredibility: -3,
              dueThisWeek: true,
            },
          ],
        })}
        secret="s"
      />,
    );
    expect(screen.getByText(pl.offer.dueNow)).toBeVisible();
    expect(screen.getByText(/Opuszczone zlecenia: 1/u)).toBeVisible();
  });

  it('shows nothing without offers or deals', () => {
    renderWithClient(<OffersCard career={careerView()} secret="s" />);
    expect(screen.queryByRole('region', { name: pl.offer.title })).toBeNull();
  });
});

describe('plan risks', () => {
  it('shows stake, chance, payout and loss before a gambling week starts', async () => {
    const received = captureActions(() =>
      HttpResponse.json(actionOutcome(careerView({ version: 2 }))),
    );
    const { user } = renderWithClient(<PlanWeek career={economyCareer()} secret="s" />);
    await user.click(screen.getByRole('radio', { name: pl.action.noAction }));
    await user.click(screen.getByRole('radio', { name: pl.category.gambling }));
    expect(screen.getByRole('radio', { name: /Wysoki/u })).toBeDisabled();
    await user.click(screen.getByRole('radio', { name: pl.wager.medium }));
    expect(screen.getByText(/Stawka 500\s*zł, szansa 45\s*%/u)).toBeVisible();
    expect(screen.getByText(pl.wager.virtual)).toBeVisible();
    expect(screen.getByText(/Ryzyko ostrzeżenia od platformy/u)).toBeVisible();
    await user.click(screen.getByRole('button', { name: pl.plan.start }));
    await waitFor(() => {
      expect(received[0]?.body).toMatchObject({
        mainAction: 'noAction',
        category: 'gambling',
        wager: 'medium',
      });
    });
  });

  it('warns about the penalty of a missed installment', async () => {
    const { user } = renderWithClient(<PlanWeek career={economyCareer()} secret="s" />);
    await user.click(screen.getByRole('radio', { name: pl.action.noAction }));
    await user.click(screen.getByRole('radio', { name: pl.category.gambling }));
    await user.click(screen.getByRole('radio', { name: pl.wager.low }));
    expect(screen.getByText(/opuszczone zlecenie — kara 200\s*zł/u)).toBeVisible();
  });

  it('shows the migration range before the move and sends the target', async () => {
    const received = captureActions(() =>
      HttpResponse.json(actionOutcome(careerView({ version: 2 }))),
    );
    const { user } = renderWithClient(<PlanWeek career={economyCareer()} secret="s" />);
    await user.click(screen.getByRole('radio', { name: pl.category.gambling }));
    await user.click(screen.getByRole('radio', { name: pl.action.planPlatformMigration }));
    await user.click(screen.getByRole('radio', { name: 'Nurt' }));
    expect(screen.getByText(/zostanie 6500–7500 obserwujących \(65%–75%\)/u)).toBeVisible();
    await user.click(screen.getByRole('button', { name: pl.plan.start }));
    await waitFor(() => {
      expect(received[0]?.body).toMatchObject({
        mainAction: 'planPlatformMigration',
        targetPlatform: 'nurt',
      });
    });
  });
});

describe('platform and report', () => {
  it('shows the strikes and banned platforms', () => {
    render(
      <PlatformCard
        career={careerView({
          platform: Platform.pulsLive,
          platformSinceWeek: 40,
          strikes: [{ platform: Platform.pulsLive, givenWeek: 41, expiresAfterWeek: 66 }],
          bannedPlatforms: [Platform.czatnia],
        })}
      />,
    );
    expect(screen.getByText('Streamujesz na: PulsLive')).toBeVisible();
    expect(screen.getByText('PulsLive: ostrzeżenie do tygodnia 66')).toBeVisible();
    expect(screen.getByText(/Zbanowane platformy: Czatnia/u)).toBeVisible();
  });

  it('reports the exact followers after a migration, the wager and the strike roll', () => {
    render(
      <WeekReport
        report={weekReport(30, {
          migration: {
            from: Platform.czatnia,
            to: Platform.nurt,
            retention: 0.7012,
            followersBefore: 10_000,
            followersAfter: 7012,
          },
          wager: {
            tier: WagerTier.low,
            stakePln: 100,
            roll: 30,
            threshold: 45,
            won: true,
            netPln: 100,
          },
          strike: {
            platform: Platform.czatnia,
            exposure: 2,
            threshold: 1,
            roll: 1,
            strike: true,
            banned: true,
          },
        })}
      />,
    );
    expect(screen.getByText(/10\s000 → 7012 obserwujących/u)).toBeVisible();
    expect(screen.getByText(/Zakład wygrany: \+100\s*zł/u)).toBeVisible();
    expect(screen.getByText(/Kontrola regulaminu: rzut 1, próg 1 — ostrzeżenie/u)).toBeVisible();
    expect(screen.getByText(/Platforma Czatnia zbanowała kanał/u)).toBeVisible();
  });

  it('labels sponsor, wager and side-business lines and the migration share', () => {
    function Labels() {
      const { t } = useTranslation();
      return (
        <p>
          {[
            'income:sponsor',
            'cost:sponsor-penalty',
            'income:wager',
            'cost:wager',
            'income:side-business',
          ]
            .map((code) => lineLabel(t, code))
            .join('|')}
          {' / '}
          {statChangeText(t, {
            code: 'followers:migration',
            amountPln: null,
            delta: 0.7,
            detail: 'nurt',
          })}
          {' / '}
          {statChangeText(t, { code: 'stat:Drama', amountPln: null, delta: -0.2, detail: 'decay' })}
        </p>
      );
    }

    render(<Labels />);
    expect(
      screen.getByText(
        /Sponsor\|Kara od sponsora\|Zakład \(wygrana\)\|Zakład \(stawka\)\|Biznes poboczny \/ Obserwujący po przeprowadzce: 70% \/ Drama −0,2 \(drama przygasa\)/u,
      ),
    ).toBeVisible();
  });

  it('shows the forced move estimate of a ban choice', () => {
    const choice: ChoiceView = {
      choiceId: 'move-nurt',
      labelKey: null,
      label: 'Przenieś się na Nurt',
      guaranteed: true,
      probability: 1,
      threshold: 100,
      modifiers: [],
      allowsCritical: false,
      viralOnCritical: 0,
      unavailableReason: null,
      migration: estimate({
        retentionMin: 0.45,
        retentionMax: 0.55,
        followersMin: 4500,
        followersMax: 5500,
      }),
    };
    render(<ChoiceOdds choice={choice} />);
    expect(screen.getByText(/zostanie 4500–5500 obserwujących/u)).toBeVisible();
  });

  it('explains the M3-B batch reasons', () => {
    function Reasons() {
      const { t } = useTranslation();
      return (
        <p>
          {reasonText(t, {
            code: 'ban-move',
            args: { platform: 'pulsLive', retention: '0.550000' },
          })}{' '}
          {reasonText(t, {
            code: 'offer-declined',
            args: { templateId: 'zetonik-wirtualny', reason: 'gambling' },
          })}
        </p>
      );
    }

    render(<Reasons />);
    expect(
      screen.getByText(
        /Ban — przeprowadzka na PulsLive \(zostaje do 55%\)\. Oferta zetonik-wirtualny odrzucona: hazard na streamie\./u,
      ),
    ).toBeVisible();
  });
});
