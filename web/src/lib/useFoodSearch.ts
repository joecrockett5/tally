import { api } from '../api/client';
import type { Food, OnlineSearch } from '../api/types';
import { useConfig } from '../profile/ConfigProvider';
import { useApi } from './useApi';
import { useDebounced } from './useDebounced';

/** Merges online results into local ones, dropping products already listed (by barcode or id). */
export function mergeResults(local: Food[], online: Food[]): Food[] {
  const ids = new Set(local.map((f) => f.id));
  const barcodes = new Set(local.map((f) => f.barcode).filter(Boolean));
  return [...local, ...online.filter((f) => !ids.has(f.id) && !(f.barcode && barcodes.has(f.barcode)))];
}

/**
 * Local results arrive quickly (200 ms debounce); Open Food Facts results follow
 * (600 ms debounce, 3+ characters) and are merged in, de-duplicated by barcode.
 */
export function useFoodSearch(query: string) {
  const { onlineEnabled } = useConfig();
  const q = query.trim();
  const localQ = useDebounced(q, 200);
  const onlineQ = useDebounced(q, 600);

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

  const results = mergeResults(local.data ?? [], online.data?.results ?? []);
  const onlinePending = q.length >= 3 && onlineEnabled && (onlineQ !== q || online.loading);
  const status = !q
    ? ''
    : !onlineEnabled
      ? 'Online search is turned off on this server.'
      : q.length < 3
        ? 'Type at least 3 letters to include Open Food Facts.'
        : onlinePending
          ? 'Searching Open Food Facts…'
          : online.error || online.data?.available === false
            ? `Online results unavailable${online.data?.message ? `: ${online.data.message}` : ''}. Showing local foods only.`
            : (online.data?.message ?? '');

  return {
    results,
    status,
    error: local.error,
    retry: local.reload,
    settled: !local.loading && !onlinePending && localQ === q,
    showsOff: results.some((f) => f.source === 'off'),
  };
}
