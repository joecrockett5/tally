import type { Basis, Goals, NutrientKey, Nutrients, QuantityUnit } from '../api/types';

export interface NutrientInfo {
  key: Exclude<NutrientKey, 'energyKj'>;
  label: string;
  unit: string;
  /** Upper limits (sugars, saturates, salt) vs targets to reach. */
  isLimit: boolean;
  indent?: boolean;
}

/** UK back-of-pack order. kJ is shown alongside kcal rather than as its own row. */
export const NUTRIENTS: NutrientInfo[] = [
  { key: 'energyKcal', label: 'Energy', unit: 'kcal', isLimit: false },
  { key: 'fat', label: 'Fat', unit: 'g', isLimit: false },
  { key: 'saturates', label: 'of which saturates', unit: 'g', isLimit: true, indent: true },
  { key: 'carbohydrate', label: 'Carbohydrate', unit: 'g', isLimit: false },
  { key: 'sugars', label: 'of which sugars', unit: 'g', isLimit: true, indent: true },
  { key: 'fibre', label: 'Fibre', unit: 'g', isLimit: false },
  { key: 'protein', label: 'Protein', unit: 'g', isLimit: false },
  { key: 'salt', label: 'Salt', unit: 'g', isLimit: true },
];

export const DASH = '–';

/** kcal/kJ as whole numbers, grams to one decimal place; unknown is "–", never 0. */
export function formatNutrient(value: number | null | undefined, key: NutrientKey): string {
  if (value === null || value === undefined || Number.isNaN(value)) return DASH;
  if (key === 'energyKcal' || key === 'energyKj') return Math.round(value).toLocaleString('en-GB');
  return (Math.round(value * 10) / 10).toLocaleString('en-GB', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
}

export function formatWithUnit(value: number | null | undefined, key: NutrientKey): string {
  const text = formatNutrient(value, key);
  if (text === DASH) return text;
  return key === 'energyKcal' ? `${text} kcal` : key === 'energyKj' ? `${text} kJ` : `${text} g`;
}

export function scaleNutrients(n: Nutrients, factor: number): Nutrients {
  const s = (v: number | null) => (v === null ? null : v * factor);
  return {
    energyKcal: s(n.energyKcal),
    energyKj: s(n.energyKj),
    fat: s(n.fat),
    saturates: s(n.saturates),
    carbohydrate: s(n.carbohydrate),
    sugars: s(n.sugars),
    fibre: s(n.fibre),
    protein: s(n.protein),
    salt: s(n.salt),
  };
}

/** Nutrition for `amount` basis units (g, ml or portions). */
export function nutrientsForAmount(perBasis: Nutrients, basis: Basis, amount: number): Nutrients {
  return scaleNutrients(perBasis, basis === 'PerPortion' ? amount : amount / 100);
}

export function basisLabel(basis: Basis, portionName?: string | null): string {
  if (basis === 'Per100g') return 'per 100 g';
  if (basis === 'Per100ml') return 'per 100 ml';
  return portionName ? `per portion (${portionName})` : 'per portion';
}

const trimNumber = (n: number) => (Math.round(n * 10) / 10).toLocaleString('en-GB');

export function describeQuantity(e: {
  unit: QuantityUnit;
  quantity: number;
  servingName?: string | null;
  portionName?: string | null;
  amount?: number;
  basis?: Basis;
}): string {
  switch (e.unit) {
    case 'Gram':
      return `${trimNumber(e.quantity)} g`;
    case 'Millilitre':
      return `${trimNumber(e.quantity)} ml`;
    case 'Portion':
      return `${trimNumber(e.quantity)} × ${e.portionName ?? 'portion'}`;
    case 'Serving': {
      const unit = e.basis === 'Per100ml' ? 'ml' : e.basis === 'PerPortion' ? '' : 'g';
      const total = e.amount !== undefined && unit ? ` (${trimNumber(e.amount)} ${unit})` : '';
      return `${trimNumber(e.quantity)} × ${e.servingName ?? 'serving'}${total}`;
    }
  }
}

export type Progress = 'none' | 'under' | 'good' | 'near' | 'over';

/**
 * Colour state for a nutrient against its goal. Targets: green within 90–110%,
 * neutral below, amber above. Limits: green under 90%, amber 90–100%, red over.
 */
export function progressState(value: number | null, goal: number | null | undefined, isLimit: boolean): Progress {
  if (goal === null || goal === undefined || goal <= 0 || value === null) return 'none';
  const ratio = value / goal;
  if (isLimit) return ratio > 1 ? 'over' : ratio >= 0.9 ? 'near' : 'good';
  return ratio > 1.1 ? 'near' : ratio >= 0.9 ? 'good' : 'under';
}

export function goalFor(goals: Goals | null | undefined, key: NutrientInfo['key']): number | null {
  return goals ? goals[key] : null;
}
