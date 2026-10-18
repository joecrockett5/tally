import { useState } from 'react';
import { api } from '../api/client';
import { MEALS, type Entry, type Food, type Meal } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { NutritionTable } from '../components/NutritionTable';
import { amountFor, defaultChoice, QuantityPicker, type QuantityChoice } from '../components/QuantityPicker';
import { Sheet } from '../components/Sheet';
import { SourceBadge } from '../components/SourceBadge';
import { basisLabel, formatNutrient, nutrientsForAmount } from '../lib/nutrients';

interface Props {
  food: Food;
  date: string;
  meal: Meal;
  initial?: QuantityChoice;
  onClose: () => void;
  onLogged: (entry: Entry) => void;
}

export function LogFoodSheet({ food, date, meal: initialMeal, initial, onClose, onLogged }: Props) {
  const [choice, setChoice] = useState<QuantityChoice>(initial ?? defaultChoice(food));
  const [meal, setMeal] = useState<Meal>(initialMeal);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<Error>();

  const amount = amountFor(food, choice);
  const valid = Number.isFinite(choice.quantity) && choice.quantity > 0 && amount > 0;
  const preview = nutrientsForAmount(food.nutrients, food.basis, valid ? amount : 0);

  const log = async () => {
    setBusy(true);
    try {
      const entry = await api.post<Entry>('/api/diary/entries', {
        date,
        meal,
        foodId: food.id,
        unit: choice.unit,
        quantity: choice.quantity,
        servingId: choice.servingId,
      });
      onLogged(entry);
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  return (
    <Sheet
      title="Add to diary"
      onClose={onClose}
      footer={
        <button type="button" className="button button-primary" disabled={!valid || busy} onClick={log}>
          Add to {meal} · {formatNutrient(preview.energyKcal, 'energyKcal')} kcal
        </button>
      }
    >
      <div className="stack">
        <div>
          <h3>{food.name}</h3>
          <p className="row small muted">
            <SourceBadge source={food.source} />
            {(food.restaurant ?? food.brand) && <span>{food.restaurant ?? food.brand}</span>}
          </p>
        </div>
        <QuantityPicker food={food} value={choice} onChange={setChoice} />
        <label className="field">
          <span>Meal</span>
          <select value={meal} onChange={(e) => setMeal(e.target.value as Meal)}>
            {MEALS.map((m) => (
              <option key={m}>{m}</option>
            ))}
          </select>
        </label>
        <NutritionTable nutrients={preview} caption="For this amount" />
        <p className="small muted">
          Food values {basisLabel(food.basis, food.portionName)}: {formatNutrient(food.nutrients.energyKcal, 'energyKcal')} kcal.
        </p>
        {food.carbsMayIncludeFibre && (
          <p className="note note-warn small">
            This product is sold in North America, where carbohydrate on labels usually includes fibre. Values are shown
            as published.
          </p>
        )}
        {food.source === 'off' && <OffAttribution />}
        <ErrorNote error={error} />
      </div>
    </Sheet>
  );
}

export function OffAttribution() {
  return (
    <p className="small muted attribution">
      Product data from{' '}
      <a href="https://world.openfoodfacts.org" target="_blank" rel="noreferrer">
        Open Food Facts
      </a>
      , available under the{' '}
      <a href="https://opendatacommons.org/licenses/odbl/1-0/" target="_blank" rel="noreferrer">
        Open Database Licence
      </a>
      .
    </p>
  );
}
