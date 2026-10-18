import type { Day, Entry, Food, Nutrients } from '../api/types';

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

export const entry = (overrides: Partial<Entry> = {}): Entry => ({
  id: 'e1',
  date: '2026-10-18',
  meal: 'Breakfast',
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
    meals: (['Breakfast', 'Lunch', 'Dinner', 'Snacks'] as const).map((meal) => ({
      meal,
      entries: entries.filter((e) => e.meal === meal),
      totals,
    })),
    totals,
    goals: null,
  };
};
