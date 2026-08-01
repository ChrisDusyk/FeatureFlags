# FeatureFlags

A feature flag platform: .NET 10 API (`FeatureFlags.Server`) orchestrated by .NET Aspire, with a React/Vite frontend. Backend follows Domain-Driven Design with vertical slice architecture, railway-oriented error handling, and an Option type in place of nulls.

## Solution layout

```
FeatureFlags.AppHost/            Aspire orchestration (Postgres, Redis, server, frontend)
FeatureFlags.Domain/             Entities, value objects, Shared/ (Result, Option) — zero project references
FeatureFlags.Infrastructure/     EF Core AppDbContext, Postgres, repository implementations — depends on Domain
FeatureFlags.Server/             API host. Features/ holds vertical slices. Composition root (Program.cs).
FeatureFlags.Domain.Tests/       xUnit tests for domain logic and Shared/ primitives
FeatureFlags.Server.Tests/       xUnit tests for feature slices
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

## Frontend

The admin console (`frontend/`) is a React Router SPA that mirrors the backend's slice layout: a screen lives in `src/features/{aggregate}/{Screen}Page.tsx` and owns its own copy and content, while `src/shell/` holds the chrome every screen shares (`AppShell`, `ChromeRail`, `EnvironmentSpine`, `PageHeader`, `Unbuilt`).

- **Design tokens** live in `src/styles/tokens.css`. Take colour, type, and spacing from there rather than hard-coding values. Colour carries one meaning: heat marks what is *live*, not what is *healthy* — amber is an enabled flag or production, never a success state.
- **The environment is indicated and controlled by two separate things.** `EnvironmentSpine` is a non-interactive band of the working environment's colour down the edge of the window, so the blast radius of any change stays on screen; `EnvironmentSwitcher` is the labelled dropdown that changes it, in the rail on desktop and in the top bar on mobile. Keep those jobs apart — an ambient colour band is not a control. Environments are hard-coded in `src/shell/environment.ts` until the backend owns them.
- **Navigation** is defined once in `src/shell/navigation.ts` and consumed by both the rail and the overview. Adding a screen means adding an entry there plus a route in `src/routes.tsx`.
- **Screens without a feature behind them** use `<Unbuilt>` — it states plainly what will live there rather than dressing an empty page up as a finished one. Never fill a screen with invented data.
- `app.MapFallbackToFile("index.html")` in `Program.cs` serves the SPA for client routes in deployed builds; Vite handles it in development.
- `pnpm build` type-checks and builds; `pnpm lint` runs ESLint.

## Testing

- `FeatureFlags.Domain.Tests` covers domain logic and the `Result`/`Option` primitives in isolation.
- `FeatureFlags.Server.Tests` covers feature slices end-to-end as they're added.
- Run the whole suite with `dotnet test FeatureFlags.sln`.

## Running the app

Use the Aspire CLI (see the `aspire` skill) rather than `dotnet run` directly — it starts the AppHost, Postgres, Redis, the server, and the Vite frontend together, and exposes the dashboard for logs/traces.
