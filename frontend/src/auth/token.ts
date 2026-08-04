/**
 * The console signs in with a cookie but calls the API with a bearer token.
 *
 * The two are not redundant. The cookie is the session and only the auth service reads it; the
 * token is a short-lived, signed statement of who you are that the .NET API can check against
 * the auth service's public keys without a database lookup or a call back. This module trades
 * one for the other and keeps the result in memory — never in storage, where anything running
 * on the page could read it.
 */

const TOKEN_ENDPOINT = '/api/auth/token';

/** Renew this far before expiry so a request in flight never carries a token that dies mid-air. */
const RENEW_MARGIN_MS = 60_000;

interface CachedToken {
  value: string;
  expiresAt: number;
}

let cached: CachedToken | null = null;
let pending: Promise<CachedToken | null> | null = null;

/** Reads `exp` out of a token. A token we cannot read the expiry of is treated as expiring now. */
function expiryOf(token: string): number {
  const payload = token.split('.')[1];

  if (!payload) {
    return 0;
  }

  try {
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    // JWT payloads drop their padding. atob tolerates that for every length a valid
    // payload can have, but restoring it costs nothing and saves the reader the detour.
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const claims: unknown = JSON.parse(atob(padded));

    if (typeof claims === 'object' && claims !== null && 'exp' in claims) {
      const exp = (claims as { exp: unknown }).exp;
      return typeof exp === 'number' ? exp * 1000 : 0;
    }
  } catch {
    // A malformed token is not worth throwing over — it just isn't usable.
  }

  return 0;
}

async function requestToken(): Promise<CachedToken | null> {
  try {
    const response = await fetch(TOKEN_ENDPOINT, {
      credentials: 'include',
      headers: { accept: 'application/json' },
    });

    if (!response.ok) {
      return null;
    }

    const body: unknown = await response.json();
    const value =
      typeof body === 'object' && body !== null && 'token' in body
        ? (body as { token: unknown }).token
        : null;

    if (typeof value !== 'string' || value.length === 0) {
      return null;
    }

    return { value, expiresAt: expiryOf(value) };
  } catch {
    // The network failed, which is not the same as being told no. Report the token as
    // unavailable rather than throwing: the caller's own request then goes out
    // unauthenticated and comes back as a 401 it already knows how to handle, instead
    // of an exception escaping from what looks like an ordinary fetch.
    return null;
  }
}

/**
 * A usable token, or null when there is no session behind it. Concurrent callers share one
 * request rather than each minting a token of their own.
 */
export async function getToken(): Promise<string | null> {
  if (cached && cached.expiresAt - RENEW_MARGIN_MS > Date.now()) {
    return cached.value;
  }

  pending ??= requestToken().finally(() => {
    pending = null;
  });

  cached = await pending;

  return cached?.value ?? null;
}

/** Drops the cached token. Call on sign-out, and whenever the API rejects one. */
export function clearToken(): void {
  cached = null;
}

/**
 * `fetch` for this application's API: attaches the bearer token, and treats a 401 as a stale
 * token worth one retry before believing it. A second 401 is a real answer and is returned.
 */
export async function apiFetch(input: string, init: RequestInit = {}): Promise<Response> {
  const send = async (token: string | null) => {
    const headers = new Headers(init.headers);

    if (token) {
      headers.set('authorization', `Bearer ${token}`);
    }

    return fetch(input, { ...init, headers, credentials: 'include' });
  };

  const response = await send(await getToken());

  if (response.status !== 401) {
    return response;
  }

  clearToken();

  return send(await getToken());
}
