import type { Food } from '../api/types';
import { basisLabel, formatNutrient } from '../lib/nutrients';
import { SourceBadge } from './SourceBadge';

interface Props {
  food: Food;
  onSelect: (food: Food) => void;
  detail?: string;
}

export function FoodRow({ food, onSelect, detail }: Props) {
  const brand = food.restaurant ?? food.brand;
  return (
    <li>
      <button type="button" className="food-row" onClick={() => onSelect(food)}>
        <span className="grow">
          <span className="food-row-name">{food.name}</span>
          <span className="food-row-meta">
            <SourceBadge source={food.source} />
            {food.isShared && food.source !== 'shared' && <span className="badge">Shared</span>}
            {brand && <span className="truncate">{brand}</span>}
          </span>
          {detail && <span className="food-row-detail">{detail}</span>}
        </span>
        <span className="food-row-energy num">
          <strong>{formatNutrient(food.nutrients.energyKcal, 'energyKcal')}</strong>
          <span className="small muted">kcal {basisLabel(food.basis).replace('per portion', '/portion').replace('per ', '/')}</span>
        </span>
      </button>
    </li>
  );
}
