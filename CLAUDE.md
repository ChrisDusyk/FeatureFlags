# FeatureFlags

A feature flag platform: .NET 10 API (`FeatureFlags.Server`) orchestrated by .NET Aspire, with a React/Vite frontend. Backend follows Domain-Driven Design with vertical slice architecture, railway-oriented error handling, and an Option type in place of nulls.

## Solution layout

```
FeatureFlags.AppHost/            Aspire orchestration (Postgres, Redis, auth, server, frontend)
FeatureFlags.Domain/             Entities, value objects, Shared/ (Result, Option) — zero project references
FeatureFlags.Infrastructure/     EF Core AppDbContext, Postgres, repository implementations — depends on Domain
FeatureFlags.Server/             API host. Features/ holds vertical slices. Composition root (Program.cs).
FeatureFlags.Domain.Tests/       xUnit tests for domain logic and Shared/ primitives
FeatureFlags.Server.Tests/       xUnit tests for feature slices
auth/                            Node service hosting Better Auth (Hono)
frontend/                        React + Vite
```

Dependency direction is one-way: `Domain` → (nothing) ← `Infrastructure` ← `Server`. Nothing references `Server`.

## C# conventions

`.editorconfig` holds them, and `EnforceCodeStyleInBuild` puts them in `dotnet build` and CI rather than only in an IDE. **Every style rule is a warning, never an error** — a build that fails over a `var` is a build people route around. Read that file before arguing with a warning; the non-obvious rules explain themselves there.

- **Take dependencies through a primary constructor.** Use the parameter directly rather than copying it into a `_field`. Keep a field only when the constructor does real work — `FeatureFlagsRefreshService` keeps `_options` because `IOptions<T>.Value` is a read, not a passthrough.
- **Two groups deliberately do not use one, and IDE0290 does not fire on either.** The EF-materialized entities (`FeatureFlag`, `User`, `SdkKey`, `FlagState`) have a second parameterless constructor for EF; a primary constructor would force it to chain through `: this(default, null!, …)`, which is worse than what it replaces. The value objects (`EnvironmentKey`, `FlagKey`, `UserRole`, `SdkKeyKind`, `SdkKeyToken`) **cannot** have one: a primary constructor is at least as accessible as its type, so converting one would let a caller mint an instance without going through `Create`/`FromPersisted`. That is a correctness rule, not a style preference.
- **Brace style is intentionally not enforced.** `Domain` writes braceless guard clauses (`if (…) return Result.Failure(…);`); `Server` and the clients brace everything. Both stay.
- Suppress a rule at the site with a comment saying why, rather than weakening it repository-wide. `Result<TValue>` is the one example: IDE0032 wants an auto property, but `Value`'s getter refuses on a failed result, so the field and the property are not the same thing.

**Package versions live in `Directory.Packages.props`, not in any csproj.** A csproj names the package; the root file gives it a version. Two clocks run there — the platform's 10.x and the .NET client's 8.0.x floors, which are what a netstandard2.0 consumer's application gets pulled up to. Do not tidy the client's up to match; that is a breaking change for the .NET Framework, Mono, and Unity consumers the target exists for.

Adding a root `Directory.*.props` file means adding it to `FeatureFlags.Server/Dockerfile` too — restore runs inside the image against copied csproj files, and a missing versions file fails there while the solution build stays green.

## Vertical slices

Each feature lives in `FeatureFlags.Server/Features/{Aggregate}/{Slice}/`, fully self-contained:

```
Features/Flags/CreateFlag/
  CreateFlagCommand.cs
  CreateFlagHandler.cs
  CreateFlagEndpoint.cs
```

No shared `Services/`, `Controllers/`, or `Repositories/` folders that span multiple features — a slice owns its own request/response types and wiring. Cross-cutting concerns (persistence, auth) come from `Infrastructure`/`Domain`, not from other slices.

## Railway-oriented error handling

Use `FeatureFlags.Domain.Shared.Result` / `Result<T>` for anything that can fail in an expected way (validation, not-found, conflict). Do not throw for these cases — exceptions are reserved for truly unexpected failures.

- `Result.Success()` / `Result.Success(value)` / `Result.Failure(error)` / `Result.Failure<T>(error)`
- Chain with `Bind`, `Map`, `Tap`, `Ensure` (`FeatureFlags.Domain.Shared.ResultExtensions`)
- Resolve at the boundary with `Match(onSuccess, onFailure)` — typically in the minimal-API endpoint, mapping `Error.Type` to an HTTP status code

## Option over null

