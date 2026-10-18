import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { Layout } from './components/Layout';
import { ProfilePicker } from './pages/ProfilePicker';
import { ProfileProvider, useProfile } from './profile/ProfileProvider';

function AppRoutes() {
  const { profile, restoring } = useProfile();
  if (restoring) return <p className="muted center splash">Loading…</p>;

  return (
    <Routes>
      <Route path="/profiles" element={<ProfilePicker />} />
      {profile ? (
        <Route element={<Layout />}>
          <Route path="/diary/:date?" element={<p>Diary coming soon.</p>} />
          <Route path="*" element={<Navigate to="/diary" replace />} />
        </Route>
      ) : (
        <Route path="*" element={<Navigate to="/profiles" replace />} />
      )}
    </Routes>
  );
}

export function App() {
  return (
    <BrowserRouter>
      <ProfileProvider>
        <AppRoutes />
      </ProfileProvider>
    </BrowserRouter>
  );
}
