import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import type { Food, Nutrients, Recipe } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { NutritionTable } from '../components/NutritionTable';
import { amountFor, type QuantityChoice } from '../components/QuantityPicker';
import { describeQuantity, formatNutrient, nutrientsForAmount, scaleNutrients } from '../lib/nutrients';
import { FoodPickerSheet } from './FoodPickerSheet';

interface DraftIngredient {
  key: string;
  food: Food;
  choice: QuantityChoice;
}

/** Sum of known values; null when no ingredient knows the nutrient. */
export function sumNutrients(items: Nutrients[]): Nutrients {
  const keys = ['energyKcal', 'energyKj', 'fat', 'saturates', 'carbohydrate', 'sugars', 'fibre', 'protein', 'salt'] as const;
  return Object.fromEntries(
    keys.map((k) => [k, items.some((n) => n[k] !== null) ? items.reduce((s, n) => s + (n[k] ?? 0), 0) : null]),
  ) as unknown as Nutrients;
}

export function RecipeFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [name, setName] = useState('');
  const [servings, setServings] = useState('4');
  const [cookedWeight, setCookedWeight] = useState('');
  const [ingredients, setIngredients] = useState<DraftIngredient[]>([]);
  const [picking, setPicking] = useState(false);
  const [error, setError] = useState<Error | string>();
  const [busy, setBusy] = useState(false);
  const [loaded, setLoaded] = useState(!id);

  useEffect(() => {
    if (!id) return;
    api
      .get<Recipe>(`/api/recipes/${id}`)
      .then((r) => {
        setName(r.name);
        setServings(String(r.servings));
        setCookedWeight(r.cookedWeightGrams ? String(r.cookedWeightGrams) : '');
        setIngredients(
          r.ingredients.map((i) => ({
            key: i.id,
            food: i.food,
            choice: {
              unit: i.unit,
              quantity: i.quantity,
              servingId: i.unit === 'Serving' ? (i.food.servings.find((s) => s.name === i.servingName)?.id ?? null) : null,
            },
          })),
        );
        setLoaded(true);
      })
      .catch(setError);
  }, [id]);

  const servingCount = Number(servings);
  const total = sumNutrients(ingredients.map((i) => nutrientsForAmount(i.food.nutrients, i.food.basis, amountFor(i.food, i.choice))));
  const perServing = servingCount > 0 ? scaleNutrients(total, 1 / servingCount) : total;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!(servingCount > 0)) return setError('Servings must be above zero.');
    const weight = cookedWeight.trim() ? Number(cookedWeight.replace(',', '.')) : null;
    if (weight !== null && !(weight > 0)) return setError('Cooked weight must be above zero, or blank.');
    if (ingredients.some((i) => i.choice.unit === 'Serving' && !i.choice.servingId))
      return setError('One ingredient’s serving no longer exists; choose a new amount for it.');

    setBusy(true);
    try {
      const body = {
        name,
        servings: servingCount,
        cookedWeightGrams: weight,
        ingredients: ingredients.map((i) => ({ foodId: i.food.id, unit: i.choice.unit, quantity: i.choice.quantity, servingId: i.choice.servingId })),
      };
      const saved = id ? await api.put<Recipe>(`/api/recipes/${id}`, body) : await api.post<Recipe>('/api/recipes', body);
      navigate(`/recipes/${saved.id}`, { replace: true });
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  if (!loaded) return error ? <ErrorNote error={error} /> : <p className="muted">Loading…</p>;

  return (
    <form className="stack" onSubmit={submit}>
      <div className="page-header">
        <h1>{id ? 'Edit recipe' : 'New recipe'}</h1>
        <button type="button" className="button button-ghost button-small" onClick={() => navigate(-1)}>
          Cancel
        </button>
      </div>
      <section className="card stack">
        <label className="field">
          <span>Name</span>
          <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={200} />
        </label>
        <div className="field-row">
          <label className="field">
            <span>Servings it makes</span>
            <input inputMode="decimal" value={servings} onChange={(e) => setServings(e.target.value)} required />
          </label>
          <label className="field">
            <span>Cooked weight (g, optional)</span>
            <input inputMode="decimal" value={cookedWeight} onChange={(e) => setCookedWeight(e.target.value)} placeholder="lets you log by grams" />
          </label>
        </div>
      </section>

      <section className="card stack">
        <div className="row-between">
          <h2>Ingredients</h2>
          <button type="button" className="button button-small" onClick={() => setPicking(true)}>
            + Add ingredient
          </button>
        </div>
        {ingredients.length === 0 && <p className="hint">Add ingredients from any source — your foods, CoFID generics or Open Food Facts products.</p>}
        <ul className="entry-list">
          {ingredients.map((i) => (
            <li key={i.key} className="entry-row">
              <span className="grow">
                <span className="entry-name">{i.food.name}</span>
                <span className="entry-meta">
                  {describeQuantity({
                    unit: i.choice.unit,
                    quantity: i.choice.quantity,
                    servingName: i.food.servings.find((s) => s.id === i.choice.servingId)?.name,
                    portionName: i.food.portionName,
                    amount: amountFor(i.food, i.choice),
                    basis: i.food.basis,
                  })}
                </span>
              </span>
              <span className="num entry-kcal">
                {formatNutrient(nutrientsForAmount(i.food.nutrients, i.food.basis, amountFor(i.food, i.choice)).energyKcal, 'energyKcal')}
              </span>
              <button type="button" className="icon-button" aria-label={`Remove ${i.food.name}`} onClick={() => setIngredients(ingredients.filter((x) => x.key !== i.key))}>
                ✕
              </button>
            </li>
          ))}
        </ul>
      </section>

      {ingredients.length > 0 && (
        <section className="card">
          <NutritionTable nutrients={perServing} caption={`Per serving (total ${formatNutrient(total.energyKcal, 'energyKcal')} kcal)`} />
        </section>
      )}

      <ErrorNote error={error} />
      <button className="button button-primary" disabled={busy || !name.trim() || ingredients.length === 0}>
        {id ? 'Save recipe' : 'Create recipe'}
      </button>

      {picking && (
        <FoodPickerSheet
          title="Add ingredient"
          onClose={() => setPicking(false)}
          onPick={(food, choice) => {
            setIngredients([...ingredients, { key: `${food.id}-${Date.now()}`, food, choice }]);
            setPicking(false);
          }}
        />
      )}
    </form>
  );
}
