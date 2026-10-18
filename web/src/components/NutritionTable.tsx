import type { NutrientKey, Nutrients } from '../api/types';
import { formatNutrient, NUTRIENTS } from '../lib/nutrients';

interface Props {
  nutrients: Nutrients;
  incomplete?: NutrientKey[];
  caption?: string;
}

/** UK back-of-pack style table. Unknown values show "–". */
export function NutritionTable({ nutrients, incomplete = [], caption }: Props) {
  return (
    <table className="nutrition-table">
      {caption && <caption>{caption}</caption>}
      <tbody>
        <tr>
          <th scope="row">Energy</th>
          <td className="num">
            {formatNutrient(nutrients.energyKj, 'energyKj')} kJ / {formatNutrient(nutrients.energyKcal, 'energyKcal')} kcal
            {incomplete.includes('energyKcal') && <Unknown />}
          </td>
        </tr>
        {NUTRIENTS.filter((n) => n.key !== 'energyKcal').map((n) => (
          <tr key={n.key} className={n.indent ? 'indent' : undefined}>
            <th scope="row">{n.label}</th>
            <td className="num">
              {formatNutrient(nutrients[n.key], n.key)}
              {nutrients[n.key] !== null && ' g'}
              {incomplete.includes(n.key) && <Unknown />}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

export function Unknown() {
  return (
    <abbr className="unknown-mark" title="Some items don't list this nutrient, so the real total may be higher">
      +?
    </abbr>
  );
}
