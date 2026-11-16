import { useState } from 'react';
import type { Goals, NutrientTotals } from '../api/types';
import { Unknown } from '../components/NutritionTable';
import { formatNutrient, goalFor, NUTRIENTS, progressState } from '../lib/nutrients';

interface Props {
  totals: NutrientTotals;
  goals: Goals | null;
}

/** The rows shown before the list is expanded: the macros, under the calorie number. */
const SUMMARY_KEYS = ['protein', 'carbohydrate', 'fat'] as const;
const SUMMARY = SUMMARY_KEYS.map((key) => NUTRIENTS.find((n) => n.key === key)!);
const EXPANDED_KEY = 'tally.dayTotalsExpanded';

function readExpanded(): boolean {
  try {
    return localStorage.getItem(EXPANDED_KEY) === '1';
  } catch {
    return false;
  }
}

export function DayTotals({ totals, goals }: Props) {
  const [expanded, setExpanded] = useState(readExpanded);
  const toggle = () => {
    const next = !expanded;
    setExpanded(next);
    try {
      localStorage.setItem(EXPANDED_KEY, next ? '1' : '0');
    } catch {
      // Private browsing or blocked storage: the choice just won't be remembered.
    }
  };
  const rows = expanded ? NUTRIENTS : SUMMARY;
  const shownKeys: string[] = expanded ? [...NUTRIENTS.map((n) => n.key), 'energyKj'] : [...SUMMARY_KEYS, 'energyKcal', 'energyKj'];
  const showIncompleteNote = totals.incomplete.some((k) => shownKeys.includes(k));
  const kcal = totals.values.energyKcal;
  const kcalGoal = goals?.energyKcal ?? null;
  const remaining = kcalGoal !== null && kcal !== null ? kcalGoal - kcal : null;

  return (
    <section className="card day-totals" aria-label="Day totals">
      <div className="energy-summary">
        <div>
          <span className="energy-big num">{formatNutrient(kcal, 'energyKcal')}</span>
          <span className="muted"> kcal</span>
          {totals.incomplete.includes('energyKcal') && <Unknown />}
          <div className="small muted num">{formatNutrient(totals.values.energyKj, 'energyKj')} kJ</div>
        </div>
        {remaining !== null && (
          <div className="energy-remaining">
            <span className={`num ${remaining < 0 ? 'text-over' : ''}`}>{formatNutrient(Math.abs(remaining), 'energyKcal')}</span>
            <span className="small muted">{remaining < 0 ? 'kcal over' : 'kcal left'}</span>
          </div>
        )}
      </div>
      <ul className="progress-list" id="day-totals-list">
        {rows.map((n) => {
          const value = totals.values[n.key];
          const goal = goalFor(goals, n.key);
          const state = progressState(value, goal, n.isLimit);
          const pct = goal && value !== null ? Math.min(100, (value / goal) * 100) : 0;
          return (
            <li key={n.key} className={expanded && n.indent ? 'indent' : undefined}>
              <div className="row-between small">
                <span>
                  {n.label}
                  {n.isLimit && goal !== null && <span className="muted"> (limit)</span>}
                </span>
                <span className="num">
                  {formatNutrient(value, n.key)}
                  {totals.incomplete.includes(n.key) && <Unknown />}
                  {goal !== null && <span className="muted"> / {formatNutrient(goal, n.key)}</span>}
                  {(value !== null || goal !== null) && ` ${n.unit}`}
                </span>
              </div>
              {goal !== null && (
                <div
                  className={`progress progress-${state}`}
                  role="progressbar"
                  aria-label={`${n.label} progress`}
                  aria-valuemin={0}
                  aria-valuemax={goal}
                  aria-valuenow={value ?? 0}
                >
                  <span style={{ width: `${pct}%` }} />
                </div>
              )}
            </li>
          );
        })}
      </ul>
      {showIncompleteNote && (
        <p className="small muted">
          <Unknown /> Some foods don't list every nutrient, so those totals may be higher.
        </p>
      )}
      <button type="button" className="totals-toggle" aria-expanded={expanded} aria-controls="day-totals-list" onClick={toggle}>
        {expanded ? 'Show less' : 'Show all nutrients'}
      </button>
    </section>
  );
}
