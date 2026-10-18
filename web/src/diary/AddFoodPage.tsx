import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { api } from '../api/client';
import { MEALS, type BarcodeLookup, type Food, type Meal, type OnlineSearch, type QuickPick } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { FoodRow } from '../components/FoodRow';
import type { QuantityChoice } from '../components/QuantityPicker';
import { describeQuantity } from '../lib/nutrients';
import { useApi } from '../lib/useApi';
import { useDebounced } from '../lib/useDebounced';
import { useConfig } from '../profile/ConfigProvider';
import { LogFoodSheet, OffAttribution } from './LogFoodSheet';
import { Scanner, type ScanResult } from './Scanner';

type QuickTab = 'recent' | 'frequent';

/** Merges online results into local ones, dropping products already listed (by barcode or id). */
export function mergeResults(local: Food[], online: Food[]): Food[] {
  const ids = new Set(local.map((f) => f.id));
  const barcodes = new Set(local.map((f) => f.barcode).filter(Boolean));
  return [...local, ...online.filter((f) => !ids.has(f.id) && !(f.barcode && barcodes.has(f.barcode)))];
}

export function AddFoodPage() {
  const { date = '', meal: mealParam } = useParams();
  const meal = (MEALS.find((m) => m.toLowerCase() === mealParam?.toLowerCase()) ?? 'Snacks') as Meal;
  const navigate = useNavigate();
  const { onlineEnabled } = useConfig();

  const [q, setQ] = useState('');
  const [tab, setTab] = useState<QuickTab>('recent');
  const [selected, setSelected] = useState<{ food: Food; initial?: QuantityChoice } | null>(null);
  const [scanning, setScanning] = useState(false);
  const [scanOutcome, setScanOutcome] = useState<BarcodeLookup | null>(null);
  const [scanError, setScanError] = useState<Error>();

  const localQ = useDebounced(q.trim(), 200);
  const onlineQ = useDebounced(q.trim(), 600);

  const local = useApi(
    (s) => (localQ ? api.get<Food[]>(`/api/foods/search?q=${encodeURIComponent(localQ)}`, s) : Promise.resolve([])),
    [localQ],
  );
  const online = useApi(
    (s) =>
      onlineEnabled && onlineQ.length >= 3
        ? api.get<OnlineSearch>(`/api/foods/search/online?q=${encodeURIComponent(onlineQ)}`, s)
        : Promise.resolve<OnlineSearch>({ available: true, message: null, results: [] }),
    [onlineQ, onlineEnabled],
  );
  const quick = useApi((s) => api.get<QuickPick[]>(`/api/foods/${tab}`, s), [tab]);

  const results = mergeResults(local.data ?? [], online.data?.results ?? []);
  const searching = q.trim().length > 0;
  const onlinePending = searching && q.trim().length >= 3 && (onlineQ !== q.trim() || online.loading);
  const showsOff = results.some((f) => f.source === 'off');

  const onScan = async ({ code, format }: ScanResult) => {
    setScanning(false);
    setScanError(undefined);
    setScanOutcome(null);
    try {
      const result = await api.get<BarcodeLookup>(
        `/api/foods/barcode/${encodeURIComponent(code)}${format ? `?format=${format}` : ''}`,
      );
      if (result.status === 'found' && result.food) setSelected({ food: result.food });
      else setScanOutcome(result);
    } catch (err) {
      setScanError(err as Error);
    }
  };

  return (
    <div className="stack">
      <div className="page-header">
        <h1>Add to {meal}</h1>
        <Link className="button button-ghost button-small" to={`/diary/${date}`}>
          Done
        </Link>
      </div>

      <div className="search-bar">
        <input
          type="search"
          className="input grow"
          placeholder="Search foods, brands, restaurants…"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          aria-label="Search foods"
          autoFocus
        />
        <button type="button" className="button button-primary" onClick={() => setScanning(true)} aria-label="Scan barcode">
          ▥ Scan
        </button>
      </div>

      <ErrorNote error={scanError} />
      {scanOutcome && (
        <div className="card stack" role="status">
          {scanOutcome.status === 'not_found' ? (
            <>
              <p>
                <strong>Barcode {scanOutcome.barcode} wasn't found.</strong> {scanOutcome.message}
              </p>
              <Link className="button button-primary" to={`/foods/new?barcode=${scanOutcome.barcode}`}>
                Add this product
              </Link>
            </>
          ) : (
            <>
              <p>
                <strong>Couldn't look up {scanOutcome.barcode}.</strong> {scanOutcome.message ?? 'Online results are unavailable.'}
              </p>
              <Link className="button" to={`/foods/new?barcode=${scanOutcome.barcode}`}>
                Add it manually
              </Link>
            </>
          )}
        </div>
      )}

      {!searching ? (
        <section className="card">
          <div className="chips" role="tablist">
            {(['recent', 'frequent'] as const).map((t) => (
              <button key={t} type="button" role="tab" className="chip" aria-selected={tab === t} aria-pressed={tab === t} onClick={() => setTab(t)}>
                {t === 'recent' ? 'Recent' : 'Frequent'}
              </button>
            ))}
          </div>
          <ErrorNote error={quick.error} onRetry={quick.reload} />
          {quick.data?.length === 0 && (
            <p className="empty">Nothing yet. Search for a food or scan a barcode to get started.</p>
          )}
          <ul className="food-list">
            {quick.data?.map((p) => (
              <FoodRow
                key={p.food.id}
                food={p.food}
                detail={`Last: ${describeQuantity({
                  unit: p.unit,
                  quantity: p.quantity,
                  servingName: p.food.servings.find((s) => s.id === p.servingId)?.name,
                  portionName: p.food.portionName,
                })}${tab === 'frequent' ? ` · logged ${p.timesLogged}×` : ''}`}
                onSelect={() => setSelected({ food: p.food, initial: { unit: p.unit, quantity: p.quantity, servingId: p.servingId } })}
              />
            ))}
          </ul>
        </section>
      ) : (
        <section className="card">
          <ErrorNote error={local.error} onRetry={local.reload} />
          {results.length > 0 && (
            <ul className="food-list">
              {results.map((f) => (
                <FoodRow key={f.id} food={f} onSelect={(food) => setSelected({ food })} />
              ))}
            </ul>
          )}
          {!local.loading && !onlinePending && results.length === 0 && (
            <div className="empty stack">
              <p>No foods match “{q.trim()}”.</p>
              <Link className="button" to={`/foods/new?name=${encodeURIComponent(q.trim())}`}>
                Create a food
              </Link>
            </div>
          )}
          <p className="small muted online-status" aria-live="polite">
            {!onlineEnabled
              ? 'Online search is turned off on this server.'
              : q.trim().length < 3
                ? 'Type at least 3 letters to include Open Food Facts.'
                : onlinePending
                  ? 'Searching Open Food Facts…'
                  : online.error || online.data?.available === false
                    ? `Online results unavailable${online.data?.message ? `: ${online.data.message}` : ''}. Showing local foods only.`
                    : online.data?.message ?? ''}
          </p>
          {showsOff && <OffAttribution />}
        </section>
      )}

      {scanning && <Scanner onDetected={onScan} onClose={() => setScanning(false)} />}
      {selected && (
        <LogFoodSheet
          food={selected.food}
          initial={selected.initial}
          date={date}
          meal={meal}
          onClose={() => setSelected(null)}
          onLogged={() => navigate(`/diary/${date}`)}
        />
      )}
    </div>
  );
}
