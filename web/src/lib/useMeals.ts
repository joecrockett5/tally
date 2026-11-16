import { api } from '../api/client';
import type { MealLayout, MealSlot } from '../api/types';
import { mealForHour } from './dates';
import { useApi } from './useApi';

/** The active profile's meals on `date`. Layouts are dated, so an old day keeps the meals it had. */
export function useMeals(date: string) {
  const layout = useApi((s) => api.get<MealLayout>(`/api/meals?date=${date}`, s), [date]);
  return { ...layout, meals: layout.data?.date === date ? layout.data.meals : undefined };
}

/** A sensible meal for "now": the one named after the time of day if there is one, else the first. */
export function defaultMeal(meals: MealSlot[], hour = new Date().getHours()): MealSlot | undefined {
  const usual = mealForHour(hour).toLowerCase();
  return meals.find((m) => m.name.toLowerCase() === usual) ?? meals[0];
}
