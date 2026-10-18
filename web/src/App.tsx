import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { Layout } from './components/Layout';
import { AddFoodPage } from './diary/AddFoodPage';
import { DiaryPage } from './diary/DiaryPage';
import { FoodDetailPage } from './foods/FoodDetailPage';
import { FoodFormPage } from './foods/FoodFormPage';
import { FoodsPage } from './foods/FoodsPage';
import { ImportPage } from './foods/ImportPage';
import { RestaurantPage } from './foods/RestaurantPage';
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
          <Route path="/foods" element={<FoodsPage />} />
          <Route path="/foods/new" element={<FoodFormPage />} />
          <Route path="/foods/import" element={<ImportPage />} />
          <Route path="/foods/:id" element={<FoodDetailPage />} />
          <Route path="/foods/:id/edit" element={<FoodFormPage />} />
          <Route path="/restaurants/:id" element={<RestaurantPage />} />
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
