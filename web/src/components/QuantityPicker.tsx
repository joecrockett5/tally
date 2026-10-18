import type { Basis, Food, QuantityUnit } from '../api/types';

export interface QuantityChoice {
  unit: QuantityUnit;
  servingId: string | null;
  quantity: number;
}

/** The amount in basis units (g, ml or portions) for a choice. */
export function amountFor(food: Pick<Food, 'servings'>, choice: QuantityChoice): number {
  if (choice.unit === 'Serving') {
    const serving = food.servings.find((s) => s.id === choice.servingId);
    return serving ? serving.amount * choice.quantity : 0;
  }
  return choice.quantity;
}

export function baseUnit(basis: Basis): QuantityUnit {
  return basis === 'PerPortion' ? 'Portion' : basis === 'Per100ml' ? 'Millilitre' : 'Gram';
}

export function defaultChoice(food: Food): QuantityChoice {
  if (food.servings.length > 0) return { unit: 'Serving', servingId: food.servings[0].id, quantity: 1 };
  return food.basis === 'PerPortion'
    ? { unit: 'Portion', servingId: null, quantity: 1 }
    : { unit: baseUnit(food.basis), servingId: null, quantity: 100 };
}

interface Props {
  food: Pick<Food, 'basis' | 'servings' | 'portionName'>;
  value: QuantityChoice;
  onChange: (choice: QuantityChoice) => void;
}

export function QuantityPicker({ food, value, onChange }: Props) {
  const base = baseUnit(food.basis);
  const baseLabel = base === 'Gram' ? 'grams (g)' : base === 'Millilitre' ? 'millilitres (ml)' : food.portionName ? `portions (${food.portionName})` : 'portions';
  const selectValue = value.unit === 'Serving' ? `serving:${value.servingId}` : value.unit;

  const onUnit = (v: string) => {
    if (v.startsWith('serving:')) {
      onChange({ unit: 'Serving', servingId: v.slice(8), quantity: value.unit === 'Serving' ? value.quantity : 1 });
    } else {
      const unit = v as QuantityUnit;
      onChange({ unit, servingId: null, quantity: unit === 'Portion' ? 1 : value.unit === unit ? value.quantity : 100 });
    }
  };

  return (
    <div className="quantity-picker">
      <label className="field">
        <span>Amount</span>
        <input
          type="number"
          inputMode="decimal"
          min="0"
          step="any"
          value={Number.isFinite(value.quantity) ? value.quantity : ''}
          onChange={(e) => onChange({ ...value, quantity: e.target.value === '' ? NaN : Number(e.target.value) })}
          aria-label="Amount"
        />
      </label>
      <label className="field grow">
        <span>Unit</span>
        <select value={selectValue} onChange={(e) => onUnit(e.target.value)} aria-label="Unit">
          <option value={base}>{baseLabel}</option>
          {food.servings.map((s) => (
            <option key={s.id} value={`serving:${s.id}`}>
              {s.name}
              {base !== 'Portion' && !/\d\s*(g|ml)\b/i.test(s.name) ? ` (${s.amount} ${base === 'Gram' ? 'g' : 'ml'})` : ''}
            </option>
          ))}
        </select>
      </label>
    </div>
  );
}
