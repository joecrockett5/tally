import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import type { Food } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { NutritionTable } from '../components/NutritionTable';
import { SourceBadge } from '../components/SourceBadge';
import { LogFoodSheet, OffAttribution } from '../diary/LogFoodSheet';
import { formatUkDate, mealForHour } from '../lib/dates';
import { basisLabel } from '../lib/nutrients';
import { useApi } from '../lib/useApi';
import { useToday } from '../profile/ConfigProvider';

export function FoodDetailPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();
  const today = useToday();
  const food = useApi((s) => api.get<Food>(`/api/foods/${id}`, s), [id]);
  const [logging, setLogging] = useState(false);
  const [error, setError] = useState<Error>();
  const [confirmDelete, setConfirmDelete] = useState(false);

  const act = async (fn: () => Promise<void>) => {
    try {
      setError(undefined);
      await fn();
    } catch (err) {
      setError(err as Error);
    }
  };

  if (food.error) return <ErrorNote error={food.error} onRetry={food.reload} />;
  const f = food.data;
  if (!f) return <p className="muted">Loading…</p>;

  const copy = () =>
    act(async () => {
      const created = await api.post<Food>(`/api/foods/${f.id}/copy`);
      navigate(`/foods/${created.id}/edit`);
    });
  const toggleShare = () =>
    act(async () => {
      const updated = await api.put<Food>(`/api/foods/${f.id}`, {
        name: f.name,
        brand: f.brand,
        restaurant: f.restaurant,
        barcode: f.barcode,
        basis: f.basis,
        portionName: f.portionName,
        nutrients: f.nutrients,
        servings: f.servings.map((s) => ({ name: s.name, amount: s.amount })),
        isShared: !f.isShared,
      });
      food.setData(updated);
    });
  const remove = () =>
    act(async () => {
      await api.del(`/api/foods/${f.id}`);
      navigate('/foods');
    });

  return (
    <div className="stack">
      <div className="page-header">
        <button type="button" className="button button-ghost button-small" onClick={() => navigate(-1)}>
          ‹ Back
        </button>
      </div>
      <section className="card stack">
        <div>
          <h1>{f.name}</h1>
          <p className="row small muted wrap">
            <SourceBadge source={f.source} />
            {f.isShared && <span className="badge">Shared with everyone</span>}
            {f.restaurant && f.restaurantId && <Link to={`/restaurants/${f.restaurantId}`}>{f.restaurant}</Link>}
            {f.brand && <span>{f.brand}</span>}
          </p>
          {f.barcode && <p className="small muted">Barcode {f.barcode}</p>}
          {f.createdByName && f.source !== 'mine' && <p className="small muted">Added by {f.createdByName}</p>}
        </div>
        <NutritionTable nutrients={f.nutrients} caption={`Nutrition ${basisLabel(f.basis, f.portionName)}`} />
        {f.servings.length > 0 && (
          <div>
            <h3>Servings</h3>
            <ul className="small">
              {f.servings.map((s) => (
                <li key={s.id}>
                  {s.name} — {s.amount} {f.basis === 'Per100ml' ? 'ml' : f.basis === 'PerPortion' ? 'portion(s)' : 'g'}
                </li>
              ))}
            </ul>
          </div>
        )}
        {f.carbsMayIncludeFibre && (
          <p className="note note-warn small">Carbohydrate may include fibre (US-style labelling); shown as published.</p>
        )}
        {f.source === 'off' && (
          <>
            {f.fetchedAt && <p className="small muted">Fetched from Open Food Facts on {formatUkDate(f.fetchedAt.slice(0, 10))}.</p>}
            <OffAttribution />
          </>
        )}
        <ErrorNote error={error} />
        <div className="button-row">
          <button type="button" className="button button-primary" onClick={() => setLogging(true)}>
            Add to diary
          </button>
          {f.canEdit ? (
            <>
              <Link className="button" to={`/foods/${f.id}/edit`}>
                Edit
              </Link>
              <button type="button" className="button" onClick={toggleShare}>
                {f.isShared ? 'Stop sharing' : 'Share with everyone'}
              </button>
            </>
          ) : (
            <button type="button" className="button" onClick={copy}>
              Copy to my foods
            </button>
          )}
        </div>
        {f.canEdit &&
          (confirmDelete ? (
            <div className="note note-error">
              <span>Delete this food? Past diary entries keep their values.</span>
              <span className="row">
                <button type="button" className="link-button" onClick={() => setConfirmDelete(false)}>
                  Cancel
                </button>
                <button type="button" className="button button-danger button-small" onClick={remove}>
                  Delete
                </button>
              </span>
            </div>
          ) : (
            <button type="button" className="link-button danger-link" onClick={() => setConfirmDelete(true)}>
              Delete food
            </button>
          ))}
      </section>
      {logging && (
        <LogFoodSheet
          food={f}
          date={today}
          meal={mealForHour(new Date().getHours())}
          onClose={() => setLogging(false)}
          onLogged={(e) => navigate(`/diary/${e.date}`)}
        />
      )}
    </div>
  );
}
