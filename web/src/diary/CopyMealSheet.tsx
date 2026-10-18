import { useState } from 'react';
import { api } from '../api/client';
import type { Meal } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { Sheet } from '../components/Sheet';
import { addDays, formatDayLabel } from '../lib/dates';

interface Props {
  date: string;
  meal: Meal;
  onClose: () => void;
  onCopied: (count: number) => void;
}

export function CopyMealSheet({ date, meal, onClose, onCopied }: Props) {
  const [from, setFrom] = useState(addDays(date, -1));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<Error>();

  const copy = async () => {
    setBusy(true);
    try {
      const { copied } = await api.post<{ copied: number }>('/api/diary/copy', {
        fromDate: from,
        toDate: date,
        fromMeal: meal,
        toMeal: meal,
      });
      onCopied(copied);
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  return (
    <Sheet
      title={`Copy ${meal.toLowerCase()} from…`}
      onClose={onClose}
      footer={
        <button type="button" className="button button-primary" disabled={busy || !from || from === date} onClick={copy}>
          Copy {meal.toLowerCase()} from {from ? formatDayLabel(from) : '…'}
        </button>
      }
    >
      <div className="stack">
        <label className="field">
          <span>Date to copy from</span>
          <input type="date" value={from} max={addDays(date, 365)} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <ErrorNote error={error} />
      </div>
    </Sheet>
  );
}
