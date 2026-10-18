import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, PROFILE_MISSING_EVENT, setActiveProfileId } from '../api/client';
import type { Profile } from '../api/types';

const STORAGE_KEY = 'tally.profileId';

interface ProfileState {
  profile: Profile | null;
  /** True until the remembered profile has been checked against the server. */
  restoring: boolean;
  choose: (profile: Profile) => void;
  update: (profile: Profile) => void;
  signOut: () => void;
}

const ProfileContext = createContext<ProfileState | null>(null);

function readStoredId(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStoredId(id: string | null) {
  try {
    if (id) localStorage.setItem(STORAGE_KEY, id);
    else localStorage.removeItem(STORAGE_KEY);
  } catch {
    /* storage unavailable (private mode) — profile is just not remembered */
  }
}

export function ProfileProvider({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [restoring, setRestoring] = useState(() => readStoredId() !== null);

  const choose = useCallback((p: Profile) => {
    setActiveProfileId(p.id);
    writeStoredId(p.id);
    setProfile(p);
  }, []);

  const signOut = useCallback(() => {
    setActiveProfileId(null);
    writeStoredId(null);
    setProfile(null);
  }, []);

  useEffect(() => {
    const stored = readStoredId();
    if (!stored) return;
    api
      .get<Profile>(`/api/profiles/${stored}`)
      .then(choose)
      .catch(() => signOut())
      .finally(() => setRestoring(false));
  }, [choose, signOut]);

  useEffect(() => {
    window.addEventListener(PROFILE_MISSING_EVENT, signOut);
    return () => window.removeEventListener(PROFILE_MISSING_EVENT, signOut);
  }, [signOut]);

  const value = useMemo(
    () => ({ profile, restoring, choose, update: setProfile, signOut }),
    [profile, restoring, choose, signOut],
  );
  return <ProfileContext.Provider value={value}>{children}</ProfileContext.Provider>;
}

export function useProfile(): ProfileState {
  const ctx = useContext(ProfileContext);
  if (!ctx) throw new Error('useProfile must be used inside ProfileProvider');
  return ctx;
}

/** For pages that only render once a profile is chosen. */
export function useActiveProfile(): Profile {
  const { profile } = useProfile();
  if (!profile) throw new Error('No active profile');
  return profile;
}
