import { vi } from 'vitest';

type Handler = (init: RequestInit & { url: string }) => unknown;

export interface MockRoute {
  method?: string;
  /** Exact path, or a regex tested against path + query. */
  path: string | RegExp;
  status?: number;
  body?: unknown | Handler;
}

/** Replaces global fetch with a small router. Returns the spy for call assertions. */
export function mockFetch(routes: MockRoute[]) {
  const spy = vi.fn(async (input: RequestInfo | URL, init: RequestInit = {}) => {
    const url = typeof input === 'string' ? input : input.toString();
    const method = (init.method ?? 'GET').toUpperCase();
    const route = routes.find(
      (r) =>
        (r.method ?? 'GET').toUpperCase() === method &&
        (typeof r.path === 'string' ? url.split('?')[0] === r.path : r.path.test(url)),
    );
    if (!route) return new Response(JSON.stringify({ title: 'not_mocked', detail: `${method} ${url}` }), { status: 404 });
    const body = typeof route.body === 'function' ? (route.body as Handler)({ ...init, url }) : route.body;
    const status = route.status ?? (body === undefined ? 204 : 200);
    return new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'Content-Type': 'application/json' },
    });
  });
  vi.stubGlobal('fetch', spy);
  return spy;
}
