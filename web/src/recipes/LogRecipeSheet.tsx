import { useState } from 'react';
import { api } from '../api/client';
import { MEALS, type Entry, type Meal, type Recipe } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { NutritionTable } from '../components/NutritionTable';
import { Sheet } from '../components/Sheet';
import { formatNutrient, scaleNutrients } from '../lib/nutrients';

interface Props {
  recipe: Recipe;
  date: string;
  meal: Meal;
  onClose: () => void;
  onLogged: (entry: Entry) => void;
}

export function LogRecipeSheet({ recipe, date, meal: initialMeal, onClose, onLogged }: Props) {
  const [byGrams, setByGrams] = useState(false);
  const [quantity, setQuantity] = useState(1);
  const [meal, setMeal] = useState<Meal>(initialMeal);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<Error>();

  const valid = Number.isFinite(quantity) && quantity > 0;
  const preview = byGrams && recipe.per100g
    ? scaleNutrients(recipe.per100g, valid ? quantity / 100 : 0)
    : scaleNutrients(recipe.perServing, valid ? quantity : 0);

  const log = async () => {
    setBusy(true);
    try {
      onLogged(await api.post<Entry>('/api/diary/entries', { date, meal, recipeId: recipe.id, unit: byGrams ? 'Gram' : 'Portion', quantity }));
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  return (
    <Sheet
      title="Add recipe to diary"
      onClose={onClose}
      footer={
        <button type="button" className="button button-primary" disabled={!valid || busy} onClick={log}>
          Add to {meal} · {formatNutrient(preview.energyKcal, 'energyKcal')} kcal
        </button>
      }
    >
      <div className="stack">
        <h3>{recipe.name}</h3>
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
            <select
              value={byGrams ? 'g' : 'serving'}
              onChange={(e) => {
                const grams = e.target.value === 'g';
                setByGrams(grams);
                setQuantity(grams ? Math.round((recipe.cookedWeightGrams ?? 0) / recipe.servings) || 100 : 1);
              }}
            >
              <option value="serving">servings (of {recipe.servings})</option>
              {recipe.cookedWeightGrams && <option value="g">grams (cooked)</option>}
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
        <NutritionTable nutrients={preview} incomplete={recipe.totals.incomplete} caption="For this amount" />
        <ErrorNote error={error} />
      </div>
    </Sheet>
  );
}
