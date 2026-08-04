# FeatureFlags Helm chart

```sh
helm install featureflags oci://ghcr.io/chrisdusyk/charts/featureflags \
  --namespace featureflags --create-namespace \
  --set origin=https://flags.example.com \
  --set betterAuth.secret="$(openssl rand -base64 32)" \
  --set postgres.password="$(openssl rand -base64 24)"
```

Then open the origin and create an account. **The first account to sign up becomes the admin.**

Putting secrets on the command line leaves them in your shell history. For anything real, create
a Secret yourself and point `betterAuth.existingSecret` at it.

## What gets created

Two Deployments — `server` (the API and the console's static files) and `auth` (Better Auth) —
plus an Ingress to the server, and optionally Postgres and Redis.

Only the server is routed to. The auth `Service` is `ClusterIP` with no Ingress on purpose:
the browser is meant to reach it only through the server's `/api/auth` forwarder, on the
console's own origin, because that is what keeps the session cookie first-party. Do not add an
ingress rule for `/api/auth` — that path is already handled, and splitting it would break
sign-in rather than speed it up.

## Values worth knowing about

| Value | Default | |
|---|---|---|
| `origin` | — | Required. The full origin including scheme. Also supplies the ingress host. |
| `migrations.mode` | `job` | `job`, `auto`, or `off`. See below. |
| `postgres.bundled` | `true` | Set `false` and `postgres.external.url` to use your own. |
| `redis.bundled` | `true` | Likewise `redis.external.url`. |
| `betterAuth.existingSecret` | `""` | A Secret with a `BETTER_AUTH_SECRET` key. Preferred over `betterAuth.secret`. |
| `server.replicas` | `1` | Safe to raise — but read the migration note first. |

## Migrations

Two schemas migrate independently, and the order between them is not optional: the server's EF
migration puts a trigger on `auth."user"`, so Better Auth has to have created that table first.

- **`job`** (default) — a `pre-install`/`pre-upgrade` hook Job migrates the auth schema in an init
  container and the application schema in the main one, so the order is structural. A failure
  fails the release before any Deployment is touched. This is the default because a chart is the
  shape most likely to run more than one replica.
- **`auto`** — the server migrates as it starts, behind a Postgres advisory lock, with an init
  container that waits for the auth service's readiness probe (which itself checks for
  `auth."user"`). Reasonable at one replica.
- **`off`** — nothing migrates. The server will not pass readiness until something does.

## Using your own database

```yaml
postgres:
  bundled: false
  external:
    existingSecret: featureflags-db   # holding a FEATUREFLAGS_DATABASE_URL key
```

The server and the auth service must point at the *same* database — they share it and separate by
schema (`public` and `auth`), which is why the chart offers one value and not two.

The bundled Postgres has no backups, no failover, and one replica. It exists so `helm install`
works on its own.

## Upgrading

```sh
helm upgrade featureflags oci://ghcr.io/chrisdusyk/charts/featureflags --reuse-values
```

The chart version, the image tag, and the app version move together — one release tag covers all
three. Take a database backup first: `helm rollback` returns the manifests to their previous
state, not the schema.
