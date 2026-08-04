import { getMigrations } from 'better-auth/db/migration';

import { auth } from './auth.ts';
import { authSchema } from './config.ts';
import { ensureAuthSchema } from './db.ts';

/**
 * Brings the `auth` schema up to the shape the configured Better Auth plugins expect.
 *
 * This is the programmatic form of `better-auth migrate`, which only works against the
 * built-in Kysely adapter — the raw `pg.Pool` in db.ts is exactly that. It reconciles
 * the live schema against the configuration rather than replaying versioned files, so
 * there is no migration history to keep in step.
 *
 * Intended for local development against the Aspire-managed Postgres container, on the
 * same terms as the server's `ApplyMigrationsAsync()`; deployed environments should
 * migrate as a deliberate step.
 */
export async function applyAuthMigrations(): Promise<void> {
  await ensureAuthSchema();

  const { toBeCreated, toBeAdded, runMigrations } = await getMigrations(auth.options);

  if (toBeCreated.length === 0 && toBeAdded.length === 0) {
    console.info(`[auth] schema "${authSchema}" is up to date.`);
    return;
  }

  const created = toBeCreated.map((migration) => migration.table);
  const altered = toBeAdded.map((migration) => migration.table);

  await runMigrations();

  console.info(
    `[auth] migrated schema "${authSchema}" — created [${created.join(', ')}], altered [${altered.join(', ')}].`,
  );
}
