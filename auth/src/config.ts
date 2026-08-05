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
  //
  // FEATUREFLAGS_DATABASE_URL is the same thing under the name a self-hosting operator
  // sets, shared with the server so that one variable configures both. Aspire's own
  // variable is checked first, for the same reason the server's translation defers to
  // it: under the AppHost, Aspire is the authority.
  const uri = process.env.FEATUREFLAGSDB_URI ?? process.env.FEATUREFLAGS_DATABASE_URL;

  if (uri) {
    // `new URL()` on something that is not one throws a TypeError naming neither the variable
    // nor what was wrong with it, and this runs at import: the container would exit on a stack
    // trace with no way back to the value that caused it. The server rejects the same input for
    // the same reason, so whichever half of the stack starts first says the same thing.
    let parsed: URL;

    try {
      parsed = new URL(uri);
    } catch {
      const name = process.env.FEATUREFLAGSDB_URI ? 'FEATUREFLAGSDB_URI' : 'FEATUREFLAGS_DATABASE_URL';

      throw new Error(
        `${name} has to be a postgres:// URL, e.g. postgres://user:password@host:5432/featureflagsdb. ` +
          'The .NET server reads this same variable, so both accept the one format. A password ' +
          "containing '/', '@', ':' or '#' has to be percent-encoded ('/' as %2F, '@' as %40).",
      );
    }

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

const isProduction = process.env.NODE_ENV === 'production';

/**
 * Whether to reconcile the `auth` schema during startup.
 *
 * Mirrors the server's `FEATUREFLAGS_APPLY_MIGRATIONS`, and defaults the same way: on
 * outside production, which is what the AppHost relies on. The compose bundle turns it
 * on explicitly because it runs one replica of each service; the Helm chart leaves it
 * off and runs `pnpm migrate` as a job instead.
 *
 * Ordering matters wherever this is decided: the server's own migration puts a trigger
 * on `auth."user"`, so this has to have run before that one does. What enforces it is
 * the readiness check in server.ts, which stays 503 until the table exists.
 */
// Parsed case-insensitively to match .NET's configuration binding on the server, which accepts
// "True" as readily as "true". One variable configures both services, so a stricter reading here
// would let `FEATUREFLAGS_APPLY_MIGRATIONS=True` migrate one schema and not the other — and the
// half that skipped is the one the other depends on.
export const applyMigrations = process.env.FEATUREFLAGS_APPLY_MIGRATIONS
  ? process.env.FEATUREFLAGS_APPLY_MIGRATIONS.trim().toLowerCase() === 'true'
  : !isProduction;
