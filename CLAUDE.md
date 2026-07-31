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

## Testing

- `FeatureFlags.Domain.Tests` covers domain logic and the `Result`/`Option` primitives in isolation.
- `FeatureFlags.Server.Tests` covers feature slices end-to-end as they're added.
- Run the whole suite with `dotnet test FeatureFlags.sln`.

## Running the app

Use the Aspire CLI (see the `aspire` skill) rather than `dotnet run` directly — it starts the AppHost, Postgres, Redis, the server, and the Vite frontend together, and exposes the dashboard for logs/traces.
