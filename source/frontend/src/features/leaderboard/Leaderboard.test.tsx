import { screen, waitFor, within } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';

import type {
  EntryStatusView,
  LeaderboardView,
  PublishedEntryView,
  ScoreView,
  SeasonView,
} from '@/api/generated/game.schemas';

import { capturePublications, problem, serveCareer, serveEntryStatus } from '../../../test/api';
import { careerView, endedNext } from '../../../test/fixtures';
import { rememberTestCareer, renderApp, TEST_CAREER_ID } from '../../../test/render';
import { server } from '../../../test/server';
import { leaderboardNickname } from './nickname';

const score: ScoreView = {
  followers: 0.5,
  wealth: 0.5,
  achievements: 0,
  followersPoints: 22_500,
  wealthPoints: 22_500,
  achievementPoints: 0,
  baseScore: 45_000,
  difficultyMultiplier: 1,
  backgroundMultiplier: 1,
  finalScore: 45_000,
};

const ended = careerView({
  status: 'ended',
  ending: 'earlyRetirement',
  next: endedNext(),
  score,
  ranked: true,
  season: 2,
});

const open: EntryStatusView = { status: 'open', season: 2, submissionDeadline: null, entry: null };

const publishedEntry: PublishedEntryView = {
  nickname: 'ŻABA',
  score: 45_000,
  weeksPlayed: 12,
  submittedAt: '2026-11-01T10:00:00Z',
  rank: 7,
};

const published: EntryStatusView = {
  status: 'published',
  season: 2,
  submissionDeadline: null,
  entry: publishedEntry,
};

function season(overrides: Partial<SeasonView> = {}): SeasonView {
  return {
    number: 2,
    openedAt: '2026-10-01T00:00:00Z',
    closedAt: null,
    submissionDeadline: null,
    extended: false,
    current: true,
    ...overrides,
  };
}

function serveBoard(board: LeaderboardView, seasons: SeasonView[] = [board.season]) {
  const asked: (string | null)[] = [];
  server.use(
    http.get('/v1/leaderboard', ({ request }) => {
      const requested = new URL(request.url).searchParams.get('season');
      asked.push(requested);
      return requested === '9'
        ? problem(404, 'season-not-found')
        : HttpResponse.json({
            ...board,
            season: seasons.find((s) => String(s.number) === requested) ?? board.season,
          });
    }),
    http.get('/v1/seasons', () => HttpResponse.json(seasons)),
  );
  return asked;
}

describe('leaderboard nickname', () => {
  it.each([
    ['ab1', 'AB1'],
    [' żaba ', 'ŻABA'],
    ['Z', 'Z'],
  ])('accepts %j as %j', (input, stored) => {
    expect(leaderboardNickname(input)).toBe(stored);
  });

  it.each(['', 'SIXSIX', 'a b', '<b>', 'É', 'a_1'])('rejects %j', (input) => {
    expect(leaderboardNickname(input)).toBeNull();
  });
});