Domain code that can meaningfully return "nothing" (e.g. repository lookups) returns `FeatureFlags.Domain.Shared.Option<T>` instead of `T?`.

- `Option<T>.Some(value)` / `Option<T>.None`
- `Match`, `Map`, `Bind`, `Reduce`
- Convert to a `Result<T>` at the point where "not found" becomes an actual failure: `option.ToResult(Error.NotFound(...))`

## Persistence

- All EF Core / Postgres concerns live in `FeatureFlags.Infrastructure`. Domain entities are persistence-ignorant — no EF attributes; configure via `IEntityTypeConfiguration<T>` under `Infrastructure/Persistence/Configurations/`.
- `AppDbContext` is registered via `builder.AddInfrastructure()` (Infrastructure/DependencyInjection.cs), which uses the Aspire Postgres client integration (`AddNpgsqlDbContext`) against the `featureflagsdb` connection defined in `AppHost.cs`.
- Value objects map through EF value converters (see `FeatureFlagConfiguration`). Give each one a `FromPersisted` factory for rehydration so the validating `Create` stays the only public way to build a new instance.

### Migrations

`dotnet-ef` is pinned in `.config/dotnet-tools.json`; run `dotnet tool restore` once, then:

```
dotnet ef migrations add <Name> --project FeatureFlags.Infrastructure --output-dir Persistence/Migrations
```

`AppDbContextFactory` supplies a design-time connection string so the CLI can build the model without Aspire. `ApplyMigrationsAsync()` runs during startup when `FEATUREFLAGS_APPLY_MIGRATIONS` says so — defaulting to on in Development, which is what the AppHost relies on. It takes a Postgres advisory lock, so two servers starting together serialise instead of racing; that makes the in-process path safe at one replica, not a substitute for migrating deliberately when there are several. `FEATUREFLAGS_MIGRATE_ONLY=true` migrates and then exits, which is how the Helm chart's job orders it.

**The `AddUsersMirror` migration depends on `auth."user"` already existing**, because it puts a trigger on it. That is why `AppHost.cs` has the server `WaitFor(auth)` — running `dotnet ef database update` against a database the auth service has never touched will fail.

## Authentication

