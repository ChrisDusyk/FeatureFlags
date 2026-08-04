/**
 * Everything the auth service needs from its environment, read once at startup so a
 * misconfigured resource fails immediately with the name of the missing variable
 * rather than at somebody's first sign-in.
 *
 * The values come from Aspire: the AppHost's `WithReference(featureFlagsDb)` injects
 * the `FEATUREFLAGSDB_*` connection properties, and the rest are set explicitly there.
 */

/** The Postgres schema Better Auth owns. Nothing the application writes lives here. */
export const authSchema = 'auth';

/**
 * Fixed rather than derived from the base URL: the .NET API validates these two
 * claims, and it should not have to be reconfigured because a hostname changed
 * between development and production. Trust comes from the JWKS signature.
 */
export const jwtIssuer = 'featureflags-auth';
export const jwtAudience = 'featureflags-api';

export interface DatabaseSettings {
  host: string;
  port: number;
  user: string;
  password: string;
  database: string;
}

function required(name: string): string {
  const value = process.env[name];

  if (!value) {
    throw new Error(`${name} is not set. Run the app through the Aspire AppHost.`);
  }

  return value;
}

function readDatabaseSettings(): DatabaseSettings {
  // Aspire hands non-.NET resources both a URI and the discrete properties. The URI
  // is the one documented for JavaScript apps, so prefer it and fall back to the parts.
  const uri = process.env.FEATUREFLAGSDB_URI;

  if (uri) {
    const parsed = new URL(uri);

    return {
      host: parsed.hostname,
      port: parsed.port ? Number(parsed.port) : 5432,
      user: decodeURIComponent(parsed.username),
      password: decodeURIComponent(parsed.password),
      database: parsed.pathname.replace(/^\//, ''),
    };
  }

  return {
    host: required('FEATUREFLAGSDB_HOST'),
    port: Number(required('FEATUREFLAGSDB_PORT')),
    user: required('FEATUREFLAGSDB_USERNAME'),
    password: required('FEATUREFLAGSDB_PASSWORD'),
    database: required('FEATUREFLAGSDB_DATABASENAME'),
  };
}

export const database = readDatabaseSettings();

export const port = Number(process.env.PORT ?? 3000);

export const secret = required('BETTER_AUTH_SECRET');

/**
 * The origin a browser sees is the console's, not this service's — every request
 * arrives through the server's `/api/auth` forwarder. The AppHost cannot hand that
 * endpoint over without making the resource graph circular (frontend → server → auth),
 * so in development it goes unset and the trusted origins below carry the real check.
 */
export const baseUrl = process.env.BETTER_AUTH_URL ?? `http://localhost:${port}`;

/**
 * The origins Better Auth will accept a request from, as exact values or wildcard
 * patterns such as `http://localhost:*`. In development this covers whichever port
 * Vite happened to take; in production it is the console's real origin.
 */
export const trustedOrigins = (process.env.BETTER_AUTH_TRUSTED_ORIGINS ?? '')
  .split(',')
  .map((origin) => origin.trim())
  .filter((origin) => origin.length > 0);

/** Mirrors the server's `IsDevelopment()` guard around applying migrations at startup. */
export const isProduction = process.env.NODE_ENV === 'production';
