import type { MealSlot } from '../api/types';

interface Props {
  meals: MealSlot[] | undefined;
  value: string | undefined;
  onChange: (mealId: string) => void;
}

/** Picks one of a day's meals. Disabled until the layout has loaded. */
export function MealSelect({ meals, value, onChange }: Props) {
  return (
    <label className="field">
      <span>Meal</span>
      <select value={value ?? ''} disabled={!meals} onChange={(e) => onChange(e.target.value)}>
        {meals?.map((m) => (
          <option key={m.id} value={m.id}>
            {m.name}
          </option>
        ))}
      </select>
    </label>
  );
}
