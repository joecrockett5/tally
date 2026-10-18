import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { food, nutrients } from '../test/fixtures';
import { mockFetch } from '../test/mockFetch';
import { LogFoodSheet } from './LogFoodSheet';

afterEach(() => vi.unstubAllGlobals());

describe('LogFoodSheet', () => {
  it('defaults to the first serving and posts the chosen quantity', async () => {
    const bread = food({
      id: 'bread',
      name: 'Wholemeal bread',
      source: 'off',
      nutrients: nutrients({ energyKcal: 250 }),
      servings: [{ id: 's1', name: '1 slice (36 g)', amount: 36 }],
    });
    const fetch = mockFetch([{ method: 'POST', path: '/api/diary/entries', status: 201, body: { id: 'e1' } }]);
    const onLogged = vi.fn();
    render(<LogFoodSheet food={bread} date="2026-10-18" meal="Lunch" onClose={() => {}} onLogged={onLogged} />);

    expect(screen.getByRole('button', { name: /Add to Lunch · 90 kcal/ })).toBeInTheDocument();
    await userEvent.clear(screen.getByLabelText('Amount'));
    await userEvent.type(screen.getByLabelText('Amount'), '2');
    await userEvent.click(screen.getByRole('button', { name: /Add to Lunch · 180 kcal/ }));

    expect(onLogged).toHaveBeenCalled();
    const body = JSON.parse(fetch.mock.calls[0][1]!.body as string);
    expect(body).toEqual({ date: '2026-10-18', meal: 'Lunch', foodId: 'bread', unit: 'Serving', quantity: 2, servingId: 's1' });
    expect(screen.getByRole('link', { name: 'Open Food Facts' })).toBeInTheDocument();
  });

  it('logs portion-only foods by portion', () => {
    const wrap = food({ basis: 'PerPortion', portionName: '1 wrap', nutrients: nutrients({ energyKcal: 450 }) });
    render(<LogFoodSheet food={wrap} date="2026-10-18" meal="Lunch" onClose={() => {}} onLogged={() => {}} />);
    expect(screen.getByRole('option', { name: 'portions (1 wrap)' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: /grams/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /450 kcal/ })).toBeInTheDocument();
  });
});
