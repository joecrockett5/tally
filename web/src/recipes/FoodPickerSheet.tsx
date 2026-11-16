import { useState } from 'react';
import { api } from '../api/client';
import type { BarcodeLookup, Food, QuickPick } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { FoodRow } from '../components/FoodRow';
import { FoodSearchResults } from '../components/FoodSearchResults';
import { amountFor, defaultChoice, QuantityPicker, type QuantityChoice } from '../components/QuantityPicker';
import { Sheet } from '../components/Sheet';
import { Scanner, type ScanResult } from '../diary/Scanner';
import { describeQuantity, formatNutrient, nutrientsForAmount } from '../lib/nutrients';
import { useApi } from '../lib/useApi';
import { useFoodSearch } from '../lib/useFoodSearch';

interface Props {
  title: string;
  onClose: () => void;
  onPick: (food: Food, choice: QuantityChoice) => void;
}

type QuickTab = 'recent' | 'frequent';

const TABS: [QuickTab, string][] = [
  ['recent', 'Recent'],
  ['frequent', 'Frequent'],
];

/**
 * Find a food by search, barcode scan or from recently logged foods, then choose
 * a quantity. Used for recipe ingredients.
 */
export function FoodPickerSheet({ title, onClose, onPick }: Props) {
  const [q, setQ] = useState('');
  const [tab, setTab] = useState<QuickTab>('recent');
  const [food, setFood] = useState<Food | null>(null);
  const [choice, setChoice] = useState<QuantityChoice | null>(null);
  const [scanning, setScanning] = useState(false);
  const [scanOutcome, setScanOutcome] = useState<BarcodeLookup | null>(null);
  const [error, setError] = useState<Error>();
  const search = useFoodSearch(food ? '' : q);
  // Tagged with its tab so a stale list is never shown under the other tab.
  const quickData = useApi(async (s) => ({ tab, items: await api.get<QuickPick[]>(`/api/foods/${tab}`, s) }), [tab]);
  const quick = quickData.data?.tab === tab ? quickData.data.items : undefined;

  const amount = food && choice ? amountFor(food, choice) : 0;
  const valid = !!choice && Number.isFinite(choice.quantity) && choice.quantity > 0 && amount > 0;

  const select = (f: Food, initial?: QuantityChoice) => {
    setFood(f);
    setChoice(initial ?? defaultChoice(f));
  };

  const onScan = async ({ code, format }: ScanResult) => {
    setScanning(false);
    setError(undefined);
    setScanOutcome(null);
    try {
      const result = await api.get<BarcodeLookup>(`/api/foods/barcode/${encodeURIComponent(code)}${format ? `?format=${format}` : ''}`);
      if (result.status === 'found' && result.food) select(result.food);
      else setScanOutcome(result);
    } catch (err) {
      setError(err as Error);
    }
  };

  // The scanner is a sheet of its own, so it takes this one's place while open.
  if (scanning) return <Scanner onDetected={onScan} onClose={() => setScanning(false)} />;

  return (
    <Sheet
      title={title}
      onClose={onClose}
      tall
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
          <div className="picker-search">
            <input type="search" className="input grow" placeholder="Search foods" value={q} onChange={(e) => setQ(e.target.value)} aria-label="Search ingredients" />
            <button type="button" className="button button-primary" onClick={() => setScanning(true)} aria-label="Scan barcode">
              ▥ Scan
            </button>
          </div>
          <ErrorNote error={error} />
          {scanOutcome && (
            <p role="status">
              <strong>
                {scanOutcome.status === 'not_found' ? `Barcode ${scanOutcome.barcode} wasn't found.` : `Couldn't look up ${scanOutcome.barcode}.`}
              </strong>{' '}
              {scanOutcome.message ?? ''} Try searching by name, or save this recipe and add the product under Foods.
            </p>
          )}
          {q.trim() ? (
            <FoodSearchResults search={search} query={q} onSelect={(f) => select(f)} />
          ) : (
            <>
              <div className="chips" role="tablist">
                {TABS.map(([t, label]) => (
                  <button key={t} type="button" role="tab" className="chip" aria-selected={tab === t} aria-pressed={tab === t} onClick={() => setTab(t)}>
                    {label}
                  </button>
                ))}
              </div>
              <ErrorNote error={quickData.error} onRetry={quickData.reload} />
              {quick?.length === 0 && <p className="empty">Nothing logged yet. Search for a food or scan a barcode.</p>}
              <ul className="food-list">
                {quick?.map((p) => (
                  <FoodRow
                    key={p.food.id}
                    food={p.food}
                    detail={`Last: ${describeQuantity({
                      unit: p.unit,
                      quantity: p.quantity,
                      servingName: p.food.servings.find((s) => s.id === p.servingId)?.name,
                      portionName: p.food.portionName,
                    })}${tab === 'frequent' ? ` · logged ${p.timesLogged}×` : ''}`}
                    onSelect={() => select(p.food, { unit: p.unit, quantity: p.quantity, servingId: p.servingId })}
                  />
                ))}
              </ul>
            </>
          )}
        </div>
      )}
    </Sheet>
  );
}
