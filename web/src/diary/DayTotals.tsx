import type { Goals, NutrientTotals } from '../api/types';
import { Unknown } from '../components/NutritionTable';
import { formatNutrient, goalFor, NUTRIENTS, progressState } from '../lib/nutrients';

interface Props {
  totals: NutrientTotals;
  goals: Goals | null;
}

export function DayTotals({ totals, goals }: Props) {
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
      <ul className="progress-list">
        {NUTRIENTS.map((n) => {
          const value = totals.values[n.key];
          const goal = goalFor(goals, n.key);
          const state = progressState(value, goal, n.isLimit);
          const pct = goal && value !== null ? Math.min(100, (value / goal) * 100) : 0;
          return (
            <li key={n.key} className={n.indent ? 'indent' : undefined}>
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
      {totals.incomplete.length > 0 && (
        <p className="small muted">
          <Unknown /> Some foods don't list every nutrient, so those totals may be higher.
        </p>
      )}
    </section>
  );
}
