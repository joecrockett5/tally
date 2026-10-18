import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { api } from '../api/client';
import type { Food, Restaurant } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { FoodRow } from '../components/FoodRow';
import { useApi } from '../lib/useApi';
import { useActiveProfile } from '../profile/ProfileProvider';

type Tab = 'mine' | 'shared' | 'restaurants';

export function FoodsPage() {
  const profile = useActiveProfile();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const tab = (params.get('tab') as Tab) ?? 'mine';
  const [filter, setFilter] = useState('');

  const foods = useApi(
    (s) => (tab === 'restaurants' ? Promise.resolve([] as Food[]) : api.get<Food[]>(`/api/foods?filter=${tab}`, s)),
    [tab, profile.id],
  );
  const restaurants = useApi(
    (s) => (tab === 'restaurants' ? api.get<Restaurant[]>('/api/restaurants', s) : Promise.resolve([] as Restaurant[])),
    [tab, profile.id],
  );

  const term = filter.trim().toLowerCase();
  const shownFoods = (foods.data ?? []).filter(
    (f) => !term || `${f.name} ${f.brand ?? ''} ${f.restaurant ?? ''}`.toLowerCase().includes(term),
  );
  const shownRestaurants = (restaurants.data ?? []).filter((r) => !term || r.name.toLowerCase().includes(term));

  return (
    <div className="stack">
      <div className="page-header">
        <h1>Foods</h1>
        <span className="row">
          <Link className="button button-small" to="/foods/import">
            Import
          </Link>
          <Link className="button button-primary button-small" to="/foods/new">
            + New food
          </Link>
        </span>
      </div>

      <div className="chips" role="tablist" aria-label="Food lists">
        {(
          [
            ['mine', 'My foods'],
            ['shared', 'Shared'],
            ['restaurants', 'Restaurants'],
          ] as const
        ).map(([key, label]) => (
          <button
            key={key}
            type="button"
            role="tab"
            className="chip"
            aria-selected={tab === key}
            aria-pressed={tab === key}
            onClick={() => setParams({ tab: key }, { replace: true })}
          >
            {label}
          </button>
        ))}
      </div>

      <input
        type="search"
        className="input"
        placeholder={tab === 'restaurants' ? 'Filter restaurants' : 'Filter foods'}
        value={filter}
        onChange={(e) => setFilter(e.target.value)}
        aria-label="Filter"
      />

      <section className="card">
        <ErrorNote error={foods.error ?? restaurants.error} onRetry={tab === 'restaurants' ? restaurants.reload : foods.reload} />
        {tab === 'restaurants' ? (
          shownRestaurants.length ? (
            <ul className="food-list">
              {shownRestaurants.map((r) => (
                <li key={r.id}>
                  <Link className="food-row" to={`/restaurants/${r.id}`}>
                    <span className="grow food-row-name">{r.name}</span>
                    <span className="muted small">
                      {r.itemCount} item{r.itemCount === 1 ? '' : 's'}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            !restaurants.loading && (
              <p className="empty">
                No restaurant items yet. Add one with <Link to="/foods/new">New food</Link>, or import a menu.
              </p>
            )
          )
        ) : shownFoods.length ? (
          <ul className="food-list">
            {shownFoods.map((f) => (
              <FoodRow key={f.id} food={f} onSelect={() => navigate(`/foods/${f.id}`)} detail={tab === 'shared' && f.createdByName ? `Shared by ${f.createdByName}` : undefined} />
            ))}
          </ul>
        ) : (
          !foods.loading && (
            <p className="empty">
              {tab === 'mine'
                ? "You haven't added any foods yet. Create one when a search or barcode comes up empty."
                : 'Nobody has shared any foods yet.'}
            </p>
          )
        )}
      </section>
    </div>
  );
}
