import {
  type AutoWeekView,
  Background,
  type BatchOutcome,
  BatchStopReason,
  type CareerOptionsView,
  type CareerStatsView,
  type CareerView,
  Category,
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

/** Every plan of a week, like the API lists them; `disabled` decides each option's reason. */
export function planNext(disabled: (plan: PlanView) => string | null = () => null): NextStepView {
  const plans: PlanOptionView[] = [];
  for (const category of Object.values(Category)) {
    for (const mainAction of Object.values(MainAction)) {
      const slots =
        mainAction === MainAction.improveProduction ? Object.values(EquipmentSlot) : [null];
      for (const upgradeSlot of slots) {
        const plan: PlanView = { mainAction, category, trainTarget: null, upgradeSlot };
        plans.push({ plan, disabledReason: disabled(plan) });
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
      textPl: 'Inny streamer kończy transmisję i wysyła do ciebie cały swój czat.',
      choices: [
        {
          choiceId: 'welcome',
          labelKey: 'event.chat-raid.welcome',
          labelPl: 'Przywitaj gości i odpal dla nich szybkie show',
          guaranteed: false,
          probability: 0.58,
          threshold: 58,
          modifiers: [{ labelKey: 'modifier.credibility', value: 0.03 }],
          allowsCritical: true,
        },
        {
          choiceId: 'ignore',
          labelKey: 'event.chat-raid.ignore',
          labelPl: 'Graj dalej, jakby nic się nie stało',
          guaranteed: true,
          probability: 1,
          threshold: 100,
          modifiers: [],
          allowsCritical: false,
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
    ...overrides,
  };
}

export function careerView(overrides: Partial<CareerView> = {}): CareerView {
  return {
    careerId: TEST_CAREER_ID,
    nickname: 'Streamer',
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
