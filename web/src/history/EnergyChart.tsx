import { Bar, BarChart, CartesianGrid, ReferenceLine, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import type { HistoryDay } from '../api/types';
import { formatDayLabel } from '../lib/dates';
import { formatNutrient } from '../lib/nutrients';
import { useCssVars } from '../lib/useCssVar';

const VARS = ['--series-1', '--muted', '--border', '--text', '--surface'] as const;

interface Props {
  days: HistoryDay[];
  target: number | null;
}

/** Daily energy as thin bars against the energy target (a single series, so no legend box). */
export function EnergyChart({ days, target }: Props) {
  const c = useCssVars(VARS);
  const data = days.map((d) => ({
    date: d.date,
    label: days.length > 7 ? String(Number(d.date.slice(8))) : formatDayLabel(d.date).slice(0, 3),
    kcal: d.entryCount > 0 ? Math.round(d.totals.energyKcal ?? 0) : null,
    entries: d.entryCount,
    incomplete: d.incomplete.includes('energyKcal'),
  }));

  return (
    <div className="chart" role="img" aria-label="Daily energy chart; the same figures are in the daily table below">
      <ResponsiveContainer width="100%" height={220}>
        <BarChart data={data} margin={{ top: 12, right: 4, bottom: 0, left: -12 }} barCategoryGap={days.length > 7 ? 2 : '25%'}>
          <CartesianGrid vertical={false} stroke={c['--border']} strokeDasharray="2 4" />
          <XAxis dataKey="label" tickLine={false} axisLine={{ stroke: c['--border'] }} tick={{ fill: c['--muted'], fontSize: 11 }} interval={days.length > 7 ? 'preserveStartEnd' : 0} />
          <YAxis tickLine={false} axisLine={false} tick={{ fill: c['--muted'], fontSize: 11 }} width={48} />
          <Tooltip
            cursor={{ fill: c['--border'], opacity: 0.4 }}
            content={({ active, payload }) => {
              const p = active ? payload?.[0]?.payload : null;
              if (!p) return null;
              return (
                <div className="chart-tooltip">
                  <strong>{formatDayLabel(p.date)}</strong>
                  <div>{p.entries ? `${formatNutrient(p.kcal, 'energyKcal')} kcal${p.incomplete ? ' (+?)' : ''}` : 'Nothing logged'}</div>
                  {target !== null && p.entries > 0 && <div className="muted">Target {formatNutrient(target, 'energyKcal')} kcal</div>}
                </div>
              );
            }}
          />
          {target !== null && (
            <ReferenceLine y={target} stroke={c['--text']} strokeDasharray="4 4" strokeWidth={1} label={{ value: 'Target', position: 'insideTopRight', fill: c['--muted'], fontSize: 11 }} />
          )}
          <Bar dataKey="kcal" fill={c['--series-1']} radius={[4, 4, 0, 0]} maxBarSize={28} isAnimationActive={false} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
