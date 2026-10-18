import { NavLink, Outlet, useNavigate } from 'react-router';
import { useActiveProfile } from '../profile/ProfileProvider';
import { Avatar } from './Avatar';

const NAV = [
  { to: '/diary', label: 'Diary', icon: '📖' },
  { to: '/foods', label: 'Foods', icon: '🥫' },
  { to: '/recipes', label: 'Recipes', icon: '🍲' },
  { to: '/history', label: 'History', icon: '📊' },
  { to: '/goals', label: 'Goals', icon: '🎯' },
];

export function Layout() {
  const profile = useActiveProfile();
  const navigate = useNavigate();

  return (
    <div className="app">
      <header className="app-header">
        <span className="brand">Tally</span>
        <button
          type="button"
          className="profile-switch"
          onClick={() => navigate('/profiles')}
          aria-label={`Switch profile (current: ${profile.name})`}
        >
          <span className="profile-switch-name">{profile.name}</span>
          <Avatar profile={profile} size={32} />
        </button>
      </header>
      <main className="app-main">
        <Outlet />
      </main>
      <nav className="tab-bar" aria-label="Main">
        {NAV.map((n) => (
          <NavLink key={n.to} to={n.to} className="tab">
            <span aria-hidden="true" className="tab-icon">
              {n.icon}
            </span>
            <span>{n.label}</span>
          </NavLink>
        ))}
      </nav>
    </div>
  );
}
