import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { Layout } from './components/Layout';
import { AddFoodPage } from './diary/AddFoodPage';
import { DiaryPage } from './diary/DiaryPage';
import { ProfilePicker } from './pages/ProfilePicker';
import { ConfigProvider } from './profile/ConfigProvider';
import { ProfileProvider, useProfile } from './profile/ProfileProvider';

function AppRoutes() {
  const { profile, restoring } = useProfile();
  if (restoring) return <p className="muted center splash">Loading…</p>;

  return (
    <Routes>
      <Route path="/profiles" element={<ProfilePicker />} />
      {profile ? (
        <Route element={<Layout />}>
          <Route path="/diary/:date?" element={<DiaryPage />} />
          <Route path="/diary/:date/add/:meal" element={<AddFoodPage />} />
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
      <ConfigProvider>
        <ProfileProvider>
          <AppRoutes />
        </ProfileProvider>
      </ConfigProvider>
    </BrowserRouter>
  );
}
