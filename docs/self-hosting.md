# Self-hosting FeatureFlags

FeatureFlags runs as two containers — the server (the API, and the console served from its
`wwwroot`) and the auth service — against Postgres and Redis.

Two supported ways to run it:

- **[Docker Compose](../deploy/compose/README.md)** on a single host. Includes Caddy, so TLS is
  automatic and there is one origin to configure.
- **[Helm](../deploy/helm/featureflags/README.md)** on Kubernetes.

Both pull the same images from `ghcr.io/chrisdusyk/`.

## Quickstart

```sh
curl -fsSL https://github.com/ChrisDusyk/FeatureFlags/releases/latest/download/featureflags-compose.tar.gz | tar xz
cd compose
cp .env.example .env
$EDITOR .env
docker compose up -d
```

Open the origin you configured. **The first account to sign up becomes the admin**, and everyone
after it is an ordinary user. There is no seeded credential and no way to promote somebody
through the console yet, so sign up first, before anyone else can.

## Configuration

Both services read the same names, so one value configures both where they share a concern.

| Variable | Used by | |
|---|---|---|
| `FEATUREFLAGS_ORIGIN` | both | **Required.** The origin a browser loads the console on, scheme and port included. |
| `FEATUREFLAGS_DATABASE_URL` | both | `postgres://user:password@host:5432/featureflagsdb`. A native Npgsql connection string also works. |
| `BETTER_AUTH_SECRET` | auth | **Required.** Signs sessions and tokens. `openssl rand -base64 32`. |
| `FEATUREFLAGS_REDIS_URL` | server | `redis://host:6379`. |
| `FEATUREFLAGS_AUTH_URL` | server | The auth service's in-network address, e.g. `http://auth:8080`. |
| `FEATUREFLAGS_APPLY_MIGRATIONS` | both | Migrate during startup. Safe at one replica. |
| `FEATUREFLAGS_MIGRATE_ONLY` | server | Migrate, then exit. For running migrations as a deliberate step. |

### Getting the origin right

This is the setting that goes wrong. `FEATUREFLAGS_ORIGIN` has to match what the browser puts in
its address bar **exactly** — scheme, hostname, and port:

- `https://flags.example.com` and `http://flags.example.com` are different origins.
- `https://flags.example.com` and `https://www.flags.example.com` are different origins.
- `http://localhost` and `http://localhost:8080` are different origins.

The auth service refuses requests from an origin it does not trust, and nothing checks this at
startup — it fails at the first sign-in attempt, with an error that does not name the cause. If
sign-in returns `INVALID_ORIGIN`, this is why.

## Architecture, and one rule

```
browser ──▶ Caddy / ingress ──▶ server ──▶ /api/auth/* ──▶ auth service ──▶ auth schema
                                       └─▶ /api/*      ──▶ (JWT bearer)  ──▶ public schema
```

**Never expose the auth service directly.** Every deployment artifact here keeps it off the
public network on purpose. The browser reaches it only through the server's `/api/auth`
forwarder, which is what keeps the console on one origin and its session cookie first-party.
Publishing a port or adding an ingress rule for it does not add a capability — it creates a
second origin, and sign-in breaks.

The two services share one database and separate by schema: Better Auth owns `auth`, the
application owns `public`. There is no foreign key between them. `public.users` is a mirror
maintained by a trigger, not a source — nothing in this application authors an identity.

## Migrations

Two schemas migrate independently, and the order is not optional: the server's migration puts a
trigger on `auth."user"`, so Better Auth has to create that table first.

The compose bundle handles this with its `depends_on` chain — the auth service reports healthy
only once that table exists, and the server waits for healthy. On Kubernetes the default
`migrations.mode: job` makes the order structural instead, with the auth migration as an init
container.

Migrating during startup is safe at exactly one replica of the server. It takes a Postgres
advisory lock, so two instances starting together serialise rather than race — but before
running more than one deliberately, migrate as a step of its own:

```sh
# the auth schema first
docker compose run --rm auth node dist/migrate-cli.js
# then the application schema
docker compose run --rm -e FEATUREFLAGS_MIGRATE_ONLY=true server
```

## Upgrading

```sh
docker compose pull && docker compose up -d
```

Read the release notes, and take a backup first. A migration is not undone by starting the old
image again, and `helm rollback` returns manifests rather than schemas.

Pin `FEATUREFLAGS_VERSION` rather than tracking `latest` once you are past trying it out — an
unattended `docker compose pull` against a moving tag is how an upgrade happens by accident.

## Backups

Everything worth keeping is in Postgres — both schemas. Nothing in Redis matters, and the
containers hold no state.

```sh
docker compose exec -T postgres pg_dump -U featureflags -Fc featureflagsdb > featureflags.dump
```

Restore into an empty database:

```sh
docker compose exec -T postgres pg_restore -U featureflags -d featureflagsdb --clean < featureflags.dump
```

Dump both schemas together. Restoring `public` against a different `auth` leaves the mirrored
`public.users` rows pointing at identities that no longer exist.

The bundled Postgres has no backups, no failover, and one replica. It is there so the first run
works. For anything whose loss would matter, point `FEATUREFLAGS_DATABASE_URL` at a database
somebody maintains.

## Health

| Path | |
|---|---|
| `/health` | Readiness. Covers the database and the cache. |
| `/alive` | Liveness. The process is answering. |

Both are unauthenticated and return a status word and nothing else — no check names, no
durations, no exception detail.

## Observability

Set `OTEL_EXPORTER_OTLP_ENDPOINT` and both services export traces, metrics, and logs. Unset,
nothing is exported and no collector is required.
