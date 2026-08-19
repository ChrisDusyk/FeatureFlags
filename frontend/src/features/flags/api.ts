/**
 * The console's half of the flags API.
 *
 * Everything here goes through apiFetch, which attaches the bearer token and retries once past a
 * stale one. A failed call is turned into an ApiError carrying the server's own words: the API
 * answers in ProblemDetails, and its `detail` says something truer than "something went wrong"
 * ever could — "A flag with the key 'new-checkout' already exists", for one.
 */

import { apiFetch } from '../../auth/token';

export interface Flag {
  id: string;
  key: string;
  name: string;
  description: string;
  /** In the environment the list was asked for, not everywhere. */
  isEnabled: boolean;
  /** When this environment last changed — not when the flag was last edited. */
  updatedAt: string;
}

interface ListFlagsResponse {
  environment: string;
  flags: Flag[];
}

export interface FlagState {
  environment: string;
  isEnabled: boolean;
  updatedAt: string;
}

/** A flag's full details, across every environment at once — unlike Flag, which is scoped to one. */
export interface FlagDetail {
  id: string;
  key: string;
  name: string;
  description: string;
  createdAt: string;
  updatedAt: string;
  states: FlagState[];
}

export interface UpdateFlagInput {
  name: string;
  description: string;
}

export type FlagHistoryEventType = 'FlagCreated' | 'FlagDetailsChanged' | 'FlagStateChanged';

/**
 * One entry in a flag's activity log. `eventType` says which of the other fields are set:
 * name/description for FlagCreated and FlagDetailsChanged, environment/isEnabled for
 * FlagStateChanged.
 */
export interface FlagHistoryEntry {
  eventType: FlagHistoryEventType;
  occurredAt: string;
  /** Who caused this, or null when the actor is unknown — history backfilled before attribution
   * existed carries no real actor. */
  causedByName: string | null;
  name: string | null;
  description: string | null;
  environment: string | null;
  isEnabled: boolean | null;
}

interface GetFlagHistoryResponse {
  entries: FlagHistoryEntry[];
}

export interface CreateFlagInput {
  key: string;
  name: string;
  description: string;
  /** Environment keys the flag starts on in. It is created everywhere regardless. */
  enabledIn: string[];
}

/**
 * A failure the server explained. `code` is the domain error code, e.g. "Flag.DuplicateKey".
 *
 * Fields are declared and assigned rather than taken as constructor parameter properties, which
 * `erasableSyntaxOnly` rules out — nothing here may depend on TypeScript emitting code.
 */
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

/**
 * A network failure is not the same as being told no, but to a caller both mean "no answer".
 * Both arrive as an ApiError so the screen has one thing to render.
 */
async function send(input: string, init: RequestInit = {}): Promise<Response> {
  let response: Response;

  try {
    response = await apiFetch(input, init);
  } catch (cause) {
    // An aborted request is the caller's own doing — let it through untouched so a screen
    // unmounting mid-flight does not paint an error on its way out.
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

export async function listFlags(environmentKey: string, signal?: AbortSignal): Promise<Flag[]> {
  const response = await send(`/api/flags?environment=${encodeURIComponent(environmentKey)}`, {
    signal,
    headers: { accept: 'application/json' },
  });

  const body = (await response.json()) as ListFlagsResponse;

  return body.flags;
}

export async function createFlag(input: CreateFlagInput): Promise<void> {
  await send('/api/flags', {
    method: 'POST',
    headers: { 'content-type': 'application/json', accept: 'application/json' },
    body: JSON.stringify(input),
  });
}

export interface FlagStateResult {
  key: string;
  environment: string;
  isEnabled: boolean;
  updatedAt: string;
}

/**
 * Sets a flag's state in one environment. Sets, not flips — sending this twice lands in the same
 * place, so a retry after a dropped response cannot turn a flag back off.
 *
 * Returns what the server settled on, so the row can show the real timestamp rather than the one
 * the browser guessed while the request was in flight.
 */
export async function setFlagState(
  key: string,
  environmentKey: string,
  isEnabled: boolean,
): Promise<FlagStateResult> {
  const response = await send(`/api/flags/${encodeURIComponent(key)}/state`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json', accept: 'application/json' },
    body: JSON.stringify({ environment: environmentKey, isEnabled }),
  });

  return (await response.json()) as FlagStateResult;
}

export async function getFlag(key: string, signal?: AbortSignal): Promise<FlagDetail> {
  const response = await send(`/api/flags/${encodeURIComponent(key)}`, {
    signal,
    headers: { accept: 'application/json' },
  });

  return (await response.json()) as FlagDetail;
}

/** Updates a flag's name and description. There is no way to send a key here — it cannot change. */
export async function updateFlag(key: string, input: UpdateFlagInput): Promise<FlagDetail> {
  const response = await send(`/api/flags/${encodeURIComponent(key)}`, {
    method: 'PUT',
    headers: { 'content-type': 'application/json', accept: 'application/json' },
    body: JSON.stringify(input),
  });

  return (await response.json()) as FlagDetail;
}

export async function getFlagHistory(key: string, signal?: AbortSignal): Promise<FlagHistoryEntry[]> {
  const response = await send(`/api/flags/${encodeURIComponent(key)}/history`, {
    signal,
    headers: { accept: 'application/json' },
  });

  const body = (await response.json()) as GetFlagHistoryResponse;

  return body.entries;
}
