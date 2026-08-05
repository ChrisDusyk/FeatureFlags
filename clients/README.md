# Client libraries

Where the NuGet and npm packages will live:

```
clients/dotnet/    FeatureFlags.Client         → NuGet
clients/node/      @featureflags/client        → npm
```

The release plumbing exists (`.github/workflows/sdk-release.yml`, tag prefixes `sdk-dotnet-v*`
and `sdk-node-v*`), and the OpenAPI document they will be generated from is produced at build
time and attached to every platform release. **The libraries themselves do not exist yet, and
cannot usefully be written against today's API.**

## What has to land first

There is no way for a program to authenticate to this API.

The only credential the system issues is a user JWT, minted by exchanging a browser session
cookie at `/api/auth/token`, and it lives fifteen minutes. A server-side SDK cannot hold a
session cookie, so a client library written today would have to impersonate the console — which
is neither something to document nor something to support.

The API is also the wrong shape for one. Every endpoint is admin CRUD:

| | |
|---|---|
| `GET /api/flags?environment=` | every flag, with console metadata, for one environment |
| `POST /api/flags` | create |
| `PUT /api/flags/{key}/state` | set a flag's state in one environment |
| `GET /api/users/me` | the signed-in user |

There is no evaluation endpoint. An SDK would have to fetch the full admin listing and index it
by key itself, with no ETag, no cursor, no push, and no server-side caching — polling an admin
API in a hot path.

So two pieces of work come first, and both are backend changes rather than SDK ones:

1. **SDK keys.** A credential a program can hold: issued per environment, revocable, and
   presented as a bearer token the server recognises without a user behind it. The console
   already promises this in copy (`EnvironmentsPage` mentions rotating SDK keys) and nothing
   implements it. This also decides how the deployment looks — SDK traffic is machine traffic,
   which is the point at which Redis stops being vestigial.
2. **An evaluation endpoint.** Something shaped for reading rather than administering: the flag
   states for one environment, cacheable, with an ETag so a poll costs a 304. `GET /api/flags/{key}`
   does not exist either, despite `CreateFlagEndpoint` returning a `Location` header that points
   at it.

Only then is there something worth generating a client for.

## Versioning

Client libraries version independently of the platform, against a documented minimum server
version. An SDK churns on a different clock than the thing it talks to, and tying them together
would mean publishing a no-op package release on every platform bump.
