import latinRegular from '@fontsource/comic-relief/files/comic-relief-latin-400-normal.woff?inline';
import latinBold from '@fontsource/comic-relief/files/comic-relief-latin-700-normal.woff?inline';
import extRegular from '@fontsource/comic-relief/files/comic-relief-latin-ext-400-normal.woff?inline';
import extBold from '@fontsource/comic-relief/files/comic-relief-latin-ext-700-normal.woff?inline';
import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { PDFDocument } from 'pdf-lib';
import { describe, expect, it, vi } from 'vitest';

import {
  Category,
  MainAction,
  SponsorKind,
  type WeekSummaryView,
} from '@/api/generated/game.schemas';
import { ageAt, nextMilestone } from '@/features/career/dashboard';
import { buildDiploma, fontkitOf, runs } from '@/features/diploma/diploma';
import pl from '@/locales/pl/translation.json';

import { serveCareer } from '../../../test/api';
import { careerView, weekReport } from '../../../test/fixtures';
import { rememberTestCareer, renderApp } from '../../../test/render';
import { server } from '../../../test/server';
import { filterWeeks, yearlyReviews } from './yearly';

const week = (n: number, overrides: Partial<WeekSummaryView> = {}): WeekSummaryView => ({
  week: n,
  plan: {
    mainAction: MainAction.noAction,
    category: Category.fps,
    trainTarget: null,
    upgradeSlot: null,
    wager: null,
    targetPlatform: null,
  },
  liveViewers: 100,
  followersNet: n,
  cashDelta: -10,
  automatic: n > 1,
  tags: [],
  ...overrides,
});

/** A data URL from Vite's `?inline` import as bytes. */
function font(dataUrl: string): Uint8Array {
  return Uint8Array.from(
    atob(dataUrl.slice(dataUrl.indexOf(',') + 1)),
    (c) => c.codePointAt(0) ?? 0,
  );
}

const describeWeek = (w: WeekSummaryView) => `${w.plan.category} ${w.tags.join(' ')}`;

describe('dashboard helpers', () => {
  it('counts the age from 18 and the next follower milestone', () => {
    expect([ageAt(1), ageAt(52), ageAt(53), ageAt(1040)]).toEqual([18, 18, 19, 37]);
    expect([nextMilestone(0), nextMilestone(1000), nextMilestone(200_000_000)]).toEqual([
      1000,
      10_000,
      null,
    ]);
  });
});

describe('yearly review and filters', () => {
  const weeks = [
    week(1, { tags: ['viral'] }),
    week(52, { plan: { ...week(1).plan, category: Category.chess } }),
    week(53, { tags: ['ban'] }),
  ];

  it('sums each 52-week year', () => {
    const [first, second] = yearlyReviews(weeks);
    expect(first).toMatchObject({
      year: 1,
      weeks: 2,
      followersNet: 53,
      cashDelta: -20,
      incidents: { viral: 1 },
      bestWeek: 52,
    });
    expect(second).toMatchObject({ year: 2, fromWeek: 53, toWeek: 104, incidents: { ban: 1 } });
  });

  it('filters by range, tag and text', () => {
    expect(
      filterWeeks(weeks, { fromWeek: 2, toWeek: null, tag: null, text: '' }, describeWeek).map(
        (w) => w.week,
      ),
    ).toEqual([52, 53]);
    expect(
      filterWeeks(weeks, { fromWeek: null, toWeek: null, tag: 'ban', text: '' }, describeWeek).map(
        (w) => w.week,
      ),
    ).toEqual([53]);
    expect(
      filterWeeks(
        weeks,
        { fromWeek: null, toWeek: null, tag: null, text: 'CHESS' },
        describeWeek,
      ).map((w) => w.week),
    ).toEqual([52]);
  });
});

