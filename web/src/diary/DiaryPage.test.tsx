import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { emptyDay, entry, MEAL, nutrients } from '../test/fixtures';
import { mockFetch } from '../test/mockFetch';
import { renderWithProfile, testProfile as profile } from '../test/renderWithProfile';
import { DiaryPage } from './DiaryPage';

const renderDiary = (path: string) => renderWithProfile(path, <Route path="/diary/:date?" element={<DiaryPage />} />);

afterEach(() => vi.unstubAllGlobals());

describe('DiaryPage', () => {
  it('shows meals, entries and dashes for unknown nutrients', async () => {
    const day = emptyDay('2026-10-17', [entry({ mealId: MEAL.Lunch, name: 'Mystery bar' })]);
    day.totals = { values: nutrients({ energyKcal: 114 }), incomplete: ['fat'] };
    mockFetch([
      { path: '/api/profiles/p1', body: profile },
      { path: '/api/diary/2026-10-17', body: day },
    ]);
    renderDiary('/diary/2026-10-17');

    const lunch = await screen.findByRole('region', { name: 'Lunch' });
    expect(within(lunch).getByText('Mystery bar')).toBeInTheDocument();
    expect(within(lunch).getByText('120 g')).toBeInTheDocument();
    const totals = screen.getByRole('region', { name: 'Day totals' });
    expect(within(totals).getAllByText(/^–/).length).toBeGreaterThan(0);
    expect(within(totals).getAllByText('+?').length).toBeGreaterThan(0);
    expect(screen.getByText('Sat 17/10/2026')).toBeInTheDocument();
  });

  it('copies a meal from yesterday', async () => {
    const fetch = mockFetch([
      { path: '/api/profiles/p1', body: profile },
      { path: '/api/diary/2026-10-17', body: emptyDay('2026-10-17') },
      { method: 'POST', path: '/api/diary/copy', body: { copied: 2 } },
    ]);
    renderDiary('/diary/2026-10-17');

    await userEvent.click(await screen.findByRole('button', { name: 'Breakfast options' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Copy from yesterday' }));

    expect(await screen.findByText(/Copied 2 items from yesterday's breakfast/)).toBeInTheDocument();
    const post = fetch.mock.calls.find(([url]) => url === '/api/diary/copy')!;
    expect(JSON.parse(post[1]!.body as string)).toEqual({
      fromDate: '2026-10-16',
      toDate: '2026-10-17',
      fromMealId: MEAL.Breakfast,
      toMealId: MEAL.Breakfast,
    });
  });

  it('shows custom meals, and a removed meal that still has entries without a way to add to it', async () => {
    const day = emptyDay('2026-10-17');
    day.meals = [
      { id: 'm-brunch', name: 'Brunch', active: true, entries: [], totals: day.totals },
      { id: MEAL.Snacks, name: 'Snacks', active: false, entries: [entry({ mealId: MEAL.Snacks })], totals: day.totals },
    ];
    mockFetch([
      { path: '/api/profiles/p1', body: profile },
      { path: '/api/diary/2026-10-17', body: day },
    ]);
    renderDiary('/diary/2026-10-17');

    const brunch = await screen.findByRole('region', { name: 'Brunch' });
    expect(within(brunch).getByRole('link', { name: '+ Add food' })).toHaveAttribute('href', '/diary/2026-10-17/add/m-brunch');
    const snacks = screen.getByRole('region', { name: 'Snacks' });
    expect(within(snacks).queryByRole('link', { name: '+ Add food' })).not.toBeInTheDocument();
    expect(within(snacks).getByText(/has been removed/)).toBeInTheDocument();

    // The entry can be moved out of the removed meal, or left where it is.
    await userEvent.click(within(snacks).getByRole('button', { name: /Banana/ }));
    const options = within(screen.getByRole('combobox', { name: 'Meal' })).getAllByRole('option');
    expect(options.map((o) => o.textContent)).toEqual(['Brunch', 'Snacks']);
  });
});
