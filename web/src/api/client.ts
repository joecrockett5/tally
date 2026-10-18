/** Minimal typed fetch wrapper. Sends the active profile as X-Profile-Id. */

let activeProfileId: string | null = null;

export function setActiveProfileId(id: string | null) {
  activeProfileId = id;
}

export const PROFILE_MISSING_EVENT = 'tally:profile-missing';

export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
  ) {
    super(message);
  }
}

async function request<T>(method: string, path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (activeProfileId) headers['X-Profile-Id'] = activeProfileId;
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  let response: Response;
  try {
    response = await fetch(path, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    });
  } catch (err) {
    if ((err as Error).name === 'AbortError') throw err;
    throw new ApiError(0, 'network', 'Could not reach the Tally server.');
  }

  if (!response.ok) {
    let code = 'error';
    let message = `Request failed (${response.status})`;
    try {
      const problem = await response.json();
      code = problem.title ?? code;
      message = problem.detail ?? problem.title ?? message;
    } catch {
      /* not JSON */
    }
    if (code === 'profile_not_found') window.dispatchEvent(new Event(PROFILE_MISSING_EVENT));
    throw new ApiError(response.status, code, message);
  }
  if (response.status === 204) return undefined as T;
  const type = response.headers.get('Content-Type') ?? '';
  return (type.includes('json') ? await response.json() : await response.text()) as T;
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal) => request<T>('GET', path, undefined, signal),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body ?? {}),
  put: <T>(path: string, body: unknown) => request<T>('PUT', path, body),
  del: <T = void>(path: string) => request<T>('DELETE', path),
};
