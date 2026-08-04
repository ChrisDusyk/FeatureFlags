import pg from 'pg';

import { authSchema, database } from './config.ts';

/**
 * Better Auth talks to Postgres through this pool with its search path pinned to the
 * `auth` schema, which is what keeps its tables out of the application's. Every
 * unqualified name it creates or queries resolves there and nowhere else — in
 * particular it can neither see nor collide with `public.users` or `public.feature_flags`.
 */
export const pool = new pg.Pool({
  host: database.host,
  port: database.port,
  user: database.user,
  password: database.password,
  database: database.database,
  options: `-c search_path=${authSchema}`,
});

/**
 * Creates the schema the pool's search path points at. Postgres does not validate a
 * search path when a connection opens, so this can safely run over that same pool.
 */
export async function ensureAuthSchema(): Promise<void> {
  // `authSchema` is a module constant, never request data.
  await pool.query(`CREATE SCHEMA IF NOT EXISTS "${authSchema}"`);
}
