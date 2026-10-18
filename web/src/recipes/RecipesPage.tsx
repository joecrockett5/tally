import { Link, useSearchParams } from 'react-router';
import { api } from '../api/client';
import type { RecipeSummary, SavedMeal } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { formatNutrient } from '../lib/nutrients';
import { useApi } from '../lib/useApi';
import { useActiveProfile } from '../profile/ProfileProvider';

export function RecipesPage() {
  const profile = useActiveProfile();
  const [params, setParams] = useSearchParams();
  const tab = params.get('tab') === 'meals' ? 'meals' : 'recipes';
  const recipes = useApi((s) => api.get<RecipeSummary[]>('/api/recipes', s), [profile.id]);
  const meals = useApi((s) => api.get<SavedMeal[]>('/api/saved-meals', s), [profile.id]);

  return (
    <div className="stack">
      <div className="page-header">
        <h1>{tab === 'recipes' ? 'Recipes' : 'Saved meals'}</h1>
        {tab === 'recipes' && (
          <Link className="button button-primary button-small" to="/recipes/new">
            + New recipe
          </Link>
        )}
      </div>
      <div className="chips" role="tablist">
        <button type="button" role="tab" className="chip" aria-selected={tab === 'recipes'} aria-pressed={tab === 'recipes'} onClick={() => setParams({}, { replace: true })}>
          Recipes
        </button>
        <button type="button" role="tab" className="chip" aria-selected={tab === 'meals'} aria-pressed={tab === 'meals'} onClick={() => setParams({ tab: 'meals' }, { replace: true })}>
          Saved meals
        </button>
      </div>

      {tab === 'recipes' ? (
        <section className="card">
          <ErrorNote error={recipes.error} onRetry={recipes.reload} />
          {recipes.data?.length === 0 && <p className="empty">No recipes yet. Build one from any foods, then log a serving in one tap.</p>}
          <ul className="food-list">
            {recipes.data?.map((r) => (
              <li key={r.id}>
                <Link className="food-row" to={`/recipes/${r.id}`}>
                  <span className="grow">
                    <span className="food-row-name">{r.name}</span>
                    <span className="food-row-detail">
                      {r.ingredientCount} ingredient{r.ingredientCount === 1 ? '' : 's'} · makes {r.servings}
                      {r.cookedWeightGrams ? ` · ${r.cookedWeightGrams} g cooked` : ''}
                    </span>
                  </span>
                  <span className="food-row-energy num">
                    <strong>{formatNutrient(r.perServing.energyKcal, 'energyKcal')}</strong>
                    <span className="small muted">kcal /serving</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      ) : (
        <section className="card">
          <ErrorNote error={meals.error} onRetry={meals.reload} />
          {meals.data?.length === 0 && <p className="empty">No saved meals yet. Open a meal's ⋯ menu in your diary and choose “Save as meal”.</p>}
          <ul className="food-list">
            {meals.data?.map((m) => (
              <li key={m.id}>
                <Link className="food-row" to={`/meals/${m.id}`}>
                  <span className="grow">
                    <span className="food-row-name">{m.name}</span>
                    <span className="food-row-detail truncate">{m.items.map((i) => i.name).join(', ')}</span>
                  </span>
                  <span className="food-row-energy num">
                    <strong>{formatNutrient(m.totals.values.energyKcal, 'energyKcal')}</strong>
                    <span className="small muted">kcal</span>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
