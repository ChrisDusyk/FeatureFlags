import { FeatureFlagsError } from '../errors.js';
import type { ResolvedOptions } from '../options.js';

/**
 * One version of the answer, whole. Replaced rather than mutated, so a refresh landing mid-read
 * cannot show a caller half of one version and half of another.
 */
export interface FlagSnapshot {
  readonly environment: string;
  readonly flags: ReadonlyMap<string, boolean>;
  /** What to send as `If-None-Match` next time, so an unchanged poll costs a 304. */
  readonly etag: string | null;
  readonly fetchedAt: number;
}

/** Relative, so it composes with whatever path the installation is served under. */
const PATH = 'api/evaluation';

interface EvaluationPayload {
  environment?: unknown;
  flags?: unknown;
}

/**
 * Fetches, conditionally. Returns null when the server answers 304 — the caller already holds that
 * answer and should keep it.
 */
export async function fetchEvaluation(
  options: ResolvedOptions,
  current: FlagSnapshot | null,
  signal: AbortSignal,
): Promise<FlagSnapshot | null> {
  const headers: Record<string, string> = {
    accept: 'application/json',
    authorization: `Bearer ${options.sdkKey}`,
  };

  if (current?.etag) {
    // Echoed back exactly as it arrived. Parsing and rebuilding it is a chance to change it, and a
    // changed validator never matches.
    headers['if-none-match'] = current.etag;
  }

  let response: Response;

  try {
    response = await options.fetch(new URL(PATH, options.baseAddress), { headers, signal });
  } catch (cause) {
    if (signal.aborted) {
      throw cause;
    }

    throw new FeatureFlagsError('FeatureFlags: the server could not be reached.', 0, { cause });
  }

  if (response.status === 304 && current) {
    return null;
  }

  if (response.status === 401 || response.status === 403) {
    throw new FeatureFlagsError(await rejectionMessage(response), response.status);
  }

  if (!response.ok) {
    throw new FeatureFlagsError(
      `FeatureFlags: the server answered ${response.status} for /${PATH}.`,
      response.status,
    );
  }

  let payload: EvaluationPayload;

  try {
    payload = (await response.json()) as EvaluationPayload;
  } catch (cause) {
    throw new FeatureFlagsError(
      'FeatureFlags: the response could not be read. This usually means something other than the ' +
        'API answered — a proxy, or a login page.',
      response.status,
      { cause },
    );
  }

  if (typeof payload.environment !== 'string' || !isFlagMap(payload.flags)) {
    throw new FeatureFlagsError(
      'FeatureFlags: the response was missing its flags.',
      response.status,
    );
  }

  return {
    environment: payload.environment,
    // Keys are lowercase slugs on the server; lowercasing here means a caller who writes
    // 'new-Checkout' gets an answer rather than a silent default.
    flags: new Map(Object.entries(payload.flags).map(([key, on]) => [key.toLowerCase(), on])),
    etag: response.headers.get('etag'),
    fetchedAt: Date.now(),
  };
}

/**
 * The server explains a refused credential in ProblemDetails, and the explanation is worth
 * surfacing verbatim — "this is a secret key and the request came from a browser" is a great deal
 * more useful than the status code that carried it.
 */
async function rejectionMessage(response: Response): Promise<string> {
  try {
    const problem: unknown = await response.json();

    if (typeof problem === 'object' && problem !== null) {
      const { detail } = problem as { detail?: unknown };

      if (typeof detail === 'string' && detail.length > 0) {
        return `FeatureFlags: ${detail}`;
      }
    }
  } catch {
    // Not JSON. The status still says something.
  }

  return 'FeatureFlags: the server rejected this SDK key. It may have been revoked, or it may belong to a different installation.';
}

function isFlagMap(value: unknown): value is Record<string, boolean> {
  return (
    typeof value === 'object' &&
    value !== null &&
    !Array.isArray(value) &&
    Object.values(value).every((entry) => typeof entry === 'boolean')
  );
}
