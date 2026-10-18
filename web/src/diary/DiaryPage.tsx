import { useState } from 'react';
import { Link, Navigate, useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import type { Day, Entry, Meal } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { addDays, formatDayLabel, isIsoDate, relativeDayName } from '../lib/dates';
import { describeQuantity, formatNutrient } from '../lib/nutrients';
import { useApi } from '../lib/useApi';
import { useToday } from '../profile/ConfigProvider';
import { useActiveProfile } from '../profile/ProfileProvider';
import { CopyMealSheet } from './CopyMealSheet';
import { DayTotals } from './DayTotals';
import { EntrySheet } from './EntrySheet';
import { SaveMealSheet } from './SaveMealSheet';

export function DiaryPage() {
  const { date: param } = useParams();
  const today = useToday();
  if (!isIsoDate(param)) return <Navigate to={`/diary/${today}`} replace />;
  return <DiaryDay date={param} today={today} />;
}

function DiaryDay({ date, today }: { date: string; today: string }) {
  const profile = useActiveProfile();
  const navigate = useNavigate();
  const day = useApi((s) => api.get<Day>(`/api/diary/${date}`, s), [date, profile.id]);
  const [editing, setEditing] = useState<Entry | null>(null);
  const [copyMeal, setCopyMeal] = useState<Meal | null>(null);
  const [saveMeal, setSaveMeal] = useState<Meal | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const go = (d: string) => {
    setNotice(null);
    navigate(`/diary/${d}`);
  };

  const copyFromYesterday = async (meal: Meal) => {
    const { copied } = await api.post<{ copied: number }>('/api/diary/copy', {
      fromDate: addDays(date, -1),
      toDate: date,
      fromMeal: meal,
      toMeal: meal,
    });
    setNotice(copied ? `Copied ${copied} item${copied === 1 ? '' : 's'} from yesterday's ${meal.toLowerCase()}.` : `Nothing was logged for ${meal.toLowerCase()} yesterday.`);
    day.reload();
  };

  const relative = relativeDayName(date, today);

  return (
    <div className="stack">
      <nav className="date-nav" aria-label="Choose day">
        <button type="button" className="icon-button" onClick={() => go(addDays(date, -1))} aria-label="Previous day">
          ‹
        </button>
        <label className="date-nav-label">
          <span className="date-nav-main">{relative ?? formatDayLabel(date)}</span>
          {relative && <span className="small muted">{formatDayLabel(date)}</span>}
          <input
            type="date"
            className="date-nav-input"
            value={date}
            onChange={(e) => isIsoDate(e.target.value) && go(e.target.value)}
            aria-label="Pick a date"
          />
        </label>
        <button type="button" className="icon-button" onClick={() => go(addDays(date, 1))} aria-label="Next day">
          ›
        </button>
      </nav>
      {date !== today && (
        <button type="button" className="link-button center" onClick={() => go(today)}>
          Back to today
        </button>
      )}

      <ErrorNote error={day.error} onRetry={day.reload} />
      {notice && (
        <p className="note" role="status">
          {notice}
        </p>
      )}

      {day.data && (
        <>
          <DayTotals totals={day.data.totals} goals={day.data.goals} />
          {day.data.meals.map(({ meal, entries, totals }) => (
            <section key={meal} className="card meal" aria-labelledby={`meal-${meal}`}>
              <header className="card-header">
                <h2 id={`meal-${meal}`}>{meal}</h2>
                <span className="row">
                  <span className="num">{formatNutrient(totals.values.energyKcal, 'energyKcal')} kcal</span>
                  <MealMenu
                    meal={meal}
                    hasEntries={entries.length > 0}
                    onCopyYesterday={() => copyFromYesterday(meal)}
                    onCopyFrom={() => setCopyMeal(meal)}
                    onSave={() => setSaveMeal(meal)}
                  />
                </span>
              </header>
              {entries.length > 0 && (
                <ul className="entry-list">
                  {entries.map((e) => (
                    <li key={e.id}>
                      <button type="button" className="entry-row" onClick={() => setEditing(e)}>
                        <span className="grow">
                          <span className="entry-name">{e.name}</span>
                          <span className="small muted entry-meta">
                            {describeQuantity(e)}
                            {e.brand ? ` · ${e.brand}` : ''}
                          </span>
                        </span>
                        <span className="num entry-kcal">{formatNutrient(e.nutrients.energyKcal, 'energyKcal')}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              <Link className="button button-ghost button-block add-food" to={`/diary/${date}/add/${meal.toLowerCase()}`}>
                + Add food
              </Link>
            </section>
          ))}
        </>
      )}

      {editing && (
        <EntrySheet
          entry={editing}
          onClose={() => setEditing(null)}
          onChanged={() => {
            setEditing(null);
            day.reload();
          }}
        />
      )}
      {saveMeal && (
        <SaveMealSheet
          date={date}
          meal={saveMeal}
          onClose={() => setSaveMeal(null)}
          onSaved={(name) => {
            setSaveMeal(null);
            setNotice(`Saved “${name}”. Find it under Saved meals when adding food.`);
          }}
        />
      )}
      {copyMeal && (
        <CopyMealSheet
          date={date}
          meal={copyMeal}
          onClose={() => setCopyMeal(null)}
          onCopied={(n) => {
            setCopyMeal(null);
            setNotice(`Copied ${n} item${n === 1 ? '' : 's'}.`);
            day.reload();
          }}
        />
      )}
    </div>
  );
}

interface MealMenuProps {
  meal: Meal;
  hasEntries: boolean;
  onCopyYesterday: () => void;
  onCopyFrom: () => void;
  onSave: () => void;
}

function MealMenu({ meal, hasEntries, onCopyYesterday, onCopyFrom, onSave }: MealMenuProps) {
  const [open, setOpen] = useState(false);
  const pick = (fn: () => void) => () => {
    setOpen(false);
    fn();
  };
  return (
    <span className="menu">
      <button
        type="button"
        className="icon-button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={`${meal} options`}
        onClick={() => setOpen((o) => !o)}
      >
        ⋯
      </button>
      {open && (
        <>
          <span className="menu-backdrop" onClick={() => setOpen(false)} />
          <span className="menu-list" role="menu">
            <button type="button" role="menuitem" onClick={pick(onCopyYesterday)}>
              Copy from yesterday
            </button>
            <button type="button" role="menuitem" onClick={pick(onCopyFrom)}>
              Copy from a date…
            </button>
            {hasEntries && (
              <button type="button" role="menuitem" onClick={pick(onSave)}>
                Save as meal…
              </button>
            )}
          </span>
        </>
      )}
    </span>
  );
}

