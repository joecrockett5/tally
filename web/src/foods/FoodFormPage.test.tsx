import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { mockFetch } from '../test/mockFetch';
import { renderWithProfile, testProfile } from '../test/renderWithProfile';
import { FoodFormPage, parseOptionalNumber } from './FoodFormPage';

afterEach(() => vi.unstubAllGlobals());

describe('parseOptionalNumber', () => {
  it('treats blank as unknown and accepts comma decimals', () => {
    expect(parseOptionalNumber('')).toBeNull();
    expect(parseOptionalNumber(' 2,5 ')).toBe(2.5);
    expect(parseOptionalNumber('-1')).toBe('invalid');
    expect(parseOptionalNumber('abc')).toBe('invalid');
  });
});

describe('FoodFormPage', () => {
  it('prefills the barcode, sends blanks as null and converts sodium to salt', async () => {
    const fetch = mockFetch([
      { path: '/api/profiles/p1', body: testProfile },
      { path: /\/api\/restaurants/, body: [] },
      { method: 'POST', path: '/api/foods', status: 201, body: { id: 'new1' } },
    ]);
    renderWithProfile(
      '/foods/new?barcode=5012345678900',
      <>
        <Route path="/foods/new" element={<FoodFormPage />} />
        <Route path="/foods/:id" element={<p>Saved</p>} />
      </>,
    );

    expect(await screen.findByLabelText('Barcode (optional)')).toHaveValue('5012345678900');
    await userEvent.type(screen.getByLabelText('Name'), 'Corner shop flapjack');
    await userEvent.type(screen.getByLabelText(/Energy \(kcal\)/), '420');
    await userEvent.type(screen.getByLabelText('Sodium (g) — converted to salt'), '0.2');
    await userEvent.click(screen.getByRole('button', { name: 'Create food' }));

    expect(await screen.findByText('Saved')).toBeInTheDocument();
    const post = fetch.mock.calls.find(([url, init]) => url === '/api/foods' && init?.method === 'POST')!;
    const body = JSON.parse(post[1]!.body as string);
    expect(body.barcode).toBe('5012345678900');
    expect(body.nutrients.energyKcal).toBe(420);
    expect(body.nutrients.fat).toBeNull();
    expect(body.nutrients.salt).toBeCloseTo(0.5);
    expect(body.isShared).toBe(false);
  });

  it('requires energy', async () => {
    mockFetch([{ path: '/api/profiles/p1', body: testProfile }]);
    renderWithProfile('/foods/new', <Route path="/foods/new" element={<FoodFormPage />} />);
    await userEvent.type(await screen.findByLabelText('Name'), 'Thing');
    await userEvent.click(screen.getByRole('button', { name: 'Create food' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Enter energy in kcal or kJ.');
  });
});
