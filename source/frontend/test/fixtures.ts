import {
  type AutoWeekView,
  Background,
  type BatchOutcome,
  BatchStopReason,
  type CareerOptionsView,
  type CareerStatsView,
  type CareerView,
  Category,
  type ChoiceView,
  Difficulty,
  EquipmentSlot,
  MainAction,
  NextStepKind,
  type NextStepView,
  type PlanOptionView,
  type PlanView,
  PronounSet,
  type RollView,
  type WeekReportView,
} from '@/api/generated/game.schemas';

import { TEST_CAREER_ID } from './render';

const skill = (strong: keyof typeof Category, value = 18) => ({
  justChatting: 10,
  fps: 10,
  moba: 10,
  chess: 10,
  gambling: 10,
  [Category[strong]]: value,
});
const tiers = (tier: number) => ({
  microphone: tier,
  camera: tier,
  computer: tier,
  connection: tier,
});

export function careerOptions(): CareerOptionsView {
  return {
    backgrounds: [
      {
        background: Background.dormRoomNewcomer,
        cashPln: 2000,
        followers: 0,
        drama: 0,
        credibility: 50,
        energy: 70,
        skill: skill('fps'),
        equipment: tiers(0),
        scoreMultiplier: 1,
      },
      {
        background: Background.localMemeCreator,
        cashPln: 1000,
        followers: 600,
        drama: 20,
        credibility: 42,
        energy: 70,
        skill: skill('justChatting'),
        equipment: tiers(1),
        scoreMultiplier: 0.96,
      },
      {
        background: Background.chessClubRegular,
        cashPln: 500,
        followers: 0,
        drama: 0,
        credibility: 58,
        energy: 70,
        skill: skill('chess', 25),
        equipment: tiers(1),
        scoreMultiplier: 0.98,
      },
      {
        background: Background.industryInsider,
        cashPln: 5000,
        followers: 1500,
        drama: 15,
        credibility: 50,
        energy: 70,
        skill: skill('fps', 10),
        equipment: tiers(2),
        scoreMultiplier: 0.9,
      },
    ],
    difficulties: [
      { difficulty: Difficulty.easy, scoreMultiplier: 1 },
      { difficulty: Difficulty.normal, scoreMultiplier: 1.1 },
    ],
    pronouns: [PronounSet.woman, PronounSet.man, PronounSet.nonbinary],
  };
}

export function stats(overrides: Partial<CareerStatsView> = {}): CareerStatsView {
  return {
    cashPln: 2000,
    followers: 0,
    lastLiveViewers: 0,
    drama: 0,
    credibility: 50,
    energy: 70,
    burnout: 0,
    skill: { justChatting: 10, fps: 18, moba: 10, chess: 10, gambling: 10 },
    boredom: { justChatting: 0, fps: 0, moba: 0, chess: 0, gambling: 0 },
    equipment: { microphone: 0, camera: 0, computer: 0, connection: 0 },
    ...overrides,
  };
}

/**
 * Every plan of a week, like the API lists them; `disabled` decides each option's reason. Networking is offered
 * only with its odds (an M3 career); an M1 career's plans leave it out.
 */
export function planNext(
  disabled: (plan: PlanView) => string | null = () => null,
  network: ChoiceView | null = null,
): NextStepView {
  const plans: PlanOptionView[] = [];
  const actions = Object.values(MainAction).filter(
    (action) => network !== null || action !== MainAction.networkCollaborate,
  );
  for (const category of Object.values(Category)) {
    for (const mainAction of actions) {
      const slots =
        mainAction === MainAction.improveProduction ? Object.values(EquipmentSlot) : [null];
      for (const upgradeSlot of slots) {
        const plan: PlanView = { mainAction, category, trainTarget: null, upgradeSlot };
        const odds = mainAction === MainAction.networkCollaborate ? network : null;
        plans.push({ plan, disabledReason: disabled(plan), odds });
      }
    }
  }

  return { kind: NextStepKind.planWeek, plans, event: null };
}

export function eventNext(): NextStepView {
  return {
    kind: NextStepKind.resolveEvent,
    plans: [],
    event: {
      eventId: 'chat-raid',
      titleKey: 'event.chat-raid.title',
      title: null,
      text: 'Inny streamer kończy transmisję i wysyła do ciebie cały swój czat.',
      mandatory: false,
      choices: [
        {
          choiceId: 'welcome',
          labelKey: 'event.chat-raid.welcome',
          label: 'Przywitaj gości i odpal dla nich szybkie show',
          guaranteed: false,
          probability: 0.58,
          threshold: 58,
          modifiers: [{ labelKey: 'modifier.credibility', value: 0.03 }],
          allowsCritical: true,
          viralOnCritical: 0,
          unavailableReason: null,
        },
        {
          choiceId: 'ignore',
          labelKey: 'event.chat-raid.ignore',
          label: 'Graj dalej, jakby nic się nie stało',
          guaranteed: true,
          probability: 1,
          threshold: 100,
          modifiers: [],
          allowsCritical: false,
          viralOnCritical: 0,
          unavailableReason: null,
        },
      ],
    },
  };
}

/** The odds of the networking roll, as an M3 career's plans carry them. */
export function networkOdds(overrides: Partial<ChoiceView> = {}): ChoiceView {
  return {
    choiceId: 'collaborate',
    labelKey: null,
    label: 'Zagadaj do innych twórców',
    guaranteed: false,
    probability: 0.41,
    threshold: 41,
    modifiers: [
      { labelKey: 'modifier.credibility', value: 0.09 },
      { labelKey: 'modifier.drama', value: -0.03 },
    ],
    allowsCritical: true,
    viralOnCritical: 0,
    unavailableReason: null,
    ...overrides,
  };
}

