import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import { MEALS, type BarcodeLookup, type Food, type Meal, type QuickPick, type Recipe, type RecipeSummary, type SavedMeal } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { FoodRow } from '../components/FoodRow';
import { FoodSearchResults } from '../components/FoodSearchResults';
import type { QuantityChoice } from '../components/QuantityPicker';
import { describeQuantity, formatNutrient } from '../lib/nutrients';
import { useApi } from '../lib/useApi';
import { useFoodSearch } from '../lib/useFoodSearch';
import { LogRecipeSheet } from '../recipes/LogRecipeSheet';
import { LogFoodSheet } from './LogFoodSheet';
import { Scanner, type ScanResult } from './Scanner';

type QuickTab = 'recent' | 'frequent' | 'recipes' | 'meals';

const TABS: [QuickTab, string][] = [
  ['recent', 'Recent'],
  ['frequent', 'Frequent'],
  ['recipes', 'Recipes'],
  ['meals', 'Saved meals'],
];

export function AddFoodPage() {
  const { date = '', meal: mealParam } = useParams();
  const meal = (MEALS.find((m) => m.toLowerCase() === mealParam?.toLowerCase()) ?? 'Snacks') as Meal;
  const navigate = useNavigate();

  const [q, setQ] = useState('');
  const [tab, setTab] = useState<QuickTab>('recent');
  const [selected, setSelected] = useState<{ food: Food; initial?: QuantityChoice } | null>(null);
  const [recipe, setRecipe] = useState<Recipe | null>(null);
  const [scanning, setScanning] = useState(false);
  const [scanOutcome, setScanOutcome] = useState<BarcodeLookup | null>(null);
  const [error, setError] = useState<Error>();

  const search = useFoodSearch(q);
  // Tagged with its tab so a stale list is never rendered as another tab's type.
  const quickData = useApi<{ tab: QuickTab; items: unknown[] }>(
    async (s) => ({
      tab,
      items:
        tab === 'recipes'
          ? await api.get<RecipeSummary[]>('/api/recipes', s)
          : tab === 'meals'
            ? await api.get<SavedMeal[]>('/api/saved-meals', s)
            : await api.get<QuickPick[]>(`/api/foods/${tab}`, s),
    }),
    [tab],
  );
  const quick = { ...quickData, data: quickData.data?.tab === tab ? quickData.data.items : undefined };
  const searching = q.trim().length > 0;
  const backToDiary = () => navigate(`/diary/${date}`);

  const onScan = async ({ code, format }: ScanResult) => {
    setScanning(false);
    setError(undefined);
    setScanOutcome(null);
    try {
      const result = await api.get<BarcodeLookup>(`/api/foods/barcode/${encodeURIComponent(code)}${format ? `?format=${format}` : ''}`);
      if (result.status === 'found' && result.food) setSelected({ food: result.food });
      else setScanOutcome(result);
    } catch (err) {
      setError(err as Error);
    }
  };

  const openRecipe = async (id: string) => {
    try {
      setRecipe(await api.get<Recipe>(`/api/recipes/${id}`));
    } catch (err) {
      setError(err as Error);
    }
  };

  const logSavedMeal = async (m: SavedMeal) => {
    try {
      await api.post(`/api/saved-meals/${m.id}/log`, { date, meal });
      backToDiary();
    } catch (err) {
      setError(err as Error);
    }
  };

  return (
    <div className="stack">
      <div className="page-header">
        <h1>Add to {meal}</h1>
        <Link className="button button-ghost button-small" to={`/diary/${date}`}>
          Done
        </Link>
      </div>

      <div className="search-bar">
        <input
          type="search"
          className="input grow"
          placeholder="Search foods, brands, restaurants…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          aria-label="Search foods"
          autoFocus
        />
        <button type="button" className="button button-primary" onClick={() => setScanning(true)} aria-label="Scan barcode">
          ▥ Scan
        </button>
      </div>

      <ErrorNote error={error} />
      {scanOutcome && (
        <div className="card stack" role="status">
          <p>
            <strong>
              {scanOutcome.status === 'not_found' ? `Barcode ${scanOutcome.barcode} wasn't found.` : `Couldn't look up ${scanOutcome.barcode}.`}
            </strong>{' '}
            {scanOutcome.message ?? 'Online results are unavailable.'}
          </p>
          <Link className={`button${scanOutcome.status === 'not_found' ? ' button-primary' : ''}`} to={`/foods/new?barcode=${scanOutcome.barcode}`}>
            {scanOutcome.status === 'not_found' ? 'Add this product' : 'Add it manually'}
          </Link>
        </div>
      )}

      {searching ? (
        <section className="card">
          <FoodSearchResults
            search={search}
            query={q}
            onSelect={(food) => setSelected({ food })}
            empty={
              <Link className="button" to={`/foods/new?name=${encodeURIComponent(q.trim())}`}>
                Create a food
              </Link>
            }
          />
        </section>
      ) : (
        <section className="card">
          <div className="chips" role="tablist">
            {TABS.map(([t, label]) => (
              <button key={t} type="button" role="tab" className="chip" aria-selected={tab === t} aria-pressed={tab === t} onClick={() => setTab(t)}>
                {label}
              </button>
            ))}
          </div>
          <ErrorNote error={quick.error} onRetry={quick.reload} />
          {!quick.loading && quick.data?.length === 0 && (
            <p className="empty">
              {tab === 'recipes' ? (
                <>No recipes yet. <Link to="/recipes/new">Create one</Link>.</>
              ) : tab === 'meals' ? (
                'No saved meals yet. Use “Save as meal” on a meal in your diary.'
              ) : (
                'Nothing yet. Search for a food or scan a barcode to get started.'
              )}
            </p>
          )}
          <ul className="food-list">
            {tab === 'recipes'
              ? (quick.data as RecipeSummary[] | undefined)?.map((r) => (
                  <li key={r.id}>
                    <button type="button" className="food-row" onClick={() => openRecipe(r.id)}>
                      <span className="grow">
                        <span className="food-row-name">{r.name}</span>
                        <span className="food-row-detail">
                          {r.ingredientCount} ingredient{r.ingredientCount === 1 ? '' : 's'} · makes {r.servings}
                        </span>
                      </span>
                      <span className="food-row-energy num">
                        <strong>{formatNutrient(r.perServing.energyKcal, 'energyKcal')}</strong>
                        <span className="small muted">kcal /serving</span>
                      </span>
                    </button>
                  </li>
                ))
              : tab === 'meals'
                ? (quick.data as SavedMeal[] | undefined)?.map((m) => (
                    <li key={m.id}>
                      <button type="button" className="food-row" onClick={() => logSavedMeal(m)} aria-label={`Log ${m.name}`}>
                        <span className="grow">
                          <span className="food-row-name">{m.name}</span>
                          <span className="food-row-detail truncate">{m.items.map((i) => i.name).join(', ')}</span>
                        </span>
                        <span className="food-row-energy num">
                          <strong>{formatNutrient(m.totals.values.energyKcal, 'energyKcal')}</strong>
                          <span className="small muted">kcal · tap to add</span>
                        </span>
                      </button>
                    </li>
                  ))
                : (quick.data as QuickPick[] | undefined)?.map((p) => (
                    <FoodRow
                      key={p.food.id}
                      food={p.food}
                      detail={`Last: ${describeQuantity({
                        unit: p.unit,
                        quantity: p.quantity,
                        servingName: p.food.servings.find((s) => s.id === p.servingId)?.name,
                        portionName: p.food.portionName,
                      })}${tab === 'frequent' ? ` · logged ${p.timesLogged}×` : ''}`}
                      onSelect={() => setSelected({ food: p.food, initial: { unit: p.unit, quantity: p.quantity, servingId: p.servingId } })}
                    />
                  ))}
          </ul>
        </section>
      )}

      {scanning && <Scanner onDetected={onScan} onClose={() => setScanning(false)} />}
      {selected && (
        <LogFoodSheet food={selected.food} initial={selected.initial} date={date} meal={meal} onClose={() => setSelected(null)} onLogged={backToDiary} />
      )}
      {recipe && <LogRecipeSheet recipe={recipe} date={date} meal={meal} onClose={() => setRecipe(null)} onLogged={backToDiary} />}
    </div>
  );
}
