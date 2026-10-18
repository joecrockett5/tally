import type { Food } from '../api/types';
import { OffAttribution } from '../diary/LogFoodSheet';
import type { useFoodSearch } from '../lib/useFoodSearch';
import { ErrorNote } from './ErrorNote';
import { FoodRow } from './FoodRow';

interface Props {
  search: ReturnType<typeof useFoodSearch>;
  query: string;
  onSelect: (food: Food) => void;
  empty?: React.ReactNode;
}

export function FoodSearchResults({ search, query, onSelect, empty }: Props) {
  return (
    <>
      <ErrorNote error={search.error} onRetry={search.retry} />
      {search.results.length > 0 && (
        <ul className="food-list">
          {search.results.map((f) => (
            <FoodRow key={f.id} food={f} onSelect={onSelect} />
          ))}
        </ul>
      )}
      {search.settled && search.results.length === 0 && (
        <div className="empty stack">
          <p>No foods match “{query.trim()}”.</p>
          {empty}
        </div>
      )}
      <p className="small muted online-status" aria-live="polite">
        {search.status}
      </p>
      {search.showsOff && <OffAttribution />}
    </>
  );
}