/** An M3 mandatory decision between weeks: ICU title, text and labels formatted by the client. */
export function decisionNext(): NextStepView {
  return {
    kind: NextStepKind.resolveDecision,
    plans: [],
    event: {
      eventId: 'sponsor-offer',
      titleKey: null,
      title: '{gender, select, female {Sponsorka} male {Sponsor} other {Sponsor_ka}} pisze',
      text: '{gender, select, female {Dostałaś} male {Dostałeś} other {Dostałoś}} ofertę współpracy.',
      mandatory: true,
      choices: [
        {
          choiceId: 'invest',
          labelKey: null,
          label: 'Kup lepszy sprzęt na start',
          guaranteed: false,
          probability: 0.6,
          threshold: 60,
          modifiers: [],
          allowsCritical: true,
          viralOnCritical: 0.05,
          unavailableReason: 'needs-cash',
        },
        {
          choiceId: 'sign',
          labelKey: null,
          label:
            '{gender, select, female {Podpisz jako twórczyni} male {Podpisz jako twórca} other {Podpisz jako osoba twórcza}}',
          guaranteed: false,
          probability: 0.5,
          threshold: 50,
          modifiers: [],
          allowsCritical: true,
          viralOnCritical: 0.05,
          unavailableReason: null,
        },
        {
          choiceId: 'refuse',
          labelKey: null,
          label: 'Odmów',
          guaranteed: true,
          probability: 1,
          threshold: 100,
          modifiers: [],
          allowsCritical: false,
          viralOnCritical: 0,
          unavailableReason: null,
        },
      ],
    },
  };
}

export const settleNext = (): NextStepView => ({
  kind: NextStepKind.settleWeek,
  plans: [],
  event: null,
});

export const endedNext = (): NextStepView => ({ kind: NextStepKind.ended, plans: [], event: null });

export function roll(overrides: Partial<RollView> = {}): RollView {
  return {
    eventId: 'chat-raid',
    choiceId: 'welcome',
    roll: 37,
    threshold: 58,
    modifiers: [],
    outcome: 'success',
    ...overrides,
  };
}

export function weekReport(week = 1, overrides: Partial<WeekReportView> = {}): WeekReportView {
  return {
    week,
    plan: {
      mainAction: MainAction.rest,
      category: Category.fps,
      trainTarget: null,
      upgradeSlot: null,
    },
    rolls: [roll()],
    liveViewers: 115,
    followersGained: 8,
    followersLost: 2,
    followersNet: 6,
    income: [
      { code: 'income:subscriptions', amountPln: 23, delta: null, detail: null },
      { code: 'income:donations', amountPln: 17, delta: null, detail: null },
    ],
    costs: [{ code: 'cost:living', amountPln: 100, delta: null, detail: null }],
    cashDelta: -60,
    statChanges: [
      { code: 'stat:Energy', amountPln: null, delta: 25, detail: 'action' },
      { code: 'stat:Skill.Fps', amountPln: null, delta: 0.82, detail: 'practice' },
      { code: 'stat:Credibility', amountPln: null, delta: 2, detail: 'chat-raid' },
    ],
    ending: null,
    decisionRolls: [],
    actionRoll: null,
    viral: null,
    ...overrides,
  };
}

export function careerView(overrides: Partial<CareerView> = {}): CareerView {
  return {
    careerId: TEST_CAREER_ID,
    nickname: 'Streamer',
    rulesetId: 'm1-prototype.1',
    version: 1,
    status: 'active',
    ending: null,
    background: Background.dormRoomNewcomer,
    difficulty: Difficulty.normal,
    pronouns: PronounSet.woman,
    week: 1,
    lastSettledWeek: 0,
    stats: stats(),
    next: planNext(),
    lastReport: null,
    ...overrides,
  };
}

export function actionOutcome(
  career: CareerView,
  extras: { roll?: RollView; report?: WeekReportView } = {},
) {
  return { career, roll: extras.roll ?? null, report: extras.report ?? null };
}

export function batchOutcome(
  stopReason: BatchStopReason = BatchStopReason.completed,
  weeks = 2,
): BatchOutcome {
  const ledger: AutoWeekView[] = Array.from({ length: weeks }, (_, index) => ({
    week: index + 2,
    plan: {
      mainAction: MainAction.rest,
      category: Category.fps,
      trainTarget: null,
      upgradeSlot: null,
    },
    planReason: {
      code: 'rest-low-energy',
      args: { energy: '20.000000', threshold: '30', category: 'fps', categoryRule: 'bonus' },
    },
    choices: [
      {
        eventId: 'chat-raid',
        choiceId: 'welcome',
        reason: {
          code: 'gamble-best-odds',
          args: { probability: '0.600000', threshold: '0.500000' },
        },
        roll: roll(),
      },
    ],
    report: weekReport(index + 2),
  }));
  return {
    weeksCommitted: weeks,
    stopReason,
    weeks: ledger,
    summary: {
      weeksCommitted: weeks,
      followersNet: 12,
      cashDelta: -120,
      incomePln: 80,
      costsPln: 200,
      start: stats(),
      end: stats({ cashPln: 1880, followers: 12 }),
    },
    career: careerView({ version: 30, week: 2 + weeks, lastSettledWeek: 1 + weeks }),
  };
}
