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
export function unref(timer: ReturnType<typeof setInterval>): void {
  (timer as unknown as { unref?: () => void }).unref?.();
}
