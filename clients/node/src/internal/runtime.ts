/**
 * Whether this code is running in a browser.
 *
 * A heuristic, and it only has to be one. The authority on this question is the server, which
 * refuses a secret key on any request carrying an `Origin` header — a browser sets that and script
 * cannot forge it. What this function is for is turning that refusal into an error at the line
 * that configured the client, in development, instead of a failed fetch in production.
 *
 * Server-side rendering is why both globals are checked and why the answer is computed rather than
 * captured: the same module runs on a server and then in a browser, and it has to answer
 * differently in each.
 */
export function isBrowser(): boolean {
  return typeof globalThis.window !== 'undefined' && typeof globalThis.document !== 'undefined';
}

/**
 * Stops a timer from holding a Node process open.
 *
 * A polling interval is not a reason for a CLI to refuse to exit. `unref` is Node's; browsers have
 * no equivalent and need none, so its absence is the ordinary case rather than a problem.
 */
export function unref(timer: ReturnType<typeof setTimeout>): void {
  (timer as unknown as { unref?: () => void }).unref?.();
}

/**
 * How long one request gets, and a way to tell why it was abandoned.
 */
export interface Deadline {
  readonly signal: AbortSignal;

  /**
   * Whether the time ran out, as against the client having been closed. Both abort the same fetch
   * the same way, and only one of them is a failure worth reporting.
   */
  readonly expired: boolean;

  /** Clears the timer and unsubscribes, however the request ended. */
  settle(): void;
}

/**
 * Gives a request a deadline, and abandons it if the client is closed first.
 *
 * `AbortSignal.timeout` and `AbortSignal.any` express this in two lines, and both are newer than
 * the floor this package claims — `AbortSignal.any` did not reach Safari or Firefox until 2024,
 * years after `fetch` did. Calling either would turn "any browser with `fetch`" into a `TypeError`
 * at the first refresh, so this is built from `AbortController` and `setTimeout`, which are as old
 * as `fetch` is. `signal.reason` is avoided for the same reason: which of the two aborts happened
 * is tracked here instead.
 */
export function deadline(lifetime: AbortSignal, ms: number): Deadline {
  const controller = new AbortController();
  let expired = false;

  const abandon = (): void => controller.abort();

  if (lifetime.aborted) {
    abandon();
  } else {
    lifetime.addEventListener('abort', abandon);
  }

  const timer = setTimeout(() => {
    expired = true;
    controller.abort();
  }, ms);

  // The in-flight fetch already holds the process open for as long as this timer matters, so there
  // is nothing for it to keep alive that was not alive anyway.
  unref(timer);

  return {
    signal: controller.signal,
    get expired(): boolean {
      return expired;
    },
    settle(): void {
      clearTimeout(timer);
      lifetime.removeEventListener('abort', abandon);
    },
  };
}
