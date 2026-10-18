import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { emptyDay, nutrients } from '../test/fixtures';
import { mockFetch } from '../test/mockFetch';
import { renderWithProfile, testProfile } from '../test/renderWithProfile';
import { DiaryPage } from '../diary/DiaryPage';
import { GoalsPage } from './GoalsPage';

afterEach(() => vi.unstubAllGlobals());

const ri = { energyKcal: 2000, fat: 70, saturates: 20, carbohydrate: 260, sugars: 90, fibre: 30, protein: 50, salt: 6 };

describe('GoalsPage', () => {
  it('saves edited targets and blanks as no target', async () => {
    const fetch = mockFetch([
      { path: '/api/profiles/p1', body: testProfile },
      { path: '/api/goals', body: ri },
      { method: 'PUT', path: '/api/goals', body: (init: RequestInit) => JSON.parse(init.body as string) },
    ]);
    renderWithProfile('/goals', <Route path="/goals" element={<GoalsPage />} />);

    const protein = await screen.findByLabelText('Protein (g)');
    expect(protein).toHaveValue('50');
    await userEvent.clear(protein);
    await userEvent.type(protein, '120');
    await userEvent.clear(screen.getByLabelText('Fat (g)'));
    await userEvent.click(screen.getByRole('button', { name: 'Save goals' }));

    expect(await screen.findByText('Goals saved.')).toBeInTheDocument();
    const put = fetch.mock.calls.find(([, init]) => init?.method === 'PUT')!;
    expect(JSON.parse(put[1]!.body as string)).toMatchObject({ protein: 120, fat: null, energyKcal: 2000 });
  });
});

describe('Day progress', () => {
  it('colours limits red when exceeded and targets green when met', async () => {
    const day = emptyDay('2026-10-17');
    day.goals = ri;
    day.totals = { values: nutrients({ energyKcal: 1950, salt: 7, protein: 20 }), incomplete: [] };
    mockFetch([
      { path: '/api/profiles/p1', body: testProfile },
      { path: '/api/diary/2026-10-17', body: day },
    ]);
    renderWithProfile('/diary/2026-10-17', <Route path="/diary/:date?" element={<DiaryPage />} />);

    expect(await screen.findByRole('progressbar', { name: 'Salt progress' })).toHaveClass('progress-over');
    expect(screen.getByRole('progressbar', { name: 'Energy progress' })).toHaveClass('progress-good');
    expect(screen.getByRole('progressbar', { name: 'Protein progress' })).toHaveClass('progress-under');
    expect(screen.getByText('kcal left')).toBeInTheDocument();
  });
});
