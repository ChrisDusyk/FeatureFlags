import { serve } from '@hono/node-server';
import { Hono } from 'hono';

import { auth } from './auth.ts';
import { applyMigrations, authSchema, port } from './config.ts';
import { pool } from './db.ts';
import { applyAuthMigrations } from './migrate.ts';

const app = new Hono();

// Liveness: the process is up and answering. Deliberately touches nothing else.
app.get('/alive', (context) => context.text('Alive'));

// Readiness: Aspire holds the server back until this passes, and the server's migration
// adds a trigger to a table Better Auth owns — so "ready" has to mean the database is
// reachable *and* migrated, not merely that the port is open.
//
// Probing for the table rather than running SELECT 1 is what makes that true outside
// development, where migrations are a deliberate step instead of something startup does:
// answering healthy with no auth."user" would let the server start and its migration fail.
app.get('/health', async (context) => {
  try {
    // to_regclass answers NULL for a missing table or schema instead of raising.
    const { rows } = await pool.query<{ ready: boolean }>(
      'SELECT to_regclass($1) IS NOT NULL AS ready',
      [`${authSchema}."user"`],
    );

    if (!rows[0]?.ready) {
      console.error(`[auth] schema "${authSchema}" has not been migrated.`);
      return context.text(`Schema "${authSchema}" has not been migrated.`, 503);
    }

    return context.text('Healthy');
  } catch (error) {
    console.error('[auth] health check failed', error);
    return context.text('Unhealthy', 503);
  }
});

app.all('/api/auth/*', (context) => auth.handler(context.req.raw));

if (applyMigrations) {
  await applyAuthMigrations();
}

serve({ fetch: app.fetch, port }, (info) => {
  console.info(`[auth] listening on port ${info.port}`);
});
