import type { Profile } from '../api/types';

export function Avatar({ profile, size = 40 }: { profile: Pick<Profile, 'avatar' | 'colour' | 'name'>; size?: number }) {
  return (
    <span
      className="avatar"
      aria-hidden="true"
      style={{ background: profile.colour, width: size, height: size, fontSize: size * 0.55 }}
    >
      {profile.avatar}
    </span>
  );
}
