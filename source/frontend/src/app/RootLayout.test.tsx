import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { renderApp } from '../../test/render';

describe('the app layout', () => {
  it('adds a decorative visitor counter that assistive technology skips', async () => {
    renderApp('/');

    expect(await screen.findByText('Jesteś 001337. gościem tej strony!')).toHaveAttribute(
      'aria-hidden',
      'true',
    );
  });

  it('marks the new career link with a decorative label', async () => {
    renderApp('/');

    expect(await screen.findByText('NOWE!')).toHaveAttribute('aria-hidden', 'true');
    expect(screen.getByRole('link', { name: 'Nowa kariera' })).toBeInTheDocument();
  });
});
