/**
 * The console's half of the SDK keys API.
 *
 * Same shape as the flags API next door, and for the same reasons: everything goes through
 * apiFetch, and a failure carries the server's own words rather than "something went wrong".
 */

import { apiFetch } from '../../auth/token';

export interface SdkKey {
  id: string;
  name: string;
  /** The environment key — 'dev', 'stg', 'prod'. */
  environment: string;
  /**
   * The leading, public part of the token: enough to recognise a key against a configuration file
   * and not enough to authenticate as one. The server never returns more than this after issuing.
   */
  hint: string;
  createdAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
  isActive: boolean;
}

interface ListSdkKeysResponse {
  keys: SdkKey[];
}

/**
 * The response to issuing a key, and the only one that ever carries `token`. It exists in memory
 * for as long as the panel showing it is open — it is not stored, and asking again will not
 * produce it.
 */
export interface IssuedSdkKey {
  id: string;
  name: string;
  environment: string;
  token: string;
  createdAt: string;
}

export class ApiError extends Error {
  code: string | null;
  status: number;

  constructor(message: string, code: string | null, status: number) {
    super(message);
    this.name = 'ApiError';
    this.code = code;
    this.status = status;
  }
}

const FALLBACK_MESSAGE = 'The console could not reach the API.';

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const problem: unknown = await response.json();

    if (typeof problem === 'object' && problem !== null) {
      const { detail, code } = problem as { detail?: unknown; code?: unknown };

      return new ApiError(
        typeof detail === 'string' && detail.length > 0 ? detail : FALLBACK_MESSAGE,
        typeof code === 'string' ? code : null,
        response.status,
      );
    }
  } catch {
    // A response that is not JSON tells us nothing beyond its status, which is still worth having.
  }

  return new ApiError(FALLBACK_MESSAGE, null, response.status);
}

async function send(input: string, init: RequestInit = {}): Promise<Response> {
  let response: Response;

  try {
    response = await apiFetch(input, init);
  } catch (cause) {
    if (cause instanceof DOMException && cause.name === 'AbortError') {
      throw cause;
    }

    throw new ApiError(FALLBACK_MESSAGE, null, 0);
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  return response;
}

export async function listSdkKeys(signal?: AbortSignal): Promise<SdkKey[]> {
  const response = await send('/api/sdk-keys', {
    signal,
    headers: { accept: 'application/json' },
  });

  const body = (await response.json()) as ListSdkKeysResponse;

  return body.keys;
}

export async function issueSdkKey(name: string, environment: string): Promise<IssuedSdkKey> {
  const response = await send('/api/sdk-keys', {
    method: 'POST',
    headers: { 'content-type': 'application/json', accept: 'application/json' },
    body: JSON.stringify({ name, environment }),
  });

  return (await response.json()) as IssuedSdkKey;
}

export async function revokeSdkKey(id: string): Promise<void> {
  await send(`/api/sdk-keys/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    headers: { accept: 'application/json' },
  });
}
