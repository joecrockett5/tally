import type { Day, Entry, Food, MealSlot, Nutrients } from '../api/types';

export const nutrients = (overrides: Partial<Nutrients> = {}): Nutrients => ({
  energyKcal: null,
  energyKj: null,
  fat: null,
  saturates: null,
  carbohydrate: null,
  sugars: null,
  fibre: null,
  protein: null,
  salt: null,
  ...overrides,
});

export const food = (overrides: Partial<Food> = {}): Food => ({
  id: 'f1',
  name: 'Banana',
  brand: null,
  source: 'cofid',
  restaurantId: null,
  restaurant: null,
  barcode: null,
  basis: 'Per100g',
  portionName: null,
  nutrients: nutrients({ energyKcal: 95, energyKj: 403, fat: 0.3, protein: 1.2 }),
  servings: [],
  isUk: true,
  carbsMayIncludeFibre: false,
  imageUrl: null,
  isShared: false,
  canEdit: false,
  createdByName: null,
  fetchedAt: null,
  ...overrides,
});

/** Ids of the built-in meals, as the API returns them. */
export const MEAL = {
  Breakfast: '0a11e000-0000-4000-8000-000000000001',
  Lunch: '0a11e000-0000-4000-8000-000000000002',
  Dinner: '0a11e000-0000-4000-8000-000000000003',
  Snacks: '0a11e000-0000-4000-8000-000000000004',
} as const;

export const meals: MealSlot[] = Object.entries(MEAL).map(([name, id]) => ({ id, name }));

export const mealLayout = (date: string) => ({ date, effectiveFrom: null, meals });

export const entry = (overrides: Partial<Entry> = {}): Entry => ({
  id: 'e1',
  date: '2026-10-18',
  mealId: MEAL.Breakfast,
  foodId: 'f1',
  recipeId: null,
  name: 'Banana',
  brand: null,
  source: 'cofid',
  basis: 'Per100g',
  portionName: null,
  unit: 'Gram',
  quantity: 120,
  servingName: null,
  servingAmount: null,
  amount: 120,
  perBasis: nutrients({ energyKcal: 95 }),
  nutrients: nutrients({ energyKcal: 114 }),
  ...overrides,
});

export const emptyDay = (date: string, entries: Entry[] = []): Day => {
  const totals = { values: nutrients({ energyKcal: entries.reduce((s, e) => s + (e.nutrients.energyKcal ?? 0), 0) }), incomplete: [] };
  return {
    date,
    meals: meals.map((meal) => ({
      ...meal,
      active: true,
      entries: entries.filter((e) => e.mealId === meal.id),
      totals,
    })),
    totals,
    goals: null,
  };
};
