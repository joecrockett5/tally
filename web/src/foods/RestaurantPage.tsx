import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import type { Food, Restaurant } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { FoodRow } from '../components/FoodRow';
import { useApi } from '../lib/useApi';

export function RestaurantPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const data = useApi((s) => api.get<{ restaurant: Restaurant; foods: Food[] }>(`/api/restaurants/${id}/foods`, s), [id]);

  return (
    <div className="stack">
      <div className="page-header">
        <h1>{data.data?.restaurant.name ?? 'Restaurant'}</h1>
        {data.data && (
          <Link className="button button-small" to={`/foods/new?restaurant=${encodeURIComponent(data.data.restaurant.name)}`}>
            + Add item
          </Link>
        )}
      </div>
      <ErrorNote error={data.error} onRetry={data.reload} />
      <section className="card">
        {data.data?.foods.length === 0 && <p className="empty">No items you can see yet.</p>}
        <ul className="food-list">
          {data.data?.foods.map((f) => (
            <FoodRow key={f.id} food={f} onSelect={() => navigate(`/foods/${f.id}`)} />
          ))}
        </ul>
      </section>
    </div>
  );
}
