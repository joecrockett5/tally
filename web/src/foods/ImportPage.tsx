import { useState, type ChangeEvent } from 'react';
import { Link } from 'react-router';
import { api } from '../api/client';
import type { ImportPreview, ImportResult } from '../api/types';
import { ErrorNote } from '../components/ErrorNote';
import { formatNutrient } from '../lib/nutrients';

type Format = 'csv' | 'json';

export function detectFormat(fileName: string, content: string): Format {
  if (/\.json$/i.test(fileName)) return 'json';
  if (/\.csv$/i.test(fileName)) return 'csv';
  return /^\s*[[{]/.test(content) ? 'json' : 'csv';
}

export function ImportPage() {
  const [content, setContent] = useState('');
  const [format, setFormat] = useState<Format>('csv');
  const [shared, setShared] = useState(false);
  const [skipDuplicates, setSkipDuplicates] = useState(true);
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [result, setResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState<Error>();
  const [busy, setBusy] = useState(false);

  const onFile = async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const text = await file.text();
    setContent(text);
    setFormat(detectFormat(file.name, text));
    setPreview(null);
    setResult(null);
  };

  const body = () => ({ format, content, shared, skipDuplicates });

  const run = async <T,>(path: string, done: (r: T) => void) => {
    setBusy(true);
    setError(undefined);
    try {
      done(await api.post<T>(path, body()));
    } catch (err) {
      setError(err as Error);
    } finally {
      setBusy(false);
    }
  };

  const importable = preview ? preview.valid + (skipDuplicates ? 0 : preview.duplicates) : 0;

  return (
    <div className="stack">
      <div className="page-header">
        <h1>Import foods</h1>
        <Link className="button button-ghost button-small" to="/foods">
          Done
        </Link>
      </div>

      <section className="card stack">
        <p className="small">
          Import custom foods or a restaurant menu from a CSV or JSON file. You'll see a preview with any problems before anything is saved.
          The format is described in <code>docs/IMPORT.md</code>; columns: <code>name, restaurant, brand, barcode, basis, portion_name, energy_kcal, energy_kj, fat, saturates, carbohydrate, sugars, fibre, protein, salt, sodium, servings</code>.
        </p>
        <label className="field">
          <span>File</span>
          <input type="file" accept=".csv,.json,text/csv,application/json" onChange={onFile} />
        </label>
        <label className="field">
          <span>…or paste the contents</span>
          <textarea
            value={content}
            onChange={(e) => {
              setContent(e.target.value);
              setFormat(detectFormat('', e.target.value));
              setPreview(null);
            }}
            placeholder={'name,restaurant,basis,portion_name,energy_kcal\nSausage Roll,Greggs,per_portion,1 roll,327'}
          />
        </label>
        <div className="field-row">
          <label className="field">
            <span>Format</span>
            <select value={format} onChange={(e) => setFormat(e.target.value as Format)}>
              <option value="csv">CSV</option>
              <option value="json">JSON</option>
            </select>
          </label>
        </div>
        <label className="checkbox">
          <input type="checkbox" checked={shared} onChange={(e) => setShared(e.target.checked)} />
          <span>Share imported foods with everyone</span>
        </label>
        <label className="checkbox">
          <input type="checkbox" checked={skipDuplicates} onChange={(e) => setSkipDuplicates(e.target.checked)} />
          <span>Skip duplicates</span>
        </label>
        <button type="button" className="button" disabled={!content.trim() || busy} onClick={() => run<ImportPreview>('/api/import/preview', (p) => { setPreview(p); setResult(null); })}>
          Preview
        </button>
      </section>

      <ErrorNote error={error} />

      {result && (
        <p className="note" role="status">
          Imported {result.created} food{result.created === 1 ? '' : 's'}.
          {result.skippedDuplicates > 0 && ` Skipped ${result.skippedDuplicates} duplicate${result.skippedDuplicates === 1 ? '' : 's'}.`}
          {result.skippedInvalid > 0 && ` Skipped ${result.skippedInvalid} row${result.skippedInvalid === 1 ? '' : 's'} with errors.`}
        </p>
      )}

      {preview && !result && (
        <section className="card stack" aria-label="Import preview">
          <p>
            <strong>{preview.valid}</strong> ready · <strong>{preview.duplicates}</strong> duplicate{preview.duplicates === 1 ? '' : 's'} ·{' '}
            <strong>{preview.invalid}</strong> with errors
          </p>
          <div className="table-scroll">
            <table className="preview-table">
              <thead>
                <tr>
                  <th>Row</th>
                  <th>Food</th>
                  <th className="num">kcal</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {preview.rows.map((r) => (
                  <tr key={r.row} className={r.errors.length ? 'row-error' : r.duplicate ? 'row-warn' : undefined}>
                    <td className="num">{r.row}</td>
                    <td>
                      {r.food?.name || <em>(no name)</em>}
                      {r.food?.restaurant && <span className="muted small"> · {r.food.restaurant}</span>}
                    </td>
                    <td className="num">{formatNutrient(r.food?.nutrients?.energyKcal, 'energyKcal')}</td>
                    <td className="small">{r.errors.length ? r.errors.join(' ') : r.duplicate ?? 'OK'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <button type="button" className="button button-primary" disabled={busy || importable === 0} onClick={() => run<ImportResult>('/api/import/commit', setResult)}>
            Import {importable} food{importable === 1 ? '' : 's'}
            {shared ? ' (shared)' : ' (private)'}
          </button>
        </section>
      )}
    </div>
  );
}
