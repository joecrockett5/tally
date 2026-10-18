import { useState } from 'react';
import { api } from '../api/client';
import { MEALS, type Entry, type Meal, type QuantityUnit } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { NutritionTable } from '../components/NutritionTable';
import { Sheet } from '../components/Sheet';
import { SourceBadge } from '../components/SourceBadge';
import { formatNutrient, nutrientsForAmount } from '../lib/nutrients';

interface Props {
  entry: Entry;
  onClose: () => void;
  onChanged: () => void;
}

/** Edit an entry's quantity or meal. Values come from the entry's own snapshot. */
export function EntrySheet({ entry, onClose, onChanged }: Props) {
  const baseUnit: QuantityUnit = entry.basis === 'PerPortion' ? 'Portion' : entry.basis === 'Per100ml' ? 'Millilitre' : 'Gram';
  const [unit, setUnit] = useState<QuantityUnit>(entry.unit);
  const [quantity, setQuantity] = useState<number>(entry.quantity);
  const [meal, setMeal] = useState<Meal>(entry.meal);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<Error>();

  const amount = unit === 'Serving' ? (entry.servingAmount ?? 0) * quantity : quantity;
  const valid = Number.isFinite(quantity) && quantity > 0;
  const preview = nutrientsForAmount(entry.perBasis, entry.basis, valid ? amount : 0);

  const changeUnit = (u: QuantityUnit) => {
    // Keep the same amount when switching between serving and base units.
    if (u === unit) return;
    if (u === 'Serving' && entry.servingAmount) setQuantity(Math.round((amount / entry.servingAmount) * 100) / 100);
    else setQuantity(Math.round(amount * 10) / 10);
    setUnit(u);
  };

  const run = async (action: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await action();
      onChanged();
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  const unitLabel = (u: QuantityUnit) =>
    u === 'Gram' ? 'grams (g)' : u === 'Millilitre' ? 'millilitres (ml)' : u === 'Portion' ? entry.portionName ?? 'portions' : entry.servingName ?? 'servings';

  return (
    <Sheet
      title="Edit entry"
      onClose={onClose}
      footer={
        <>
          <button type="button" className="button button-danger" disabled={busy} onClick={() => run(() => api.del(`/api/diary/entries/${entry.id}`))}>
            Delete
          </button>
          <button
            type="button"
            className="button button-primary"
            disabled={busy || !valid}
            onClick={() => run(() => api.put(`/api/diary/entries/${entry.id}`, { meal, unit, quantity }))}
          >
            Save · {formatNutrient(preview.energyKcal, 'energyKcal')} kcal
          </button>
        </>
      }
    >
      <div className="stack">
        <div>
          <h3>{entry.name}</h3>
          <p className="row small muted">
            <SourceBadge source={entry.source} />
            {entry.brand && <span>{entry.brand}</span>}
          </p>
        </div>
        <div className="quantity-picker">
          <label className="field">
            <span>Amount</span>
            <input
              type="number"
              inputMode="decimal"
              min="0"
              step="any"
              value={Number.isFinite(quantity) ? quantity : ''}
              onChange={(e) => setQuantity(e.target.value === '' ? NaN : Number(e.target.value))}
            />
          </label>
          <label className="field grow">
            <span>Unit</span>
            <select value={unit} onChange={(e) => changeUnit(e.target.value as QuantityUnit)}>
              <option value={baseUnit}>{unitLabel(baseUnit)}</option>
              {entry.servingAmount && <option value="Serving">{unitLabel('Serving')}</option>}
            </select>
          </label>
        </div>
        <label className="field">
          <span>Meal</span>
          <select value={meal} onChange={(e) => setMeal(e.target.value as Meal)}>
            {MEALS.map((m) => (
              <option key={m}>{m}</option>
            ))}
          </select>
        </label>
        <NutritionTable nutrients={preview} caption="For this amount" />
        <p className="small muted">Nutrition is as it was when you logged this, even if the food has changed since.</p>
        <ErrorNote error={error} />
      </div>
    </Sheet>
  );
}
