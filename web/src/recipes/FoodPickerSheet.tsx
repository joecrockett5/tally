import { useState } from 'react';
import type { Food } from '../api/types';
import { FoodSearchResults } from '../components/FoodSearchResults';
import { amountFor, defaultChoice, QuantityPicker, type QuantityChoice } from '../components/QuantityPicker';
import { Sheet } from '../components/Sheet';
import { formatNutrient, nutrientsForAmount } from '../lib/nutrients';
import { useFoodSearch } from '../lib/useFoodSearch';

interface Props {
  title: string;
  onClose: () => void;
  onPick: (food: Food, choice: QuantityChoice) => void;
}

/** Search any food source, then choose a quantity. Used for recipe ingredients. */
export function FoodPickerSheet({ title, onClose, onPick }: Props) {
  const [q, setQ] = useState('');
  const [food, setFood] = useState<Food | null>(null);
  const [choice, setChoice] = useState<QuantityChoice | null>(null);
  const search = useFoodSearch(food ? '' : q);

  const amount = food && choice ? amountFor(food, choice) : 0;
  const valid = !!choice && Number.isFinite(choice.quantity) && choice.quantity > 0 && amount > 0;

  return (
    <Sheet
      title={title}
      onClose={onClose}
      footer={
        food &&
        choice && (
          <>
            <button type="button" className="button" onClick={() => setFood(null)}>
              Back
            </button>
            <button type="button" className="button button-primary" disabled={!valid} onClick={() => onPick(food, choice)}>
              Add · {formatNutrient(nutrientsForAmount(food.nutrients, food.basis, valid ? amount : 0).energyKcal, 'energyKcal')} kcal
            </button>
          </>
        )
      }
    >
      {food && choice ? (
        <div className="stack">
          <h3>{food.name}</h3>
          <QuantityPicker food={food} value={choice} onChange={setChoice} />
        </div>
      ) : (
        <div className="stack">
          <input type="search" className="input" autoFocus placeholder="Search foods" value={q} onChange={(e) => setQ(e.target.value)} aria-label="Search ingredients" />
          {q.trim() && (
            <FoodSearchResults
              search={search}
              query={q}
              onSelect={(f) => {
                setFood(f);
                setChoice(defaultChoice(f));
              }}
            />
          )}
        </div>
      )}
    </Sheet>
  );
}
