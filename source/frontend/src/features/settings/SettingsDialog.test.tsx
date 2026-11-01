import { screen } from '@testing-library/react';
import { expect, it } from 'vitest';

import { useTextFilters } from '@/textFilters/textFilters';

import { serveCareer } from '../../../test/api';
import { careerView, decisionNext } from '../../../test/fixtures';
import { rememberTestCareer, renderApp, TEST_CAREER_ID } from '../../../test/render';

it('switches the theme from the settings dialog and closes it', async () => {
  const { user } = renderApp('/');

  await user.click(await screen.findByRole('button', { name: 'Ustawienia' }));
  await user.click(screen.getByRole('radio', { name: 'Shareware z Windowsa 98' }));

  expect(document.documentElement.dataset.theme).toBe('win98');
  expect(localStorage.getItem('pss.theme')).toBe('win98');
  await user.click(screen.getByRole('button', { name: 'Zamknij' }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});

it('switches the text filters on, both off by default, and keeps them for the next visit', async () => {
  const { user } = renderApp('/');

  await user.click(await screen.findByRole('button', { name: 'Ustawienia' }));
  const streamerFriendly = screen.getByRole('checkbox', { name: 'Tryb Streamer Friendly' });
  const sensitive = screen.getByRole('checkbox', { name: 'Łagodniejsze opisy treści wrażliwych' });
  expect(streamerFriendly).not.toBeChecked();
  expect(sensitive).not.toBeChecked();
  expect(streamerFriendly).toHaveAccessibleDescription(/Lista nie gwarantuje zgodności/u);

  await user.click(streamerFriendly);
  await user.click(sensitive);

  expect(useTextFilters.getState()).toMatchObject({ streamerFriendly: true, sensitive: true });
  expect(JSON.parse(localStorage.getItem('pss.textFilters.v1') ?? '{}')).toEqual({
    streamerFriendly: true,
    sensitive: true,
  });
});

it('changes the pronouns of a career in this browser only, and the text follows', async () => {
  rememberTestCareer();
  const seen = serveCareer(careerView({ rulesetId: 'm3-systems.1', next: decisionNext() }));
  const { user } = renderApp(`/kariera/${TEST_CAREER_ID}`);

  expect(await screen.findByText('Dostałaś ofertę współpracy.')).toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Ustawienia' }));
  const neutral = screen.getByRole('radio', { name: 'forma neutralna' });
  expect(screen.getByRole('radio', { name: 'ona/jej' })).toBeChecked();
  await user.click(neutral);
  await user.click(screen.getByRole('button', { name: 'Zamknij' }));

  expect(await screen.findByText('Dostałoś ofertę współpracy.')).toBeInTheDocument();
  expect(JSON.parse(localStorage.getItem('pss.pronouns.v1') ?? '{}')).toEqual({
    [TEST_CAREER_ID]: 'nonbinary',
  });
  // Only career reads reached the server (any other request fails the test): the pronouns stay in this browser.
  expect(seen.every((auth) => auth === 'Bearer secret-1')).toBe(true);
});
