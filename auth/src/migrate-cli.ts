/**
 * Applies the auth schema's migrations and exits — `pnpm migrate`.
 *
 * Startup can do this itself (see `applyMigrations` in config.ts), which is what the AppHost
 * and the compose bundle rely on. This is the same work as a step that can be ordered: the
 * Helm chart runs it as a pre-upgrade job, before the server's own migration, because that
 * one puts a trigger on `auth."user"` and needs the table to exist first.
 *
 * The exit code is the point. A migration that failed has to stop whatever comes next rather
 * than let the server start against a half-built schema.
 */

import { applyAuthMigrations } from './migrate.ts';
import { pool } from './db.ts';

try {
  await applyAuthMigrations();
} catch (error) {
  console.error('[auth] migration failed', error);
  process.exitCode = 1;
} finally {
  // Without this the pool's idle connections keep the event loop alive and the process hangs
  // instead of exiting — which in a job means a step that never completes.
  await pool.end();
}
