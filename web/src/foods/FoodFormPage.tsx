import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router';
import { api } from '../api/client';
import type { Basis, Food, FoodInput, NutrientKey, Restaurant } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { NUTRIENTS } from '../lib/nutrients';
import { useDebounced } from '../lib/useDebounced';

type NutrientText = Record<NutrientKey, string>;

const emptyNutrients: NutrientText = {
  energyKcal: '',
  energyKj: '',
  fat: '',
  saturates: '',
  carbohydrate: '',
  sugars: '',
  fibre: '',
  protein: '',
  salt: '',
};

interface ServingText {
  name: string;
  amount: string;
}

/** Blank → null (unknown), otherwise a number. Accepts a comma decimal separator too. */
export function parseOptionalNumber(text: string): number | null | 'invalid' {
  const t = text.trim().replace(',', '.');
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) && n >= 0 ? n : 'invalid';
}

export function FoodFormPage() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const editing = !!id;

  const [name, setName] = useState(params.get('name') ?? '');
  const [brand, setBrand] = useState('');
  const [restaurant, setRestaurant] = useState(params.get('restaurant') ?? '');
  const [barcode, setBarcode] = useState(params.get('barcode') ?? '');
  const [basis, setBasis] = useState<Basis>(params.get('restaurant') ? 'PerPortion' : 'Per100g');
  const [portionName, setPortionName] = useState('');
  const [nutrients, setNutrients] = useState<NutrientText>(emptyNutrients);
  const [servings, setServings] = useState<ServingText[]>([]);
  const [isShared, setIsShared] = useState(false);
  const [sodium, setSodium] = useState('');
  const [error, setError] = useState<Error | string>();
  const [busy, setBusy] = useState(false);
  const [loaded, setLoaded] = useState(!editing);

  useEffect(() => {
    if (!id) return;
    api
      .get<Food>(`/api/foods/${id}`)
      .then((f) => {
        setName(f.name);
        setBrand(f.brand ?? '');
        setRestaurant(f.restaurant ?? '');
        setBarcode(f.barcode ?? '');
        setBasis(f.basis);
        setPortionName(f.portionName ?? '');
        setNutrients(
          Object.fromEntries(Object.entries(f.nutrients).map(([k, v]) => [k, v === null ? '' : String(Math.round(v * 1000) / 1000)])) as NutrientText,
        );
        setServings(f.servings.map((s) => ({ name: s.name, amount: String(s.amount) })));
        setIsShared(f.isShared);
        setLoaded(true);
      })
      .catch(setError);
  }, [id]);

  const restaurantQ = useDebounced(restaurant.trim(), 250);
  const [suggestions, setSuggestions] = useState<Restaurant[]>([]);
  useEffect(() => {
    if (restaurantQ.length < 1) return setSuggestions([]);
    const c = new AbortController();
    api
      .get<Restaurant[]>(`/api/restaurants?q=${encodeURIComponent(restaurantQ)}`, c.signal)
      .then(setSuggestions)
      .catch(() => undefined);
    return () => c.abort();
  }, [restaurantQ]);

  const unit = basis === 'Per100ml' ? 'ml' : basis === 'PerPortion' ? 'portions' : 'g';
  const basisText = basis === 'Per100g' ? 'per 100 g' : basis === 'Per100ml' ? 'per 100 ml' : 'per portion';

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const values: Partial<Record<NutrientKey, number | null>> = {};
    for (const key of Object.keys(emptyNutrients) as NutrientKey[]) {
      const v = parseOptionalNumber(nutrients[key]);
      if (v === 'invalid') return setError(`Check the value for ${key === 'energyKj' ? 'energy (kJ)' : NUTRIENTS.find((n) => n.key === key)?.label ?? key}.`);
      values[key] = v;
    }
    const sodiumValue = parseOptionalNumber(sodium);
    if (sodiumValue === 'invalid') return setError('Check the sodium value.');
    if (values.salt === null && sodiumValue !== null) values.salt = sodiumValue * 2.5;
    if (values.energyKcal === null && values.energyKj === null) return setError('Enter energy in kcal or kJ.');

    const parsedServings = servings
      .filter((s) => s.name.trim() || s.amount.trim())
      .map((s) => ({ name: s.name.trim(), amount: Number(s.amount.replace(',', '.')) }));
    if (parsedServings.some((s) => !s.name || !(s.amount > 0))) return setError('Each serving needs a name and a size above zero.');

    const input: FoodInput = {
      name,
      brand: brand || null,
      restaurant: restaurant || null,
      barcode: barcode || null,
      basis,
      portionName: basis === 'PerPortion' ? portionName || null : null,
      nutrients: values as FoodInput['nutrients'],
      servings: parsedServings,
      isShared,
    };
    setBusy(true);
    try {
      const saved = editing ? await api.put<Food>(`/api/foods/${id}`, input) : await api.post<Food>('/api/foods', input);
      navigate(`/foods/${saved.id}`, { replace: true });
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  if (!loaded) return error ? <ErrorNote error={error} /> : <p className="muted">Loading…</p>;

  return (
    <form className="stack" onSubmit={submit}>
      <div className="page-header">
        <h1>{editing ? 'Edit food' : 'New food'}</h1>
        <button type="button" className="button button-ghost button-small" onClick={() => navigate(-1)}>
          Cancel
        </button>
      </div>

      <section className="card stack">
        <label className="field">
          <span>Name</span>
          <input value={name} onChange={(e) => setName(e.target.value)} required maxLength={300} />
        </label>
        <label className="field">
          <span>Restaurant (optional)</span>
          <input value={restaurant} onChange={(e) => setRestaurant(e.target.value)} list="restaurant-options" maxLength={100} placeholder="e.g. Nando's" />
          <datalist id="restaurant-options">
            {suggestions.map((r) => (
              <option key={r.id} value={r.name} />
            ))}
          </datalist>
        </label>
        <div className="field-row">
          <label className="field">
            <span>Brand (optional)</span>
            <input value={brand} onChange={(e) => setBrand(e.target.value)} maxLength={200} />
          </label>
          <label className="field">
            <span>Barcode (optional)</span>
            <input value={barcode} onChange={(e) => setBarcode(e.target.value)} inputMode="numeric" />
          </label>
        </div>
      </section>

      <section className="card stack">
        <fieldset className="field">
          <legend>Nutrition is given…</legend>
          <div className="chips wrap">
            {(
              [
                ['Per100g', 'per 100 g'],
                ['Per100ml', 'per 100 ml'],
                ['PerPortion', 'per portion only'],
              ] as const
            ).map(([b, label]) => (
              <button key={b} type="button" className="chip" aria-pressed={basis === b} onClick={() => setBasis(b)}>
                {label}
              </button>
            ))}
          </div>
          {basis === 'PerPortion' && <p className="hint">Use this when you only know the values for a whole item, e.g. a restaurant dish.</p>}
        </fieldset>
        {basis === 'PerPortion' && (
          <label className="field">
            <span>What is one portion?</span>
            <input value={portionName} onChange={(e) => setPortionName(e.target.value)} placeholder="e.g. 1 burger" maxLength={100} />
          </label>
        )}
        <p className="hint">Leave a value blank if it isn't known — it will show as “–”, not zero.</p>
        <div className="nutrient-grid">
          <NutrientInput label={`Energy (kcal) ${basisText}`} value={nutrients.energyKcal} onChange={(v) => setNutrients({ ...nutrients, energyKcal: v })} />
          <NutrientInput label="Energy (kJ)" value={nutrients.energyKj} onChange={(v) => setNutrients({ ...nutrients, energyKj: v })} />
          {NUTRIENTS.filter((n) => n.key !== 'energyKcal').map((n) => (
            <NutrientInput key={n.key} label={`${n.label} (g)`} value={nutrients[n.key]} onChange={(v) => setNutrients({ ...nutrients, [n.key]: v })} />
          ))}
          {nutrients.salt.trim() === '' && <NutrientInput label="Sodium (g) — converted to salt" value={sodium} onChange={setSodium} />}
        </div>
      </section>

      <section className="card stack">
        <div className="row-between">
          <h2>Servings</h2>
          <button type="button" className="button button-small" onClick={() => setServings([...servings, { name: '', amount: '' }])}>
            + Add serving
          </button>
        </div>
        {servings.length === 0 && <p className="hint">Optional, e.g. “1 slice” = 36 g.</p>}
        {servings.map((s, i) => (
          <div key={i} className="serving-row">
            <label className="field grow">
              <span>Serving name</span>
              <input value={s.name} onChange={(e) => setServings(servings.map((x, j) => (j === i ? { ...x, name: e.target.value } : x)))} placeholder="1 slice" />
            </label>
            <label className="field serving-amount">
              <span>Size ({unit})</span>
              <input value={s.amount} inputMode="decimal" onChange={(e) => setServings(servings.map((x, j) => (j === i ? { ...x, amount: e.target.value } : x)))} />
            </label>
            <button type="button" className="icon-button" aria-label={`Remove serving ${i + 1}`} onClick={() => setServings(servings.filter((_, j) => j !== i))}>
              ✕
            </button>
          </div>
        ))}
      </section>

      <section className="card">
        <label className="checkbox">
          <input type="checkbox" checked={isShared} onChange={(e) => setIsShared(e.target.checked)} />
          <span>
            <strong>Share with everyone</strong>
            <span className="hint" style={{ display: 'block' }}>
              Everyone on this Tally can see and log it; only you can edit it.
            </span>
          </span>
        </label>
      </section>

      <ErrorNote error={error} />
      <button className="button button-primary" disabled={busy || !name.trim()}>
        {editing ? 'Save changes' : 'Create food'}
      </button>
    </form>
  );
}

function NutrientInput({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  return (
    <label className="field">
      <span>{label}</span>
      <input inputMode="decimal" value={value} onChange={(e) => onChange(e.target.value)} placeholder="–" />
    </label>
  );
}
