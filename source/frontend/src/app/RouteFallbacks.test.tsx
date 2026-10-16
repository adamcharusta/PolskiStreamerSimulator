import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { renderApp } from '../../test/render';
import { RouteError } from './RouteFallbacks';
import { createAppRouter } from './router';

describe('the router fallbacks', () => {
  it('shows a Polish page with a way home for an unknown address', async () => {
    const { user, router } = renderApp('/nie-ma-takiej-strony');

    expect(
      await screen.findByRole('heading', { name: 'Nie ma takiej strony' }),
    ).toBeInTheDocument();
    await user.click(screen.getByRole('link', { name: 'Wróć na start' }));

    expect(router.state.location.pathname).toBe('/');
  });

  it('shows a Polish error page when a screen fails', () => {
    expect(createAppRouter().options.defaultErrorComponent).toBe(RouteError);

    render(<RouteError />);

    expect(screen.getByRole('heading', { name: 'Coś poszło nie tak' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Wróć na start' })).toHaveAttribute('href', '/');
  });
});
