# Client libraries

Where the NuGet and npm packages live:

```
clients/dotnet/    FeatureFlags.Client         → NuGet
clients/node/      @featureflags/client        → npm
```

The release plumbing is in `.github/workflows/sdk-release.yml`, on the tag prefixes
`sdk-dotnet-v*` and `sdk-node-v*`. The OpenAPI document is produced at build time and attached to
every platform release. `.github/workflows/clients-ci.yml` is the pull-request side of the same
pair — both packages built, tested, and linted on any change under `clients/`. The .NET job builds
the library on its own rather than through its tests, because it multi-targets and a
`ProjectReference` from the `net10.0` test project only ever builds the `net10.0` one.

## What a client talks to

Two things had to land in the platform first, and both have:

1. **SDK keys.** A credential a program can hold: issued per environment from the console, revocable,
   and presented as a bearer token the server recognises without a user behind it. Before this the
   only credential the system issued was a fifteen-minute user JWT minted from a browser session
   cookie — nothing a server-side SDK could hold without impersonating the console.
2. **An evaluation endpoint.** `GET /api/evaluation` answers with the flag states for the key's
   own environment and an ETag, so a poll that finds nothing changed costs a 304. Before this the
   only read was the admin listing, with console metadata, no ETag, and no server-side caching.

A client therefore needs exactly two settings — the origin and the key — and polls one endpoint.
The environment is not configurable because the key carries it.

## Two kinds of key

`ffs_` keys are secret and server-side only. `ffp_` keys are publishable and may be shipped to a
browser. Both read the same thing; the kind decides where a key may be used *from*, and the server
enforces it — a request carrying an `Origin` header must present a publishable key.

The node client runs in both places and refuses to start with a secret key in a browser, so the
mistake surfaces at the line that configured it rather than as a 401 after the key is already in a
bundle. The .NET client is server-side only and has no such check to make.

The admin endpoints (`GET /api/flags?environment=`, `POST /api/flags`, `PUT /api/flags/{key}/state`,
`GET /api/users/me`, the `sdk-keys` routes) are **closed to SDK keys**. They are the console's, they
require a user's token, and a client library has no business calling them. A management client for
automation is a separate thing from a feature-flag SDK, and would need a credential that does not
exist yet.

## Versioning

Client libraries version independently of the platform, against a documented minimum server
version. An SDK churns on a different clock than the thing it talks to, and tying them together
would mean publishing a no-op package release on every platform bump.

`GET /api/evaluation` and the SDK key format are the compatibility surface. The minimum server
version for any client published from here is the first release that carries them.
