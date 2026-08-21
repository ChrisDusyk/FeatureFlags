import { FeatureFlagsError } from '../errors.js';
import type { ResolvedOptions } from '../options.js';
import { authorizedHeaders, readJson, send, throwForStatus } from './http.js';
import type { Ruleset } from './evaluate.js';
import type { Deadline } from './runtime.js';

/**
 * One version of the ruleset, whole. Replaced rather than mutated, so a refresh landing mid-read
 * cannot show a caller half of one version and half of another.
 */
export interface RulesetSnapshot {
  readonly ruleset: Ruleset;
  /** What to send as `If-None-Match` next time, so an unchanged poll costs a 304. */
  readonly etag: string | null;
  readonly fetchedAt: number;
}

/** Relative, so it composes with whatever path the installation is served under. */
const PATH = 'api/evaluation/ruleset';

/**
 * Fetches the ruleset, conditionally. Returns null when the server answers 304 — the caller
 * already holds that answer and should keep it.
 *
 * Only a secret key may ask: this ships every segment definition an environment uses, which is not
 * something a publishable key can be handed. The server refuses one with a 403 whose body says so.
 */
export async function fetchRuleset(
  options: ResolvedOptions,
  current: RulesetSnapshot | null,
  attempt: Deadline,
): Promise<RulesetSnapshot | null> {
  const response = await send(
    options,
    PATH,
    { headers: authorizedHeaders(options, current?.etag ?? null) },
    attempt,
  );

  if (response.status === 304 && current) {
    return null;
  }

  await throwForStatus(response, PATH);

  const payload = await readJson(response);

  if (!isRuleset(payload)) {
    throw new FeatureFlagsError('FeatureFlags: the response was missing its flags.', response.status);
  }

  return {
    ruleset: payload,
    etag: response.headers.get('etag'),
    fetchedAt: Date.now(),
  };
}

/**
 * Checked structurally rather than trusted. This is a network boundary, and a payload that is
 * almost right — a proxy's error page, an older server — should read as a failure rather than as a
 * ruleset in which every flag happens to be off.
 */
export function isRuleset(value: unknown): value is Ruleset {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<Ruleset>;

  return (
    typeof candidate.environment === 'string' &&
    Array.isArray(candidate.flags) &&
    Array.isArray(candidate.segments) &&
    candidate.flags.every(
      (flag) =>
        typeof flag?.key === 'string' &&
        typeof flag.isEnabled === 'boolean' &&
        Array.isArray(flag.targetedSegments),
    ) &&
    candidate.segments.every(
      (segment) =>
        typeof segment?.key === 'string' &&
        Array.isArray(segment.included) &&
        Array.isArray(segment.excluded) &&
        Array.isArray(segment.conditions),
    )
  );
}
