import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { rememberTestCareer, renderApp } from '../../../test/render';

describe('home', () => {
  it('lists the careers known to this browser', async () => {
    rememberTestCareer({ careerId: 'b', nickname: 'Drugi' });
    rememberTestCareer({ careerId: 'a', nickname: 'Pierwszy' });

    renderApp('/');

    const links = await screen.findAllByRole('link', { name: /Kontynuuj karierę/u });
    expect(links.map((l) => l.getAttribute('href'))).toEqual(['/kariera/a', '/kariera/b']);
    expect(screen.getByText('Pierwszy')).toBeInTheDocument();
  });

  it('shows the empty state, the access notice and the new-career link', async () => {
    renderApp('/');

    expect(
      await screen.findByText('W tej przeglądarce nie masz jeszcze żadnej kariery.'),
    ).toBeInTheDocument();
    expect(screen.getByText(/klucz odzyskiwania przywraca/u)).toHaveTextContent('6 miesiącach');
    expect(screen.getByRole('link', { name: 'Odzyskaj karierę kluczem' })).toHaveAttribute(
      'href',
      '/odzyskaj',
    );
    expect(screen.getByRole('link', { name: 'Nowa kariera' })).toHaveAttribute(
      'href',
      '/nowa-kariera',
    );
  });

  it('shows the empty state for malformed storage instead of failing', async () => {
    localStorage.setItem('pss.careers.v1', '{"oops":');

    renderApp('/');

    expect(
      await screen.findByText('W tej przeglądarce nie masz jeszcze żadnej kariery.'),
    ).toBeInTheDocument();
  });
});
