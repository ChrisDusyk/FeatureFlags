const relative = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/**
 * How long ago a flag's state moved, in the coarsest unit that still says something. "3 days ago"
 * is what a person wants from a list; the exact instant belongs in a tooltip, which is where the
 * caller puts it.
 */
export function changedAgo(timestamp: string): string {
  const then = Date.parse(timestamp);

  if (Number.isNaN(then)) {
    return 'at an unknown time';
  }

  const elapsed = Date.now() - then;

  // Clocks disagree. A state stamped a few seconds into the future is skew, not the future.
  if (elapsed < MINUTE) {
    return 'just now';
  }

  if (elapsed < HOUR) {
    return relative.format(-Math.floor(elapsed / MINUTE), 'minute');
  }

  if (elapsed < DAY) {
    return relative.format(-Math.floor(elapsed / HOUR), 'hour');
  }

  return relative.format(-Math.floor(elapsed / DAY), 'day');
}

/** The full instant, for the title attribute behind the relative one. */
export function changedExactly(timestamp: string): string {
  const then = new Date(timestamp);

  return Number.isNaN(then.getTime()) ? timestamp : then.toLocaleString();
}