describe('ending ranking panel', () => {
  it('publishes the result under an uppercased nickname and shows its rank', async () => {
    rememberTestCareer();
    serveCareer(ended);
    serveEntryStatus(open);
    const received = capturePublications(() => HttpResponse.json(published));
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}`);

    const panel = await screen.findByRole('region', { name: 'Ranking' });
    expect(
      await within(panel).findByText(/Dodaj wynik 45\s000 pkt do tabeli sezonu 2/u),
    ).toBeInTheDocument();
    await user.type(within(panel).getByLabelText('Nick w rankingu'), 'żaba');
    await user.click(within(panel).getByRole('button', { name: 'Publikuję wynik' }));

    expect(
      await within(panel).findByText(/ŻABA — 45\s000 pkt, 7\. miejsce w sezonie 2/u),
    ).toBeInTheDocument();
    expect(received.map((r) => r.body)).toEqual([{ nickname: 'ŻABA' }]);
    expect(received[0]?.authorization).toBe('Bearer secret-1');
    expect(within(panel).getByRole('link', { name: 'Zobacz ranking sezonu' })).toHaveAttribute(
      'href',
      '/ranking?sezon=2',
    );
    expect(screen.getAllByText('Rankingowa · Sezon 2').length).toBeGreaterThan(0);
  });

  it('rejects an invalid nickname before sending anything', async () => {
    rememberTestCareer();
    serveCareer(ended);
    serveEntryStatus(open);
    const received = capturePublications(() => HttpResponse.json(published));
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}`);

    const input = await screen.findByLabelText('Nick w rankingu');
    await user.type(input, 'a_b');
    await user.click(screen.getByRole('button', { name: 'Publikuję wynik' }));

    expect(await screen.findByText(/Nick musi mieć 1–5 znaków/u)).toBeInTheDocument();
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(received).toEqual([]);
  });

  it('says when publication failed', async () => {
    rememberTestCareer();
    serveCareer(ended);
    serveEntryStatus(open);
    capturePublications(() => problem(409, 'submission-closed'));
    const { user } = renderApp(`/kariera/${TEST_CAREER_ID}`);

    await user.type(await screen.findByLabelText('Nick w rankingu'), 'abc');
    await user.click(screen.getByRole('button', { name: 'Publikuję wynik' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Nie udało się opublikować wyniku.');
  });

  it('shows the deadline while a closed season still accepts', async () => {
    rememberTestCareer();
    serveCareer(ended);
    serveEntryStatus({ ...open, submissionDeadline: '2027-01-30T12:00:00Z' });
    renderApp(`/kariera/${TEST_CAREER_ID}`);

    expect(await screen.findByText(/Wyniki z tego sezonu przyjmujemy do/u)).toHaveTextContent(
      '30 sty 2027',
    );
  });

  it('says when the deadline has passed and offers no form', async () => {
    rememberTestCareer();
    serveCareer(ended);
    serveEntryStatus({
      status: 'closed',
      season: 2,
      submissionDeadline: '2027-01-30T12:00:00Z',
      entry: null,
    });
    renderApp(`/kariera/${TEST_CAREER_ID}`);

    expect(
      await screen.findByText(/Termin publikacji wyników z sezonu 2 minął/u),
    ).toHaveTextContent('30 sty 2027');
    expect(screen.queryByLabelText('Nick w rankingu')).not.toBeInTheDocument();
  });

  it('explains that an unranked career has no table', async () => {
    rememberTestCareer();
    serveCareer({ ...ended, ranked: false, season: null });
    serveEntryStatus({ status: 'unranked', season: null, submissionDeadline: null, entry: null });
    renderApp(`/kariera/${TEST_CAREER_ID}`);

    expect(await screen.findByText(/Ta kariera jest bez rankingu/u)).toBeInTheDocument();
    expect(screen.getAllByText('Bez rankingu').length).toBeGreaterThan(0);
  });

  it('shows a hidden entry without a rank', async () => {
    rememberTestCareer();
    serveCareer(ended);
    serveEntryStatus({ ...published, entry: { ...publishedEntry, rank: null } });
    renderApp(`/kariera/${TEST_CAREER_ID}`);

    expect(await screen.findByText(/ŻABA — 45\s000 pkt\. Wpis jest ukryty\./u)).toBeInTheDocument();
  });
});

describe('leaderboard screen', () => {
  it('lists the current season in order with public fields only', async () => {
    serveBoard({
      season: season(),
      entries: [
        {
          rank: 1,
          nickname: 'TOP',
          score: 90_000,
          weeksPlayed: 1040,
          difficulty: 'normal',
          ending: 'completed',
          submittedAt: '2026-10-10T00:00:00Z',
        },
        {
          rank: 2,
          nickname: 'ŻABA',
          score: 45_000,
          weeksPlayed: 12,
          difficulty: 'easy',
          ending: 'earlyRetirement',
          submittedAt: '2026-10-11T00:00:00Z',
        },
      ],
    });
    renderApp('/ranking');

    const table = await screen.findByRole('table', { name: 'Top 100 — sezon 2' });
    const rows = within(table).getAllByRole('row');
    expect(rows[1]).toHaveTextContent(/1TOP90\s000/u);
    expect(rows[2]).toHaveTextContent('ŻABA');
    expect(rows[2]).toHaveTextContent('Łatwy');
    expect(rows[2]).toHaveTextContent('Emerytura');
    expect(screen.getByText(/Sezon trwa/u)).toBeInTheDocument();
  });

  it('switches seasons with the picker and shows a closed deadline', async () => {
    const asked = serveBoard({ season: season(), entries: [] }, [
      season(),
      season({
        number: 1,
        current: false,
        closedAt: '2026-10-01T00:00:00Z',
        submissionDeadline: '2099-01-01T00:00:00Z',
        extended: true,
      }),
    ]);
    const { user, router } = renderApp('/ranking');

    expect(
      await screen.findByText('W tym sezonie nikt jeszcze nie opublikował wyniku.'),
    ).toBeInTheDocument();
    await user.selectOptions(await screen.findByLabelText('Sezon'), '1');

    await waitFor(() => {
      expect(router.state.location.search).toEqual({ sezon: 1 });
    });
    expect(await screen.findByText(/Sezon zamknięty\. Wyniki przyjmujemy do/u)).toHaveTextContent(
      'Termin przedłużony dla wszystkich.',
    );
    expect(asked).toEqual([null, '1']);
  });

  it('says when a season does not exist', async () => {
    serveBoard({ season: season(), entries: [] });
    renderApp('/ranking?sezon=9');

    expect(await screen.findByRole('alert')).toHaveTextContent('Nie ma takiego sezonu.');
  });

  it('is linked from the home screen', async () => {
    renderApp('/');

    expect(await screen.findByRole('link', { name: 'Ranking' })).toHaveAttribute(
      'href',
      '/ranking',
    );
  });
});
