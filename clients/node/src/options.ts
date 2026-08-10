/**
 * How to reach a FeatureFlags installation.
 *
 * There is deliberately no environment here. An SDK key is issued for one environment and carries
 * it, so the server decides which flags this client sees — one thing to configure, and no way for
 * it to drift from what the console shows.
 */
export interface FeatureFlagsOptions {
  /**
   * The origin the console is on — `https://flags.example.com`. The same value the installation
   * was deployed with as `FEATUREFLAGS_ORIGIN`.
   */
  baseAddress: string;

  /**
   * A key issued under Organization → Environments. It is shown once, when it is issued.
   *
   * In a browser this has to be a **publishable** key (`ffp_`). A secret key (`ffs_`) is refused
   * by the server when the request comes from a browser, and this client refuses to start with one
   * so the mistake surfaces where it was made rather than as a failed fetch later.
   */
  sdkKey: string;

  /**
   * How stale an answer may get before it is refetched. Defaults to 30 seconds.
   *
   * This is the upper bound on how long a toggle takes to reach this process, and the lower bound
   * on how often it asks — a poll that finds nothing changed costs a 304 with no body, so it can
   * be shorter than it looks.
   */
  pollingInterval?: number;

  /** How long a single refresh may take before it is abandoned, in milliseconds. Defaults to 10 seconds. */
  timeout?: number;

  /**
   * The `fetch` to use. Defaults to the global one, which both Node 20+ and browsers have. Present
   * for tests and for anyone who has to route through a proxy agent.
   */
  fetch?: typeof globalThis.fetch;
}

export interface ResolvedOptions extends Required<Omit<FeatureFlagsOptions, 'fetch'>> {
  fetch: typeof globalThis.fetch;
}

export const SECRET_KEY_PREFIX = 'ffs_';
export const PUBLISHABLE_KEY_PREFIX = 'ffp_';

const DEFAULTS = {
  pollingInterval: 30_000,
  timeout: 10_000,
} as const;

/**
 * Fills in the defaults and rejects what could only fail later.
 *
 * Everything checked here is something that would otherwise surface as a 401 or a failed fetch
 * somewhere far from the line that caused it — which for a flag client means "the flags were
 * always off" rather than an error anyone notices.
 */
export function resolveOptions(options: FeatureFlagsOptions): ResolvedOptions {
  if (!options || typeof options !== 'object') {
    throw new TypeError('createFeatureFlagsClient needs an options object.');
  }

  const baseAddress = requireOrigin(options.baseAddress);
  const sdkKey = requireSdkKey(options.sdkKey);

  const pollingInterval = options.pollingInterval ?? DEFAULTS.pollingInterval;
  const timeout = options.timeout ?? DEFAULTS.timeout;

  if (!Number.isFinite(pollingInterval) || pollingInterval <= 0) {
    throw new TypeError('FeatureFlags: pollingInterval must be a positive number of milliseconds.');
  }

  if (!Number.isFinite(timeout) || timeout <= 0) {
    throw new TypeError('FeatureFlags: timeout must be a positive number of milliseconds.');
  }

  const resolvedFetch = options.fetch ?? globalThis.fetch;

  if (typeof resolvedFetch !== 'function') {
    throw new TypeError(
      'FeatureFlags: no fetch is available. Node 20 or later has one built in; otherwise pass one as options.fetch.',
    );
  }

  return {
    baseAddress,
    sdkKey,
    pollingInterval,
    timeout,
    // Bound, because an unbound global fetch throws "Illegal invocation" in a browser.
    fetch: resolvedFetch.bind(globalThis),
  };
}

function requireOrigin(value: string): string {
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new TypeError(
      'FeatureFlags: baseAddress is required. It is the origin the console is on, for example https://flags.example.com.',
    );
  }

  let url: URL;

  try {
    url = new URL(value);
  } catch {
    throw new TypeError(
      `FeatureFlags: baseAddress must be an absolute URL including the scheme — got ${JSON.stringify(value)}.`,
    );
  }

  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
    throw new TypeError(
      `FeatureFlags: baseAddress must be http or https — got ${JSON.stringify(url.protocol)}.`,
    );
  }

  // A credential in the URL is not one this client can use — the SDK key is the credential, and it
  // travels in a header. What it would do instead is ride along in anything that logs the address.
  if (url.username.length > 0 || url.password.length > 0) {
    throw new TypeError(
      'FeatureFlags: baseAddress must not carry a username or password. The SDK key is the credential.',
    );
  }

  // A path is kept, because an installation may be served under one. A query or a fragment is not:
  // relative resolution drops both, so keeping them would mean an address that reads one way and
  // requests another. Refused for the same reason the server refuses them in FEATUREFLAGS_ORIGIN.
  if (url.search.length > 0 || url.hash.length > 0) {
    throw new TypeError(
      'FeatureFlags: baseAddress must be an address, with no query string or fragment.',
    );
  }

  // A trailing slash, so URL composition keeps any path the installation is served under instead
  // of dropping its last segment.
  const address = `${url.origin}${url.pathname}`;

  return address.endsWith('/') ? address : `${address}/`;
}

function requireSdkKey(value: string): string {
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new TypeError(
      'FeatureFlags: sdkKey is required. Issue one in the console under Organization → Environments.',
    );
  }

  const key = value.trim();

  // Matched loosely on purpose: this catches a value that is obviously not a key — an unexpanded
  // environment variable, a JWT pasted by mistake. Whether the key is *valid* is the server's to
  // say, and only it can.
  if (!key.startsWith(SECRET_KEY_PREFIX) && !key.startsWith(PUBLISHABLE_KEY_PREFIX)) {
    throw new TypeError(
      `FeatureFlags: sdkKey does not look like one — it should begin with ${SECRET_KEY_PREFIX} or ${PUBLISHABLE_KEY_PREFIX}.`,
    );
  }

  return key;
}
