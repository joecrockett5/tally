import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { Recipe } from '../api/types';
import { emptyDay, entry, nutrients } from '../test/fixtures';
import { mockFetch } from '../test/mockFetch';
import { renderWithProfile, testProfile } from '../test/renderWithProfile';
import { DiaryPage } from '../diary/DiaryPage';
import { LogRecipeSheet } from './LogRecipeSheet';
import { sumNutrients } from './RecipeFormPage';

afterEach(() => vi.unstubAllGlobals());

const chilli: Recipe = {
  id: 'r1',
  name: 'Lentil chilli',
  servings: 4,
  cookedWeightGrams: 1200,
  ingredients: [],
  totals: { values: nutrients({ energyKcal: 560 }), incomplete: ['fat'] },
  perServing: nutrients({ energyKcal: 140 }),
  per100g: nutrients({ energyKcal: 560 / 12 }),
};

describe('sumNutrients', () => {
  it('sums known values and keeps all-unknown as null', () => {
    const sum = sumNutrients([nutrients({ energyKcal: 100, fat: 2 }), nutrients({ energyKcal: 50 })]);
    expect(sum.energyKcal).toBe(150);
    expect(sum.fat).toBe(2);
    expect(sum.protein).toBeNull();
  });
});

describe('LogRecipeSheet', () => {
  it('logs servings by default and grams when a cooked weight exists', async () => {
    const fetch = mockFetch([{ method: 'POST', path: '/api/diary/entries', status: 201, body: { id: 'e1', date: '2026-10-18' } }]);
    render(<LogRecipeSheet recipe={chilli} date="2026-10-18" meal="Dinner" onClose={() => {}} onLogged={() => {}} />);

    expect(screen.getByRole('button', { name: /140 kcal/ })).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Unit' }), 'g');
    expect(screen.getByLabelText('Amount')).toHaveValue(300); // one serving's worth
    await userEvent.click(screen.getByRole('button', { name: /Add to Dinner · 140 kcal/ }));

    const body = JSON.parse(fetch.mock.calls[0][1]!.body as string);
    expect(body).toEqual({ date: '2026-10-18', meal: 'Dinner', recipeId: 'r1', unit: 'Gram', quantity: 300 });
  });
});

describe('Save as meal', () => {
  it('saves a diary meal under a name', async () => {
    const fetch = mockFetch([
      { path: '/api/profiles/p1', body: testProfile },
      { path: '/api/diary/2026-10-17', body: emptyDay('2026-10-17', [entry({ meal: 'Breakfast' })]) },
      { method: 'POST', path: '/api/saved-meals/from-diary', status: 201, body: { id: 'm1' } },
    ]);
    renderWithProfile('/diary/2026-10-17', <Route path="/diary/:date?" element={<DiaryPage />} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Breakfast options' }));
    await userEvent.click(screen.getByRole('menuitem', { name: 'Save as meal…' }));
    await userEvent.click(screen.getByRole('button', { name: 'Save meal' }));

    expect(await screen.findByText(/Saved “Usual breakfast”/)).toBeInTheDocument();
    const post = fetch.mock.calls.find(([url]) => url === '/api/saved-meals/from-diary')!;
    expect(JSON.parse(post[1]!.body as string)).toEqual({ name: 'Usual breakfast', date: '2026-10-17', meal: 'Breakfast' });
  });

  it('is not offered for an empty meal', async () => {
    mockFetch([
      { path: '/api/profiles/p1', body: testProfile },
      { path: '/api/diary/2026-10-17', body: emptyDay('2026-10-17') },
    ]);
    renderWithProfile('/diary/2026-10-17', <Route path="/diary/:date?" element={<DiaryPage />} />);
    await userEvent.click(await screen.findByRole('button', { name: 'Lunch options' }));
    expect(screen.queryByRole('menuitem', { name: 'Save as meal…' })).not.toBeInTheDocument();
  });
});
