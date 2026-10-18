export interface Profile {
  id: string;
  name: string;
  avatar: string;
  colour: string;
}

export interface AppConfig {
  version: string;
  timeZone: string;
  today: string;
  onlineEnabled: boolean;
}

export interface Nutrients {
  energyKcal: number | null;
  energyKj: number | null;
  fat: number | null;
  saturates: number | null;
  carbohydrate: number | null;
  sugars: number | null;
  fibre: number | null;
  protein: number | null;
  salt: number | null;
}

export type NutrientKey = keyof Nutrients;

export type Basis = 'Per100g' | 'Per100ml' | 'PerPortion';
export type FoodSourceLabel = 'mine' | 'shared' | 'restaurant' | 'cofid' | 'off' | 'recipe';
export type Meal = 'Breakfast' | 'Lunch' | 'Dinner' | 'Snacks';
export type QuantityUnit = 'Gram' | 'Millilitre' | 'Portion' | 'Serving';

export const MEALS: Meal[] = ['Breakfast', 'Lunch', 'Dinner', 'Snacks'];

export interface Serving {
  id: string;
  name: string;
  amount: number;
}

export interface Food {
  id: string;
  name: string;
  brand: string | null;
  source: FoodSourceLabel;
  restaurantId: string | null;
  restaurant: string | null;
  barcode: string | null;
  basis: Basis;
  portionName: string | null;
  nutrients: Nutrients;
  servings: Serving[];
  isUk: boolean;
  carbsMayIncludeFibre: boolean;
  imageUrl: string | null;
  isShared: boolean;
  canEdit: boolean;
  createdByName: string | null;
  fetchedAt: string | null;
}

export interface NutrientTotals {
  values: Nutrients;
  incomplete: NutrientKey[];
}

export interface Entry {
  id: string;
  date: string;
  meal: Meal;
  foodId: string | null;
  recipeId: string | null;
  name: string;
  brand: string | null;
  source: FoodSourceLabel;
  basis: Basis;
  portionName: string | null;
  unit: QuantityUnit;
  quantity: number;
  servingName: string | null;
  servingAmount: number | null;
  amount: number;
  perBasis: Nutrients;
  nutrients: Nutrients;
}

export interface Goals {
  energyKcal: number | null;
  fat: number | null;
  saturates: number | null;
  carbohydrate: number | null;
  sugars: number | null;
  fibre: number | null;
  protein: number | null;
  salt: number | null;
}

export interface Day {
  date: string;
  meals: { meal: Meal; entries: Entry[]; totals: NutrientTotals }[];
  totals: NutrientTotals;
  goals: Goals | null;
}

export interface QuickPick {
  food: Food;
  unit: QuantityUnit;
  quantity: number;
  servingId: string | null;
  timesLogged: number;
}

export interface OnlineSearch {
  available: boolean;
  message: string | null;
  results: Food[];
}

export interface BarcodeLookup {
  status: 'found' | 'not_found' | 'unavailable';
  barcode: string;
  food: Food | null;
  message: string | null;
}

export interface Restaurant {
  id: string;
  name: string;
  itemCount: number;
}

export interface FoodInput {
  name: string;
  brand: string | null;
  restaurant: string | null;
  barcode: string | null;
  basis: Basis;
  portionName: string | null;
  nutrients: Nutrients;
  servings: { name: string; amount: number }[];
  isShared: boolean;
}

export interface ImportRow {
  row: number;
  food: FoodInput | null;
  errors: string[];
  duplicate: string | null;
}

export interface ImportPreview {
  rows: ImportRow[];
  valid: number;
  invalid: number;
  duplicates: number;
}

export interface ImportResult {
  created: number;
  skippedDuplicates: number;
  skippedInvalid: number;
}
