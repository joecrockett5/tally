import { useEffect, useState } from 'react';

/** Resolved values of CSS custom properties, refreshed when the colour scheme changes (charts need real colours). */
export function useCssVars<T extends string>(names: readonly T[]): Record<T, string> {
  const read = () =>
    Object.fromEntries(
      names.map((n) => [n, getComputedStyle(document.documentElement).getPropertyValue(n).trim() || '#888']),
    ) as Record<T, string>;
  const [values, setValues] = useState(read);
  useEffect(() => {
    const mq = window.matchMedia?.('(prefers-color-scheme: dark)');
    const update = () => setValues(read());
    mq?.addEventListener?.('change', update);
    return () => mq?.removeEventListener?.('change', update);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);
  return values;
}
