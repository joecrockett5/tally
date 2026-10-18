import { useEffect, useState, type FormEvent } from 'react';
import { api } from '../api/client';
import type { Goals } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { NUTRIENTS } from '../lib/nutrients';
import { useActiveProfile } from '../profile/ProfileProvider';

type GoalText = Record<keyof Goals, string>;

const toText = (g: Goals): GoalText =>
  Object.fromEntries(Object.entries(g).map(([k, v]) => [k, v === null ? '' : String(v)])) as GoalText;

export function GoalsPage() {
  const profile = useActiveProfile();
  const [values, setValues] = useState<GoalText | null>(null);
  const [error, setError] = useState<Error | string>();
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.get<Goals>('/api/goals').then((g) => setValues(toText(g))).catch(setError);
  }, [profile.id]);

  const resetDefaults = async () => {
    setValues(toText(await api.get<Goals>('/api/goals/defaults')));
    setSaved(false);
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!values) return;
    const body: Partial<Record<keyof Goals, number | null>> = {};
    for (const [k, v] of Object.entries(values) as [keyof Goals, string][]) {
      const t = v.trim().replace(',', '.');
      if (t === '') body[k] = null;
      else if (Number(t) > 0) body[k] = Number(t);
      else return setError('Targets must be above zero, or blank for no target.');
    }
    setBusy(true);
    setError(undefined);
    try {
      setValues(toText(await api.put<Goals>('/api/goals', body)));
      setSaved(true);
    } catch (err) {
      setError(err as Error);
    } finally {
      setBusy(false);
    }
  };

  if (!values) return error ? <ErrorNote error={error} /> : <p className="muted">Loading…</p>;

  return (
    <form className="stack" onSubmit={submit}>
      <div className="page-header">
        <h1>Daily goals</h1>
      </div>
      <section className="card stack">
        <p className="small muted">
          Defaults are the UK Reference Intakes for an average adult, plus the recommended 30 g of fibre. Sugars, saturates and salt are
          upper limits — staying under them is the aim. Leave a box blank for no target.
        </p>
        <div className="goal-list">
          {NUTRIENTS.map((n) => (
            <label key={n.key} className={`goal-row${n.indent ? ' indent' : ''}`}>
              <span className="grow">
                {n.label}
                <span className={`badge ${n.isLimit ? 'badge-limit' : 'badge-target'}`}>{n.isLimit ? 'limit' : 'target'}</span>
              </span>
              <input
                className="input goal-input"
                inputMode="decimal"
                value={values[n.key]}
                placeholder="none"
                onChange={(e) => {
                  setValues({ ...values, [n.key]: e.target.value });
                  setSaved(false);
                }}
                aria-label={`${n.label} (${n.unit})`}
              />
              <span className="goal-unit muted small">{n.unit}</span>
            </label>
          ))}
        </div>
      </section>
      <ErrorNote error={error} />
      {saved && (
        <p className="note" role="status">
          Goals saved.
        </p>
      )}
      <div className="button-row">
        <button type="button" className="button" onClick={resetDefaults}>
          Reset to UK defaults
        </button>
        <button className="button button-primary" disabled={busy}>
          Save goals
        </button>
      </div>
    </form>
  );
}
