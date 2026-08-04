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

`AppDbContextFactory` supplies a design-time connection string so the CLI can build the model without Aspire. In Development the server applies pending migrations at startup via `ApplyMigrationsAsync()`; deployed environments should migrate as a deliberate step instead.

**The `AddUsersMirror` migration depends on `auth."user"` already existing**, because it puts a trigger on it. That is why `AppHost.cs` has the server `WaitFor(auth)` — running `dotnet ef database update` against a database the auth service has never touched will fail.

## Authentication

Identity is owned by [Better Auth](https://www.better-auth.com/), which is a Node library, so it runs as its own Aspire resource in `auth/` (Hono + `@hono/node-server`). The console is static files in production and cannot host it.

```
browser ──▶ /api/auth/*  ──▶ server (YARP forwarder) ──▶ auth service ──▶ auth schema
        └─▶ /api/*       ──▶ server (JWT bearer)     ──▶ public schema
```

- **One origin, on purpose.** The browser never addresses the auth service directly; `app.MapForwarder("/api/auth/{**catch-all}", …)` in `Program.cs` proxies to it. That is what keeps the session cookie first-party. In development Vite already proxies `/api` to the server, so the same path works.
- **Two schemas, one database.** Better Auth's tables (`user`, `session`, `account`, `verification`, `jwks`) live in the `auth` schema — its pool pins `search_path` there in `auth/src/db.ts`. The application's tables stay in `public`. There is no foreign key between them: EF's migration history has no business depending on tables another tool migrates.
- **`public.users` is a mirror, not a source.** A trigger (`public.mirror_auth_user`, added by the `AddUsersMirror` migration) copies inserts, updates, and deletes across in the same transaction as the write. The domain `User` has a `FromPersisted` factory and no `Create` or mutators, and `IUserRepository` is read-only, because nothing in this application authors an identity.
- **Cookies sign in; tokens call the API.** The console trades its session cookie for a short-lived ES256 JWT at `/api/auth/token` (`frontend/src/auth/token.ts` caches it in memory only). The .NET API validates it against the auth service's JWKS — no session lookup, no call back. Better Auth defaults to EdDSA, which `Microsoft.IdentityModel` cannot validate; **keep it on ES256**.
- **Roles are `user` and `admin`**, a single value on the user. `UserRole` is the domain type, `AuthPolicies.SignedIn` / `AuthPolicies.Admin` are the policies, and the claim name the token carries has to stay in step with `AuthClaims`. There is no organization entity yet — "their organization" is the single implicit one, and the Members screen is still `<Unbuilt>`.
- **The first account to sign up becomes the admin** (a `databaseHooks.user.create.before` hook in `auth/src/auth.ts`); everyone after it is a `user`. There is no seeded credential.
- **The issuer and audience are fixed strings**, not URLs — `auth/src/config.ts` and `AuthenticationExtensions` must agree on them, and neither needs changing when a hostname does.
- The auth service applies its own migrations at startup outside production, mirroring `ApplyMigrationsAsync()`. It uses `getMigrations` from `better-auth/db/migration`, which reconciles the live schema against the plugin configuration rather than replaying versioned files — so adding a plugin is all it takes to change the schema.
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