Identity is owned by [Better Auth](https://www.better-auth.com/), which is a Node library, so it runs as its own Aspire resource in `auth/` (Hono + `@hono/node-server`). The console is static files in production and cannot host it.

```
browser  ──▶ /api/auth/*    ──▶ server (YARP forwarder) ──▶ auth service ──▶ auth schema
         └─▶ /api/*         ──▶ server (JWT bearer)     ──▶ public schema
program  ──▶ /api/evaluation ─▶ server (SDK key)        ──▶ public schema
```

- **One origin, on purpose.** The browser never addresses the auth service directly; `app.MapForwarder("/api/auth/{**catch-all}", …)` in `Program.cs` proxies to it. That is what keeps the session cookie first-party. In development Vite already proxies `/api` to the server, so the same path works.
- **Two schemas, one database.** Better Auth's tables (`user`, `session`, `account`, `verification`, `jwks`) live in the `auth` schema — its pool pins `search_path` there in `auth/src/db.ts`. The application's tables stay in `public`. There is no foreign key between them: EF's migration history has no business depending on tables another tool migrates.
- **`public.users` is a mirror, not a source.** A trigger (`public.mirror_auth_user`, added by the `AddUsersMirror` migration) copies inserts, updates, and deletes across in the same transaction as the write. The domain `User` has a `FromPersisted` factory and no `Create` or mutators, and `IUserRepository` is read-only, because nothing in this application authors an identity.
- **Cookies sign in; tokens call the API.** The console trades its session cookie for a short-lived ES256 JWT at `/api/auth/token` (`frontend/src/auth/token.ts` caches it in memory only). The .NET API validates it against the auth service's JWKS — no session lookup, no call back. Better Auth defaults to EdDSA, which `Microsoft.IdentityModel` cannot validate; **keep it on ES256**.
- **Roles are `user` and `admin`**, a single value on the user. `UserRole` is the domain type, `AuthPolicies.SignedIn` / `AuthPolicies.Admin` are the policies, and the claim name the token carries has to stay in step with `AuthClaims`. There is no organization entity yet — "their organization" is the single implicit one, and the Members screen is still `<Unbuilt>`.
- **Two kinds of credential, one header, and the policies are what keep them apart.** A user's JWT and an `SdkKey` both arrive as `Authorization: Bearer`; a policy scheme (`AuthSchemes.Any`) forwards on the `ff?_` prefix *shape*, not on the kinds this build knows, which is a total test because a JWT's first segment always begins `ey`. Keep it looser than `SdkKeyKind.All`: routing happens before the row is read, so a token of a kind added later still has to reach the handler that can look it up. **Every policy names its scheme** — `RequireAuthenticatedUser()` is satisfied by any authenticated principal, so without `AddAuthenticationSchemes` an SDK key would pass `SignedIn` and be handed the whole console API. Do not remove that pinning, and give a new policy a scheme when you add one. `AuthPoliciesTests` is the guard.
- **A browser cannot keep a secret, so there are two key kinds.** `ffs_` is secret and server-side only; `ffp_` is publishable and expected to be seen. They read the same thing — the kind decides where a key may be used *from*. **The enforcement is `BrowserCredentialRule`, not CORS**: a request carrying an `Origin` header came from a browser (it is a forbidden header, so script cannot forge it), and a secret key arriving with one is refused. CORS only decides which origins may read the answer, from `FEATUREFLAGS_BROWSER_ORIGINS` — it is the browser's rule and says nothing about a key lifted out of a bundle. Do not collapse the two into one check.
- **An SDK key is scoped to one environment, and that is where the environment comes from.** `GET /api/evaluation` takes no environment parameter — it reads the claim the key's row produced, so there is nothing for a caller to widen. The token is `ffs_{env}_{selector}_{secret}`: the selector is a public indexed lookup handle, the secret is SHA-256'd at rest (a fast hash on purpose — it is 256 bits of CSPRNG output, so there is no dictionary to run). **The segments are hex, not base64url**, because base64url's alphabet contains the `_` the format separates on. The environment segment is decoration and is never trusted; the row decides.
- **`/api/evaluation` is the only cached route, and the only reason Redis is not vestigial.** It answers with key→boolean and an ETag, through `HybridCache` on a few seconds' TTL. The console deliberately reads the admin listing instead, so what an operator sees after flipping a switch is never stale.
- **The first account to sign up becomes the admin** (a `databaseHooks.user.create.before` hook in `auth/src/auth.ts`); everyone after it is a `user`. There is no seeded credential.
- **The issuer and audience are fixed strings**, not URLs — `auth/src/config.ts` and `AuthenticationExtensions` must agree on them, and neither needs changing when a hostname does.
- **Trusted origins have to cover Aspire's `<resource>-<app>.dev.localhost` hostnames**, which is what a browser actually loads — the bare `localhost:<port>` URLs are internal. Testing the auth path with the internal URL passes the origin check while the browser fails it, so reproduce against the external URL from `aspire describe`.
- The auth service applies its own migrations at startup on the same terms as the server (`FEATUREFLAGS_APPLY_MIGRATIONS`, defaulting to on outside production), and `pnpm migrate` runs the same work as a step that can be ordered. It uses `getMigrations` from `better-auth/db/migration`, which reconciles the live schema against the plugin configuration rather than replaying versioned files — so adding a plugin is all it takes to change the schema.
- **Its `/health` probes for `auth."user"`, not just database reachability.** Because the server waits on that health check and its own migration puts a trigger on that table, reporting healthy too early would let the server start and then fail to migrate. That check is what orders the two migrations in the compose bundle, with no orchestration involved — so do not weaken it to a `SELECT 1`.
- `BETTER_AUTH_SECRET` is an Aspire parameter; set it locally with `dotnet user-secrets set "Parameters:auth-secret" <value>` in `FeatureFlags.AppHost`. Publishing also requires a `console-origin` parameter, which is the origin the browser sees.
- `pnpm build` in `auth/` type-checks and compiles to `dist/`. Node runs the TypeScript in `src/` directly in development, so nothing there may rely on TypeScript emitting code (`erasableSyntaxOnly` enforces it).

## Frontend

The admin console (`frontend/`) is a React Router SPA that mirrors the backend's slice layout: a screen lives in `src/features/{aggregate}/{Screen}Page.tsx` and owns its own copy and content, while `src/shell/` holds the chrome every screen shares (`AppShell`, `ChromeRail`, `EnvironmentSpine`, `PageHeader`, `Unbuilt`).

- **Design tokens** live in `src/styles/tokens.css`. Take colour, type, and spacing from there rather than hard-coding values. Colour carries one meaning: heat marks what is *live*, not what is *healthy* — amber is an enabled flag or production, never a success state.
- **The environment is indicated and controlled by two separate things.** `EnvironmentSpine` is a non-interactive band of the working environment's colour down the edge of the window, so the blast radius of any change stays on screen; `EnvironmentSwitcher` is the labelled dropdown that changes it, in the rail on desktop and in the top bar on mobile. Keep those jobs apart — an ambient colour band is not a control. Environments are hard-coded in `src/shell/environment.ts` until the backend owns them.
- **Navigation** is defined once in `src/shell/navigation.ts` and consumed by both the rail and the overview. Adding a screen means adding an entry there plus a route in `src/routes.tsx`.
- **Screens without a feature behind them** use `<Unbuilt>` — it states plainly what will live there rather than dressing an empty page up as a finished one. Never fill a screen with invented data.
- **The auth screens sit outside `AppShell`** (`src/features/auth/`), with no rail and no environment spine: before you have signed in there is no working environment to be in, and showing one would claim a context you do not have. `RequireAuth` wraps everything else and carries the attempted deep link across in navigation state.
- **Call the API through `apiFetch`** (`src/auth/token.ts`), never bare `fetch` — it attaches the bearer token and retries once when the API rejects a stale one. Read the signed-in user from `useCurrentUser()`, which reflects what the *server* will allow, rather than decoding the token in the browser.
- `app.MapFallbackToFile("index.html")` in `Program.cs` serves the SPA for client routes in deployed builds; Vite handles it in development.
- `pnpm build` type-checks and builds; `pnpm lint` runs ESLint.

## Testing

- `FeatureFlags.Domain.Tests` covers domain logic and the `Result`/`Option` primitives in isolation.
- `FeatureFlags.Server.Tests` covers feature slices end-to-end as they're added.
- Run the whole suite with `dotnet test FeatureFlags.sln`.
- There is no JavaScript test runner in `frontend/` or `auth/` yet, so the auth path is covered on the .NET side (claims mapping, the authorization policies, the `User` mirror) and verified against a running stack.

## Running the app

Use the Aspire CLI (see the `aspire` skill) rather than `dotnet run` directly — it starts the AppHost, Postgres, Redis, the auth service, the server, and the Vite frontend together, and exposes the dashboard for logs/traces.

## Deployment

`deploy/` holds what a self-hoster runs: a Docker Compose bundle for a single host and a Helm chart for Kubernetes, both consuming `featureflags-server` and `featureflags-auth` images from GHCR. See `docs/self-hosting.md`.

**Those artifacts are hand-authored, and the AppHost is the development loop — they are not generated from each other, so a change to the resource graph has to be made in both.** Adding an environment variable, a dependency, or a service to `AppHost.cs` means changing `deploy/compose/docker-compose.yml` and the chart's templates too. That duplication is deliberate: the compose file and `values.yaml` are a documented product surface with defaults chosen for a stranger, which is not what `aspire publish` emits.

- **The consumer-facing configuration surface is `FEATUREFLAGS_*`,** translated into the Aspire keys the code actually reads by `FeatureFlags.Server/Hosting/SelfHostConfiguration.cs`. It fills only keys that are unset, so Aspire always wins under the AppHost. Add a new setting there rather than making a consumer learn `ConnectionStrings__featureflagsdb` or `services__auth__http__0`.
- **`FEATUREFLAGS_ORIGIN` is one value on purpose.** It drives Caddy's site address, `BETTER_AUTH_URL`, `BETTER_AUTH_TRUSTED_ORIGINS`, and the ingress host. They have to agree, and a mismatch fails at somebody's first sign-in rather than at startup — keep them derived from the one value rather than configured separately. Its *shape* is checked where it is set (`NormaliseConsoleOrigin` at startup, `featureflags.origin` while templating): a missing scheme or a trailing path can never match an `Origin` header, so those are refused rather than deployed. Whether the hostname is the right one is not checkable, and that is the distinction to keep — do not "improve" either check into rejecting well-formed origins.
- **Health endpoints are mapped in every environment** (`Extensions.cs`). They were Development-only, which meant `/health` fell through to `MapFallbackToFile` in Production and answered 200 with the console's HTML — a probe passing falsely. Do not put that gate back.
- **The auth service must never be exposed directly** — no published port, no ingress rule, in any artifact. Same origin through the server's forwarder is what keeps the session cookie first-party.
- Images build with `docker build -f FeatureFlags.Server/Dockerfile .` (context is the repository root, because it builds the console too) and `docker build auth/`. The server's runtime image is chiseled: no shell, so no `HEALTHCHECK` and nothing to exec into.
- A `v*` tag releases both images, the chart, and the OpenAPI document together (`.github/workflows/release.yml`). Client libraries version separately on `sdk-dotnet-v*` / `sdk-node-v*` tags — see `clients/README.md`. Their compatibility surface is `GET /api/evaluation` and the SDK key format, not the admin API, which is closed to SDK keys.
