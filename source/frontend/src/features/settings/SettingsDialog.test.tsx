import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it } from 'vitest';

import { SettingsDialog } from './SettingsDialog';

it('switches the theme from the settings dialog and closes it', async () => {
  const user = userEvent.setup();
  render(<SettingsDialog />);

  await user.click(screen.getByRole('button', { name: 'Ustawienia' }));
  await user.click(screen.getByRole('radio', { name: 'Shareware z Windowsa 98' }));

  expect(document.documentElement.dataset.theme).toBe('win98');
  expect(localStorage.getItem('pss.theme')).toBe('win98');
  await user.click(screen.getByRole('button', { name: 'Zamknij' }));
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
});
