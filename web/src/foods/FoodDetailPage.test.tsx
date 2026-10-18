import { screen } from '@testing-library/react';
import { Route } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { food } from '../test/fixtures';
import { mockFetch } from '../test/mockFetch';
import { renderWithProfile, testProfile } from '../test/renderWithProfile';
import { FoodDetailPage } from './FoodDetailPage';

afterEach(() => vi.unstubAllGlobals());

const show = (f: ReturnType<typeof food>) => {
  mockFetch([
    { path: '/api/profiles/p1', body: testProfile },
    { path: '/api/config', body: { version: '1', timeZone: 'Europe/London', today: '2026-10-18', onlineEnabled: true } },
    { path: `/api/foods/${f.id}`, body: f },
  ]);
  renderWithProfile(`/foods/${f.id}`, <Route path="/foods/:id" element={<FoodDetailPage />} />);
};

describe('FoodDetailPage', () => {
  it("offers copy, not edit, for someone else's shared item", async () => {
    show(food({ id: 'x', name: 'Peri wrap', source: 'restaurant', restaurant: "Nando's", restaurantId: 'r1', isShared: true, canEdit: false, createdByName: 'Sam' }));
    expect(await screen.findByRole('button', { name: 'Copy to my foods' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Edit' })).not.toBeInTheDocument();
    expect(screen.getByText('Added by Sam')).toBeInTheDocument();
  });

  it('lets the owner edit and unshare', async () => {
    show(food({ id: 'y', source: 'mine', isShared: true, canEdit: true }));
    expect(await screen.findByRole('link', { name: 'Edit' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Stop sharing' })).toBeInTheDocument();
  });
});
