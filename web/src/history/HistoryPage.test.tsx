import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route } from 'react-router';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import type { History } from '../api/types';
import { nutrients } from '../test/fixtures';
import { mockFetch } from '../test/mockFetch';
import { renderWithProfile, testProfile } from '../test/renderWithProfile';
import { HistoryPage, periodRange } from './HistoryPage';

beforeAll(() => {
  vi.stubGlobal('ResizeObserver', class { observe() {} unobserve() {} disconnect() {} });
});
afterEach(() => vi.unstubAllGlobals());

describe('periodRange', () => {
  it('uses Monday-start weeks and calendar months', () => {
    expect(periodRange('week', '2026-10-18')).toEqual({ from: '2026-10-12', to: '2026-10-18' });
    expect(periodRange('month', '2026-02-10')).toEqual({ from: '2026-02-01', to: '2026-02-28' });
  });
});

describe('HistoryPage', () => {
  it('shows averages over logged days and days on target', async () => {
    const days = Array.from({ length: 7 }, (_, i) => ({
      date: `2026-10-${String(12 + i).padStart(2, '0')}`,
      entryCount: i < 2 ? 3 : 0,
      totals: nutrients({ energyKcal: i === 0 ? 2000 : i === 1 ? 1000 : null }),
      incomplete: [],
      onTarget: i === 0 ? true : i === 1 ? false : null,
    }));
    const history: History = {
      from: '2026-10-12',
      to: '2026-10-18',
      days,
      loggedDays: 2,
      averages: nutrients({ energyKcal: 1500, protein: 60 }),
      macros: { proteinKcal: 240, carbohydrateKcal: 800, fatKcal: 460, proteinPct: 16, carbohydratePct: 53.3, fatPct: 30.7 },
      daysOnTarget: 1,
      goals: { energyKcal: 2000, fat: 70, saturates: 20, carbohydrate: 260, sugars: 90, fibre: 30, protein: 50, salt: 6 },
    };
    vi.useFakeTimers({ toFake: ['Date'], now: new Date('2026-10-18T12:00:00Z') });
    mockFetch([
      { path: '/api/profiles/p1', body: testProfile },
      { path: '/api/config', body: { version: '1', timeZone: 'Europe/London', today: '2026-10-18', onlineEnabled: true } },
      { path: /\/api\/history\?from=2026-10-12&to=2026-10-18/, body: history },
    ]);
    renderWithProfile('/history', <Route path="/history" element={<HistoryPage />} />);

    const summary = await screen.findByRole('region', { name: 'Summary' });
    expect(within(summary).getByText('1,500')).toBeInTheDocument();
    expect(within(summary).getByText('1/2')).toBeInTheDocument();
    expect(screen.getByText(/Only days with at least one entry count towards averages \(2 of 7 days\)/)).toBeInTheDocument();
    expect(screen.getByText('53%')).toBeInTheDocument();
    expect(screen.getAllByText('not logged')).toHaveLength(5);
    vi.useRealTimers();
  });

  it('downloads the CSV export with the profile header', async () => {
    const fetch = mockFetch([
      { path: '/api/profiles/p1', body: testProfile },
      { path: /\/api\/history/, body: { from: 'x', to: 'x', days: [], loggedDays: 0, averages: nutrients(), macros: null, daysOnTarget: null, goals: {} } },
      { path: /\/api\/export\/diary\.csv/, body: 'date,meal' },
    ]);
    URL.createObjectURL = vi.fn(() => 'blob:x');
    URL.revokeObjectURL = vi.fn();
    renderWithProfile('/history', <Route path="/history" element={<HistoryPage />} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Download CSV' }));
    const call = fetch.mock.calls.find(([url]) => String(url).startsWith('/api/export/diary.csv'))!;
    expect((call[1]!.headers as Record<string, string>)['X-Profile-Id']).toBe('p1');
    expect(URL.createObjectURL).toHaveBeenCalled();
  });
});
