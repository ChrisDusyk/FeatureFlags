import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    include: ['test/**/*.test.ts'],
    // Fake timers are used to age the snapshot past its polling interval without waiting, which is
    // the only way to test staleness without making the suite take as long as the interval.
    restoreMocks: true,
  },
});
