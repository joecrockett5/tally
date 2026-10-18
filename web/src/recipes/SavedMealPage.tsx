import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import { MEALS, type Meal, type SavedMeal } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { NutritionTable } from '../components/NutritionTable';
import { SourceBadge } from '../components/SourceBadge';
import { mealForHour } from '../lib/dates';
import { describeQuantity, formatNutrient } from '../lib/nutrients';
import { useApi } from '../lib/useApi';
import { useToday } from '../profile/ConfigProvider';

export function SavedMealPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const today = useToday();
  const meal = useApi((s) => api.get<SavedMeal>(`/api/saved-meals/${id}`, s), [id]);
  const [target, setTarget] = useState<Meal>(mealForHour(new Date().getHours()));
  const [date, setDate] = useState(today);
  const [error, setError] = useState<Error>();
  const [busy, setBusy] = useState(false);

  if (meal.error) return <ErrorNote error={meal.error} onRetry={meal.reload} />;
  const m = meal.data;
  if (!m) return <p className="muted">Loading…</p>;

  const run = async (fn: () => Promise<void>) => {
    setBusy(true);
    try {
      await fn();
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  return (
    <div className="stack">
      <div className="page-header">
        <Link className="button button-ghost button-small" to="/recipes?tab=meals">
          ‹ Saved meals
        </Link>
      </div>
      <section className="card stack">
        <h1>{m.name}</h1>
        <ul className="entry-list">
          {m.items.map((i) => (
            <li key={i.id} className="entry-row">
              <span className="grow">
                <span className="entry-name">{i.name}</span>
                <span className="entry-meta row">
                  <SourceBadge source={i.source} />
                  {describeQuantity({ ...i, amount: i.servingAmount ? i.servingAmount * i.quantity : undefined })}
                </span>
              </span>
              <span className="num entry-kcal">{formatNutrient(i.nutrients.energyKcal, 'energyKcal')}</span>
            </li>
          ))}
        </ul>
        <NutritionTable nutrients={m.totals.values} incomplete={m.totals.incomplete} caption="Whole meal (current values)" />
      </section>
      <section className="card stack">
        <h2>Log this meal</h2>
        <div className="field-row">
          <label className="field">
            <span>Date</span>
            <input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </label>
          <label className="field">
            <span>Meal</span>
            <select value={target} onChange={(e) => setTarget(e.target.value as Meal)}>
              {MEALS.map((x) => (
                <option key={x}>{x}</option>
              ))}
            </select>
          </label>
        </div>
        <button
          type="button"
          className="button button-primary"
          disabled={busy || !date}
          onClick={() =>
            run(async () => {
              await api.post(`/api/saved-meals/${m.id}/log`, { date, meal: target });
              navigate(`/diary/${date}`);
            })
          }
        >
          Add to {target}
        </button>
      </section>
      <ErrorNote error={error} />
      <button
        type="button"
        className="link-button danger-link"
        disabled={busy}
        onClick={() =>
          run(async () => {
            await api.del(`/api/saved-meals/${m.id}`);
            navigate('/recipes?tab=meals');
          })
        }
      >
        Delete saved meal
      </button>
    </div>
  );
}