describe('dashboard', () => {
  it('shows week, age, platform, obligations and the next milestone with a history link', async () => {
    const career = rememberTestCareer();
    serveCareer(
      careerView({
        week: 60,
        stats: { ...careerView().stats, followers: 4000 },
        contracts: [
          {
            contractId: 'herbata-spokoj:4',
            templateId: 'herbata-spokoj',
            name: 'Herbata Spokój',
            kind: SponsorKind.contract,
            paymentPln: 400,
            requiredCategory: null,
            platforms: [],
            nextDueWeek: 60,
            termEndWeek: 70,
            missed: 0,
            breachCashPln: 200,
            breachCredibility: -3,
            dueThisWeek: true,
          },
        ],
      }),
    );
    renderApp(`/kariera/${career.careerId}`);
    const card = await screen.findByRole('region', { name: pl.dashboard.title });
    expect(card).toHaveTextContent('Tydzień 60 z 1040 · 19 lat · Czatnia');
    expect(card).toHaveTextContent('W tym tygodniu: Herbata Spokój');
    expect(card).toHaveTextContent('Do 10 000 obserwujących brakuje 6000');
    expect(screen.getByRole('link', { name: pl.dashboard.history })).toHaveAttribute(
      'href',
      `/kariera/${career.careerId}/historia`,
    );
  });
});

describe('history screen', () => {
  it('lists the weeks, the yearly review and opens a stored report', async () => {
    const career = rememberTestCareer();
    server.use(
      http.get('/v1/careers/:id', () => HttpResponse.json(careerView())),
      http.get('/v1/careers/:id/history', () =>
        HttpResponse.json({ weeks: [week(1, { tags: ['decision'] }), week(2)] }),
      ),
      http.get('/v1/careers/:id/history/:week', () => HttpResponse.json(weekReport(1))),
    );
    const { user } = renderApp(`/kariera/${career.careerId}/historia`);
    expect(await screen.findByRole('heading', { name: pl.history.title, level: 1 })).toBeVisible();
    expect(screen.getByRole('article', { name: 'Rok 1' })).toHaveTextContent('2 tygodni');
    expect(screen.getByText('2 tygodnie')).toBeVisible();
    await user.selectOptions(screen.getByLabelText(pl.history.kind), 'decision');
    expect(screen.getByText('1 tydzień')).toBeVisible();
    await user.click(screen.getByRole('button', { name: pl.history.show }));
    expect(await screen.findByRole('heading', { name: 'Raport — tydzień 1' })).toBeVisible();
  });

  it('explains a failed load', async () => {
    const career = rememberTestCareer();
    server.use(
      http.get('/v1/careers/:id/history', () =>
        HttpResponse.json({ code: 'internal-error' }, { status: 500 }),
      ),
    );
    renderApp(`/kariera/${career.careerId}/historia`);
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(pl.history.loadFailed);
    });
  });
});

describe('diploma', () => {
  it('takes fontkit from the default export of the browser build', () => {
    const kit = { create: vi.fn() };
    expect(fontkitOf({ default: kit })).toBe(kit);
    expect(fontkitOf(kit as never)).toBe(kit);
  });

  it('splits text into Latin and Latin Extended runs', () => {
    expect(runs('Żółw ok', new Set(Array.from('w ok', (c) => c.codePointAt(0) ?? 0)))).toEqual([
      { text: 'Żół', ext: true },
      { text: 'w ok', ext: false },
    ]);
  });

  it('builds a one-page PDF with Polish letters', async () => {
    const pdf = await buildDiploma(
      {
        title: 'Dyplom streamera',
        nickname: 'Zażółć gęślą jaźń',
        subtitle: 'Świeżak z akademika · poziom: Normalny',
        lines: ['Tygodnie na wizji: 1040', 'Kariera ukończona'],
        score: 'Wynik: 91 850',
        achievements: `Osiągnięcia: ${'Nowy dom, '.repeat(20)}koniec`,
        footer: 'Polski Streamer Simulator · fikcyjna kariera · 26.10.2026',
      },
      {
        regular: {
          latin: font(latinRegular),
          latinExt: font(extRegular),
        },
        bold: {
          latin: font(latinBold),
          latinExt: font(extBold),
        },
      },
    );
    const doc = await PDFDocument.load(pdf);
    expect(doc.getPageCount()).toBe(1);
    expect(doc.getTitle()).toBe('Dyplom streamera — Zażółć gęślą jaźń');
  });
});
