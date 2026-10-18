import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { mockFetch } from '../test/mockFetch';
import { detectFormat, ImportPage } from './ImportPage';

afterEach(() => vi.unstubAllGlobals());

describe('ImportPage', () => {
  it('detects the format from name or content', () => {
    expect(detectFormat('menu.JSON', '')).toBe('json');
    expect(detectFormat('', '  [{"name":"x"}]')).toBe('json');
    expect(detectFormat('', 'name,energy_kcal')).toBe('csv');
  });

  it('previews rows with errors before importing', async () => {
    mockFetch([
      {
        method: 'POST',
        path: '/api/import/preview',
        body: {
          rows: [
            { row: 2, food: { name: 'Sausage Roll', restaurant: 'Greggs', nutrients: { energyKcal: 327 } }, errors: [], duplicate: null },
            { row: 3, food: { name: 'Bad', restaurant: null, nutrients: { energyKcal: null } }, errors: ['Give energy_kcal or energy_kj.'], duplicate: null },
          ],
          valid: 1,
          invalid: 1,
          duplicates: 0,
        },
      },
    ]);
    render(
      <MemoryRouter>
        <ImportPage />
      </MemoryRouter>,
    );
    await userEvent.type(screen.getByLabelText('…or paste the contents'), 'name,energy_kcal');
    await userEvent.click(screen.getByRole('button', { name: 'Preview' }));

    expect(await screen.findByText('Give energy_kcal or energy_kj.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Import 1 food (private)' })).toBeEnabled();
  });
});
