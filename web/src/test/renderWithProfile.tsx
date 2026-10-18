import { render } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter, Routes } from 'react-router';
import { ProfileProvider, useProfile } from '../profile/ProfileProvider';

export const testProfile = { id: 'p1', name: 'Alex', avatar: '🍎', colour: '#1f6f5c' };

function WhenChosen({ children }: { children: ReactNode }) {
  const { profile } = useProfile();
  return profile ? <>{children}</> : null;
}

/**
 * Renders `routes` at `path` once the remembered test profile has been restored.
 * The mocked fetch must answer GET /api/profiles/p1 with `testProfile`.
 */
export function renderWithProfile(path: string, routes: ReactNode) {
  localStorage.setItem('tally.profileId', testProfile.id);
  return render(
    <MemoryRouter initialEntries={[path]}>
      <ProfileProvider>
        <WhenChosen>
          <Routes>{routes}</Routes>
        </WhenChosen>
      </ProfileProvider>
    </MemoryRouter>,
  );
}
