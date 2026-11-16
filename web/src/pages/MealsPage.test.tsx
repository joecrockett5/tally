import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { MEAL, meals } from '../test/fixtures';
import { mockFetch } from '../test/mockFetch';
import { renderWithProfile, testProfile } from '../test/renderWithProfile';
import { MealsPage } from './MealsPage';

afterEach(() => vi.unstubAllGlobals());

const renderMeals = () =>
  renderWithProfile(
    '/settings/meals',
    <>
      <Route path="/settings/meals" element={<MealsPage />} />
      <Route path="/diary/:date" element={<p>Diary</p>} />
    </>,
  );

describe('MealsPage', () => {
  it('renames, reorders, removes and adds meals, keeping the ids of those that stay', async () => {
    const fetch = mockFetch([
      { path: '/api/profiles/p1', body: testProfile },
      { path: '/api/meals', body: { date: '2026-10-18', effectiveFrom: null, meals } },
      { method: 'PUT', path: '/api/meals', body: { date: '2026-10-18', effectiveFrom: '2026-10-18', meals } },
    ]);
    renderMeals();

    const dinner = await screen.findByDisplayValue('Dinner');
    await userEvent.clear(dinner);
    await userEvent.type(dinner, 'Tea');
    await userEvent.click(screen.getByRole('button', { name: 'Move Tea up' }));
    await userEvent.click(screen.getByRole('button', { name: 'Remove Snacks' }));
    await userEvent.click(screen.getByRole('button', { name: '+ Add a meal' }));
    await userEvent.keyboard('Supper');
    await userEvent.click(screen.getByRole('button', { name: 'Save meals' }));

    expect(await screen.findByText('Diary')).toBeInTheDocument();
    const put = fetch.mock.calls.find(([, init]) => init?.method === 'PUT')!;
    expect(JSON.parse(put[1]!.body as string)).toEqual({
      meals: [
        { id: MEAL.Breakfast, name: 'Breakfast' },
        { id: MEAL.Dinner, name: 'Tea' },
        { id: MEAL.Lunch, name: 'Lunch' },
        { name: 'Supper' },
      ],
    });
  });

  it('asks for a different name instead of saving a duplicate', async () => {
    const fetch = mockFetch([
      { path: '/api/profiles/p1', body: testProfile },
      { path: '/api/meals', body: { date: '2026-10-18', effectiveFrom: null, meals } },
    ]);
    renderMeals();

    const dinner = await screen.findByDisplayValue('Dinner');
    await userEvent.clear(dinner);
    await userEvent.type(dinner, 'lunch');
    await userEvent.click(screen.getByRole('button', { name: 'Save meals' }));

    expect(screen.getByText('Each meal needs a different name.')).toBeInTheDocument();
    expect(fetch.mock.calls.some(([, init]) => init?.method === 'PUT')).toBe(false);
  });
});
