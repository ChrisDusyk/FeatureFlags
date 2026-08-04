import { serve } from '@hono/node-server';
import { Hono } from 'hono';

import { auth } from './auth.ts';
import { isProduction, port } from './config.ts';
import { pool } from './db.ts';
import { applyAuthMigrations } from './migrate.ts';

const app = new Hono();

// Liveness: the process is up and answering. Deliberately touches nothing else.
app.get('/alive', (context) => context.text('Alive'));

// Readiness: Aspire holds the server back until this passes, and the server's first
// EF migration adds a trigger to a table Better Auth owns — so "ready" has to mean
// the database is reachable and migrated, not merely that the port is open.
app.get('/health', async (context) => {
  try {
    await pool.query('SELECT 1');
    return context.text('Healthy');
  } catch (error) {
    console.error('[auth] health check failed', error);
    return context.text('Unhealthy', 503);
  }
});

app.all('/api/auth/*', (context) => auth.handler(context.req.raw));

if (!isProduction) {
  await applyAuthMigrations();
}

serve({ fetch: app.fetch, port }, (info) => {
  console.info(`[auth] listening on port ${info.port}`);
});
