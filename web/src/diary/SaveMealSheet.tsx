import { useState, type FormEvent } from 'react';
import { api } from '../api/client';
import type { Meal } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { Sheet } from '../components/Sheet';

interface Props {
  date: string;
  meal: Meal;
  onClose: () => void;
  onSaved: (name: string) => void;
}

export function SaveMealSheet({ date, meal, onClose, onSaved }: Props) {
  const [name, setName] = useState(`Usual ${meal.toLowerCase()}`);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<Error>();

  const save = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      await api.post('/api/saved-meals/from-diary', { name, date, meal });
      onSaved(name);
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  return (
    <Sheet title={`Save ${meal.toLowerCase()} as a meal`} onClose={onClose}>
      <form className="stack" onSubmit={save}>
        <label className="field">
          <span>Name</span>
          <input autoFocus value={name} onChange={(e) => setName(e.target.value)} maxLength={200} required />
        </label>
        <p className="hint">Log the same foods again later in one tap from “Saved meals”.</p>
        <ErrorNote error={error} />
        <button className="button button-primary" disabled={busy || !name.trim()}>
          Save meal
        </button>
      </form>
    </Sheet>
  );
}
