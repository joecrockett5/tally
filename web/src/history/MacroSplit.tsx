import type { History } from '../api/types';

const PARTS = [
  { key: 'protein', label: 'Protein', className: 'macro-protein' },
  { key: 'carbohydrate', label: 'Carbohydrate', className: 'macro-carbs' },
  { key: 'fat', label: 'Fat', className: 'macro-fat' },
] as const;

/** 100% stacked bar of energy from protein, carbohydrate and fat, direct-labelled, with a table equivalent. */
export function MacroSplit({ macros }: { macros: NonNullable<History['macros']> }) {
  const pct = { protein: macros.proteinPct, carbohydrate: macros.carbohydratePct, fat: macros.fatPct };
  const kcal = { protein: macros.proteinKcal, carbohydrate: macros.carbohydrateKcal, fat: macros.fatKcal };
  return (
    <div className="stack">
      <div className="macro-bar" aria-hidden="true">
        {PARTS.map((p) => (
          <span key={p.key} className={p.className} style={{ flexGrow: pct[p.key] }} title={`${p.label} ${Math.round(pct[p.key])}%`} />
        ))}
      </div>
      <table className="nutrition-table macro-table">
        <caption className="visually-hidden">Energy by macronutrient, average per logged day</caption>
        <tbody>
          {PARTS.map((p) => (
            <tr key={p.key}>
              <th scope="row">
                <span className={`macro-swatch ${p.className}`} aria-hidden="true" /> {p.label}
              </th>
              <td className="num">{Math.round(pct[p.key])}%</td>
              <td className="num muted">{Math.round(kcal[p.key])} kcal/day</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="hint">Energy from each macronutrient using UK label factors (protein and carbohydrate 4 kcal/g, fat 9 kcal/g).</p>
    </div>
  );
}
