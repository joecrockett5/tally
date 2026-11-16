import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router';
import { api } from '../api/client';
import type { MealLayout } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { useToday } from '../profile/ConfigProvider';
import { useActiveProfile } from '../profile/ProfileProvider';

const MAX_MEALS = 12;
const MAX_NAME_LENGTH = 40;

/** `id` is absent for a meal that has been added here and not yet saved. */
interface Row {
  key: number;
  id?: string;
  name: string;
}

/** Add, rename, reorder and remove the active profile's meals. */
export function MealsPage() {
  const profile = useActiveProfile();
  const today = useToday();
  const navigate = useNavigate();
  const [rows, setRows] = useState<Row[] | null>(null);
  const [error, setError] = useState<Error | string>();
  const [busy, setBusy] = useState(false);
  const nextKey = useRef(0);
  const focusKey = useRef<number | null>(null);

  useEffect(() => {
    api
      .get<MealLayout>(`/api/meals?date=${today}`)
      .then((l) => setRows(l.meals.map((m) => ({ key: nextKey.current++, ...m }))))
      .catch(setError);
  }, [profile.id, today]);

  if (!rows) return error ? <ErrorNote error={error} /> : <p className="muted">Loading…</p>;

  const change = (next: Row[]) => {
    setRows(next);
    setError(undefined);
  };
  const move = (index: number, by: number) => {
    const next = [...rows];
    [next[index], next[index + by]] = [next[index + by], next[index]];
    change(next);
  };
  const add = () => {
    focusKey.current = nextKey.current;
    change([...rows, { key: nextKey.current++, name: '' }]);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const names = rows.map((r) => r.name.trim().replace(/\s+/g, ' '));
    if (names.some((n) => !n)) return setError('Give every meal a name, or remove it.');
    if (new Set(names.map((n) => n.toLowerCase())).size !== names.length) return setError('Each meal needs a different name.');
    setBusy(true);
    setError(undefined);
    try {
      await api.put<MealLayout>('/api/meals', { meals: rows.map((r, i) => ({ id: r.id, name: names[i] })) });
      navigate(`/diary/${today}`);
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  return (
    <form className="stack" onSubmit={submit}>
      <div className="page-header">
        <h1>Meals</h1>
        <Link className="button button-ghost button-small" to={`/diary/${today}`}>
          Cancel
        </Link>
      </div>
      <section className="card stack">
        <p className="small muted">
          Choose the meals {profile.name}'s diary is split into. Changes apply from today: earlier days keep the meals and names they
          had.
        </p>
        <ol className="meal-edit-list">
          {rows.map((row, i) => {
            const label = row.name.trim() || `meal ${i + 1}`;
            return (
              <li key={row.key} className="meal-edit-row">
                <input
                  className="input grow"
                  value={row.name}
                  maxLength={MAX_NAME_LENGTH}
                  placeholder="Meal name"
                  aria-label={`Name of meal ${i + 1}`}
                  ref={(el) => {
                    if (el && focusKey.current === row.key) {
                      focusKey.current = null;
                      el.focus();
                    }
                  }}
                  onChange={(e) => change(rows.map((r) => (r.key === row.key ? { ...r, name: e.target.value } : r)))}
                />
                <button type="button" className="icon-button" disabled={i === 0} onClick={() => move(i, -1)} aria-label={`Move ${label} up`}>
                  ↑
                </button>
                <button
                  type="button"
                  className="icon-button"
                  disabled={i === rows.length - 1}
                  onClick={() => move(i, 1)}
                  aria-label={`Move ${label} down`}
                >
                  ↓
                </button>
                <button
                  type="button"
                  className="icon-button"
                  disabled={rows.length === 1}
                  onClick={() => change(rows.filter((r) => r.key !== row.key))}
                  aria-label={`Remove ${label}`}
                >
                  ✕
                </button>
              </li>
            );
          })}
        </ol>
        <button type="button" className="button" disabled={rows.length >= MAX_MEALS} onClick={add}>
          + Add a meal
        </button>
        <p className="small muted">
          Removing a meal doesn't delete anything. Food already logged in it today stays on the day, where you can move it to
          another meal.
        </p>
      </section>
      <ErrorNote error={error} />
      <button className="button button-primary" disabled={busy}>
        Save meals
      </button>
    </form>
  );
}
