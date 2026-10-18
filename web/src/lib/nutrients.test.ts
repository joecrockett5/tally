import { describe, expect, it } from 'vitest';
import { describeQuantity, formatNutrient, nutrientsForAmount, progressState } from './nutrients';
import { nutrients } from '../test/fixtures';

describe('formatNutrient', () => {
  it('shows a dash for unknown values, never zero', () => {
    expect(formatNutrient(null, 'fat')).toBe('–');
    expect(formatNutrient(undefined, 'energyKcal')).toBe('–');
  });
  it('rounds kcal to integers and grams to one decimal', () => {
    expect(formatNutrient(181.6, 'energyKcal')).toBe('182');
    expect(formatNutrient(2, 'fat')).toBe('2.0');
    expect(formatNutrient(0.149, 'salt')).toBe('0.1');
    expect(formatNutrient(2345.4, 'energyKcal')).toBe('2,345');
  });
});

describe('nutrientsForAmount', () => {
  it('scales per-100 values by amount/100 and portions by count', () => {
    expect(nutrientsForAmount(nutrients({ energyKcal: 200, fat: null }), 'Per100g', 150)).toMatchObject({ energyKcal: 300, fat: null });
    expect(nutrientsForAmount(nutrients({ energyKcal: 450 }), 'PerPortion', 2).energyKcal).toBe(900);
  });
});

describe('progressState', () => {
  it('treats targets and limits differently', () => {
    expect(progressState(1000, 2000, false)).toBe('under');
    expect(progressState(1950, 2000, false)).toBe('good');
    expect(progressState(2500, 2000, false)).toBe('near');
    expect(progressState(3, 6, true)).toBe('good');
    expect(progressState(5.6, 6, true)).toBe('near');
    expect(progressState(7, 6, true)).toBe('over');
    expect(progressState(null, 6, true)).toBe('none');
    expect(progressState(5, null, true)).toBe('none');
  });
});

describe('describeQuantity', () => {
  it('describes each unit', () => {
    expect(describeQuantity({ unit: 'Gram', quantity: 150 })).toBe('150 g');
    expect(describeQuantity({ unit: 'Serving', quantity: 2, servingName: '1 slice', amount: 72, basis: 'Per100g' })).toBe('2 × 1 slice (72 g)');
    expect(describeQuantity({ unit: 'Portion', quantity: 1, portionName: '1/4 chicken' })).toBe('1 × 1/4 chicken');
  });
});
