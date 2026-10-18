import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ProfileProvider } from '../profile/ProfileProvider';
import { mockFetch } from '../test/mockFetch';
import { ProfilePicker } from './ProfilePicker';

const alex = { id: 'a1', name: 'Alex', avatar: '🍎', colour: '#1f6f5c' };
const sam = { id: 's1', name: 'Sam', avatar: '🥑', colour: '#2f5d9b' };

function renderPicker() {
  return render(
    <MemoryRouter initialEntries={['/profiles']}>
      <ProfileProvider>
        <Routes>
          <Route path="/profiles" element={<ProfilePicker />} />
          <Route path="/diary" element={<p>Diary page</p>} />
        </Routes>
      </ProfileProvider>
    </MemoryRouter>,
  );
}

afterEach(() => vi.unstubAllGlobals());

describe('ProfilePicker', () => {
  it('lists profiles and remembers the chosen one', async () => {
    mockFetch([{ path: '/api/profiles', body: [alex, sam] }]);
    renderPicker();

    await userEvent.click(await screen.findByRole('button', { name: /Sam/ }));

    expect(await screen.findByText('Diary page')).toBeInTheDocument();
    expect(localStorage.getItem('tally.profileId')).toBe('s1');
  });

  it('creates a profile with just a name', async () => {
    const fetch = mockFetch([
      { path: '/api/profiles', body: [] },
      { method: 'POST', path: '/api/profiles', status: 201, body: { ...alex, name: 'Jo' } },
    ]);
    renderPicker();

    await userEvent.click(await screen.findByRole('button', { name: /Add profile/ }));
    await userEvent.type(screen.getByLabelText('Name'), 'Jo');
    await userEvent.click(screen.getByRole('button', { name: 'Create' }));

    expect(await screen.findByText('Diary page')).toBeInTheDocument();
    const post = fetch.mock.calls.find(([, init]) => init?.method === 'POST')!;
    expect(JSON.parse(post[1]!.body as string)).toEqual({ name: 'Jo' });
  });

  it('only enables delete once the name is typed exactly', async () => {
    mockFetch([{ path: '/api/profiles', body: [alex] }]);
    renderPicker();

    await userEvent.click(await screen.findByRole('button', { name: 'Manage profiles' }));
    await userEvent.click(screen.getByRole('button', { name: /Alex/ }));

    const del = screen.getByRole('button', { name: 'Delete Alex' });
    expect(del).toBeDisabled();
    await userEvent.type(screen.getByLabelText(/to confirm/), 'alex');
    expect(del).toBeDisabled();
    await userEvent.clear(screen.getByLabelText(/to confirm/));
    await userEvent.type(screen.getByLabelText(/to confirm/), 'Alex');
    expect(del).toBeEnabled();
  });
});
