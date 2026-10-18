import type { FoodSourceLabel } from '../api/types';

export const SOURCE_LABELS: Record<FoodSourceLabel, string> = {
  mine: 'Mine',
  shared: 'Shared',
  restaurant: 'Restaurant',
  cofid: 'Generic (CoFID)',
  off: 'Open Food Facts',
  recipe: 'Recipe',
};

export function SourceBadge({ source }: { source: FoodSourceLabel }) {
  return <span className={`badge badge-${source}`}>{SOURCE_LABELS[source]}</span>;
}
