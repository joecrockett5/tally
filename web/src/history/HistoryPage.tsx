import { useState } from 'react';
import { Link } from 'react-router';
import { api, download } from '../api/client';
import type { History } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { addDays, endOfMonth, formatDayLabel, formatMonth, formatUkDate, startOfMonth, startOfWeek } from '../lib/dates';
import { formatNutrient, goalFor, NUTRIENTS } from '../lib/nutrients';
import { useApi } from '../lib/useApi';
import { useToday } from '../profile/ConfigProvider';
import { useActiveProfile } from '../profile/ProfileProvider';
import { EnergyChart } from './EnergyChart';
import { MacroSplit } from './MacroSplit';

type Period = 'week' | 'month';

export function periodRange(period: Period, anchor: string): { from: string; to: string } {
  if (period === 'week') {
    const from = startOfWeek(anchor);
    return { from, to: addDays(from, 6) };
  }
  return { from: startOfMonth(anchor), to: endOfMonth(anchor) };
}

export function HistoryPage() {
  const profile = useActiveProfile();
  const today = useToday();
  const [period, setPeriod] = useState<Period>('week');
  const [anchor, setAnchor] = useState(today);
  const { from, to } = periodRange(period, anchor);
  const history = useApi((s) => api.get<History>(`/api/history?from=${from}&to=${to}`, s), [from, to, profile.id]);

  const shift = (direction: -1 | 1) =>
    setAnchor(period === 'week' ? addDays(from, 7 * direction) : direction < 0 ? addDays(from, -1) : addDays(to, 1));
  const title = period === 'week' ? `Week of ${formatUkDate(from)}` : formatMonth(from);
  const h = history.data?.from === from ? history.data : undefined;

  return (
    <div className="stack">
      <div className="page-header">
        <h1>History</h1>
      </div>
      <div className="chips" role="tablist">
        {(['week', 'month'] as const).map((p) => (
          <button key={p} type="button" role="tab" className="chip" aria-selected={period === p} aria-pressed={period === p} onClick={() => setPeriod(p)}>
            {p === 'week' ? 'Week' : 'Month'}
          </button>
        ))}
      </div>
      <nav className="date-nav" aria-label="Choose period">
        <button type="button" className="icon-button" onClick={() => shift(-1)} aria-label={`Previous ${period}`}>
          ‹
        </button>
        <span className="date-nav-label">
          <span className="date-nav-main">{title}</span>
          <span className="small muted">
            {formatUkDate(from)} – {formatUkDate(to)}
          </span>
        </span>
        <button type="button" className="icon-button" onClick={() => shift(1)} aria-label={`Next ${period}`} disabled={to >= today}>
          ›
        </button>
      </nav>

      <ErrorNote error={history.error} onRetry={history.reload} />
      {!h && !history.error && <p className="muted">Loading…</p>}
      {h && h.loggedDays === 0 && (
        <section className="card empty">
          Nothing logged in this {period}. <Link to={`/diary/${today}`}>Go to today's diary</Link>.
        </section>
      )}
      {h && h.loggedDays > 0 && (
        <>
          <section className="stat-row" aria-label="Summary">
            <div className="card stat">
              <span className="stat-value num">{formatNutrient(h.averages.energyKcal, 'energyKcal')}</span>
              <span className="stat-label">avg kcal/day</span>
            </div>
            <div className="card stat">
              <span className="stat-value num">{h.loggedDays}</span>
              <span className="stat-label">days logged</span>
            </div>
            {h.daysOnTarget !== null && (
              <div className="card stat">
                <span className="stat-value num">
                  {h.daysOnTarget}/{h.loggedDays}
                </span>
                <span className="stat-label">days on target</span>
              </div>
            )}
          </section>

          <section className="card stack">
            <h2>Daily energy</h2>
            <EnergyChart days={h.days} target={h.goals.energyKcal} />
          </section>

          <section className="card stack">
            <h2>Average per day</h2>
            <p className="hint">Only days with at least one entry count towards averages ({h.loggedDays} of {h.days.length} days).</p>
            <table className="nutrition-table">
              <thead>
                <tr>
                  <th />
                  <th className="num">Average</th>
                  <th className="num">Goal</th>
                </tr>
              </thead>
              <tbody>
                {NUTRIENTS.map((n) => {
                  const goal = goalFor(h.goals, n.key);
                  return (
                    <tr key={n.key} className={n.indent ? 'indent' : undefined}>
                      <th scope="row">{n.label}</th>
                      <td className="num">
                        {formatNutrient(h.averages[n.key], n.key)}
                        {h.averages[n.key] !== null && ` ${n.unit}`}
                      </td>
                      <td className="num muted">{goal === null ? '–' : `${n.isLimit ? '≤ ' : ''}${formatNutrient(goal, n.key)} ${n.unit}`}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </section>

          {h.macros && (
            <section className="card stack">
              <h2>Where your energy came from</h2>
              <MacroSplit macros={h.macros} />
            </section>
          )}

          <section className="card stack">
            <h2>Day by day</h2>
            <p className="hint">On target = energy within 10% of your goal without going over the sugars, saturates or salt limits.</p>
            <table className="nutrition-table">
              <thead>
                <tr>
                  <th>Day</th>
                  <th className="num">kcal</th>
                  <th className="num">On target</th>
                </tr>
              </thead>
              <tbody>
                {h.days.map((d) => (
                  <tr key={d.date}>
                    <th scope="row">
                      <Link to={`/diary/${d.date}`}>{formatDayLabel(d.date)}</Link>
                    </th>
                    <td className="num">{d.entryCount ? formatNutrient(d.totals.energyKcal, 'energyKcal') : <span className="muted">not logged</span>}</td>
                    <td className="num">{d.onTarget === null ? '' : d.onTarget ? '✓ Yes' : 'No'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        </>
      )}

      <ExportCard defaultFrom={from} defaultTo={to} />
    </div>
  );
}

function ExportCard({ defaultFrom, defaultTo }: { defaultFrom: string; defaultTo: string }) {
  const [from, setFrom] = useState<string | null>(null);
  const [to, setTo] = useState<string | null>(null);
  const [error, setError] = useState<Error>();
  const [busy, setBusy] = useState(false);
  const f = from ?? defaultFrom;
  const t = to ?? defaultTo;

  const run = async () => {
    setBusy(true);
    setError(undefined);
    try {
      await download(`/api/export/diary.csv?from=${f}&to=${t}`, `tally-diary-${f}-to-${t}.csv`);
    } catch (err) {
      setError(err as Error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="card stack">
      <h2>Export diary</h2>
      <p className="hint">A CSV with one row per entry: date, meal, food, source, quantity and every nutrient. Up to a year at a time.</p>
      <div className="field-row">
        <label className="field">
          <span>From</span>
          <input type="date" value={f} max={t} onChange={(e) => setFrom(e.target.value)} />
        </label>
        <label className="field">
          <span>To</span>
          <input type="date" value={t} min={f} onChange={(e) => setTo(e.target.value)} />
        </label>
      </div>
      <ErrorNote error={error} />
      <button type="button" className="button" disabled={busy || !f || !t || f > t} onClick={run}>
        Download CSV
      </button>
    </section>
  );
}
