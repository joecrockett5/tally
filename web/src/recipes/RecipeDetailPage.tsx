import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import type { Recipe } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { NutritionTable } from '../components/NutritionTable';
import { SourceBadge } from '../components/SourceBadge';
import { mealForHour } from '../lib/dates';
import { describeQuantity, formatNutrient } from '../lib/nutrients';
import { useApi } from '../lib/useApi';
import { useToday } from '../profile/ConfigProvider';
import { LogRecipeSheet } from './LogRecipeSheet';

export function RecipeDetailPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const today = useToday();
  const recipe = useApi((s) => api.get<Recipe>(`/api/recipes/${id}`, s), [id]);
  const [logging, setLogging] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [error, setError] = useState<Error>();

  if (recipe.error) return <ErrorNote error={recipe.error} onRetry={recipe.reload} />;
  const r = recipe.data;
  if (!r) return <p className="muted">Loading…</p>;

  const remove = async () => {
    try {
      await api.del(`/api/recipes/${r.id}`);
      navigate('/recipes');
    } catch (err) {
      setError(err as Error);
    }
  };

  return (
    <div className="stack">
      <div className="page-header">
        <Link className="button button-ghost button-small" to="/recipes">
          ‹ Recipes
        </Link>
      </div>
      <section className="card stack">
        <div>
          <h1>{r.name}</h1>
          <p className="small muted">
            Makes {r.servings} serving{r.servings === 1 ? '' : 's'}
            {r.cookedWeightGrams ? ` · ${r.cookedWeightGrams} g cooked (${Math.round(r.cookedWeightGrams / r.servings)} g per serving)` : ''}
          </p>
        </div>
        <NutritionTable nutrients={r.perServing} incomplete={r.totals.incomplete} caption="Per serving" />
        {r.per100g && <p className="small muted">Per 100 g cooked: {formatNutrient(r.per100g.energyKcal, 'energyKcal')} kcal.</p>}
        <div className="button-row">
          <button type="button" className="button button-primary" onClick={() => setLogging(true)}>
            Add to diary
          </button>
          <Link className="button" to={`/recipes/${r.id}/edit`}>
            Edit
          </Link>
        </div>
      </section>
      <section className="card">
        <h2>Ingredients</h2>
        <ul className="entry-list">
          {r.ingredients.map((i) => (
            <li key={i.id} className="entry-row">
              <span className="grow">
                <span className="entry-name">{i.food.name}</span>
                <span className="entry-meta row">
                  <SourceBadge source={i.food.source} />
                  {describeQuantity({ ...i, portionName: i.food.portionName, basis: i.food.basis })}
                </span>
              </span>
              <span className="num entry-kcal">{formatNutrient(i.nutrients.energyKcal, 'energyKcal')}</span>
            </li>
          ))}
        </ul>
        <p className="small muted">Total: {formatNutrient(r.totals.values.energyKcal, 'energyKcal')} kcal. Recipe values follow the ingredients; diary entries keep the values from when they were logged.</p>
      </section>
      <ErrorNote error={error} />
      {confirmDelete ? (
        <div className="note note-error">
          <span>Delete this recipe? Diary entries stay.</span>
          <span className="row">
            <button type="button" className="link-button" onClick={() => setConfirmDelete(false)}>
              Cancel
            </button>
            <button type="button" className="button button-danger button-small" onClick={remove}>
              Delete
            </button>
          </span>
        </div>
      ) : (
        <button type="button" className="link-button danger-link" onClick={() => setConfirmDelete(true)}>
          Delete recipe
        </button>
      )}
      {logging && <LogRecipeSheet recipe={r} date={today} meal={mealForHour(new Date().getHours())} onClose={() => setLogging(false)} onLogged={(e) => navigate(`/diary/${e.date}`)} />}
    </div>
  );
}
