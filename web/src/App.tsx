import { BrowserRouter, Navigate, Route, Routes } from 'react-router';
import { ErrorBoundary } from './components/ErrorBoundary';
import { Layout } from './components/Layout';
import { OfflineBanner } from './components/OfflineBanner';
import { AddFoodPage } from './diary/AddFoodPage';
import { DiaryPage } from './diary/DiaryPage';
import { FoodDetailPage } from './foods/FoodDetailPage';
import { FoodFormPage } from './foods/FoodFormPage';
import { FoodsPage } from './foods/FoodsPage';
import { HistoryPage } from './history/HistoryPage';
import { ImportPage } from './foods/ImportPage';
import { RestaurantPage } from './foods/RestaurantPage';
import { GoalsPage } from './pages/GoalsPage';
import { ProfilePicker } from './pages/ProfilePicker';
import { RecipeDetailPage } from './recipes/RecipeDetailPage';
import { RecipeFormPage } from './recipes/RecipeFormPage';
import { RecipesPage } from './recipes/RecipesPage';
import { SavedMealPage } from './recipes/SavedMealPage';
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
          <Route path="/recipes" element={<RecipesPage />} />
          <Route path="/recipes/new" element={<RecipeFormPage />} />
          <Route path="/recipes/:id" element={<RecipeDetailPage />} />
          <Route path="/recipes/:id/edit" element={<RecipeFormPage />} />
          <Route path="/meals/:id" element={<SavedMealPage />} />
          <Route path="/goals" element={<GoalsPage />} />
          <Route path="/history" element={<HistoryPage />} />
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
    <ErrorBoundary>
      <BrowserRouter>
        <OfflineBanner />
        <ConfigProvider>
          <ProfileProvider>
            <AppRoutes />
          </ProfileProvider>
        </ConfigProvider>
      </BrowserRouter>
    </ErrorBoundary>
  );
}
