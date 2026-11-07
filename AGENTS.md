# AGENTS.md

Guidance for AI agents (and humans) working in this repository.

## Project Overview

Stashit is an internal file stashing service: upload a small-to-medium file, get a
URL back, download it later. Files expire after a server-decided TTL.

- **Stack:** .NET 10, C# (nullable enabled, implicit usings enabled), ASP.NET Core Controllers
- **Storage:** Redis (keys hold file bytes; side-key holds metadata; both share the TTL)
- **Positioning:** internal use only; auth/rate-limiting/abuse control is handled by
  an upstream HTTP proxy and is explicitly out of scope for this service

## API Contract

### POST /files

- Raw binary request body (no multipart). Upload size limit is configurable, default **100 MB**.
- Original filename and Content-Type are captured from the request headers
  (Content-Disposition filename, Content-Type) for later download.
- On success responds **201 Created** with a **Location** header pointing at
  `https://<host>/files/<id>` and an empty body.
- On failure: 413 if over the size limit, 415 for disallowed content types (if configured).

### GET /files/{id}

- Streams the stored bytes. Responds 200 with the **original Content-Type** and
  `Content-Disposition: attachment; filename="<original>"`.
- 404 if the file does not exist or has expired.

There is deliberately no delete endpoint — expiry is the only lifecycle mechanism.

## Configuration (IOptions pattern)

All tunables go in `appsettings.json` and bind via typed config in `Program.cs`.

| Setting             | Env var                   | Default  | Notes                          |
|---------------------|---------------------------|----------|--------------------------------|
| `Storage__BaseUrl`  | `STORAGE__BASEURL`        | `Redis://localhost:6379` | Redis connection |
| `Limits__MaxUploadBytes` | `LIMITS__MAXUPLOADBYTES` | 104857600 (100 MB) | Enforced on upload |
| `Limits__DefaultTtl` | `LIMITS__DEFAULTTTL`     | `24:00:00` (TimeSpan) | Expiry applied server-side |

## Architecture Guidelines

- **Thin controllers.** Business logic lives in `Services/` (e.g. `IStashService` /
  `StashService`), registered via DI. Controllers map HTTP <-> domain only.
- **Redis usage:** use `StackExchange.Redis` directly (no ORM/abstraction layer needed).
  Key scheme:
  - `stash:data:{id}` — file bytes (binary)
  - `stash:meta:{id}` — metadata (JSON: originalFilename, contentType, uploadedAt)
  - Both keys get the same TTL at write time via `Expire`.
- IDs: random, unguessable (e.g. 128-bit base64url), not sequential.
- **Streaming:** stream request body into Redis; do not buffer whole uploads in
  managed memory beyond what the Redis client requires.
- Error handling: return appropriate 4xx responses with a short JSON message body;
  don't leak internals (Redis errors surface as 500s with a generic message).

## Code Conventions

- File/class/namespace names match PascalCase project convention.
- `var` for local variables when type is obvious; explicit types for public APIs.
- Async suffix (`...Async`) on async methods; `ConfigureAwait(false)` is NOT used
  (this is an ASP.NET Core app with the default SynchronizationContext model).
- XML doc comments on public members of Services and Controllers.
- No comments in code unless explaining a non-obvious "why".
- Follow existing patterns in the repo before introducing new dependencies.

## Development Workflow

### Run locally

```bash
docker compose up -d redis   # starts Redis on localhost:6379
dotnet run --project src/Stashit
```

### Run as a container

```bash
docker build -t stashit .
docker run --rm -it \
  --network stashit_default \
  -p 8080:8080 \
  stashit
# or, against the docker-compose Redis on the host:
docker run --rm -it \
  -p 8080:8080 \
  -e STORAGE__BASEURL=localhost:6379 \
  stashit
```

The Dockerfile is a two-stage build: `mcr.microsoft.com/dotnet/sdk:10.0` for
restore/publish, `mcr.microsoft.com/dotnet/aspnet:10.0` for the runtime. It
publishes with `UseAppHost=false` so the final image only contains the
framework-dependent `.dll` and assets. The runtime image defaults
`STORAGE__BASEURL` to `redis:6379` — a typical Docker Network service name.
Override with your Redis host:port (e.g. `localhost:6379` for the local
docker-compose Redis from the host) when deploying.

### Build / test / check

No lint tools are configured. Verification loop:

```bash
dotnet build -c Release --no-restore   # must compile with zero warnings
dotnet test -c Release                 # must pass, 100% of tests green
```

### Tests

- **Framework:** xUnit.
- **Redis in tests:** use a fake Redis (e.g. `Microsoft.Testing.Mocks` or
  `NSubstitute` for `IConnectionMultiplexer`), NOT Testcontainers — tests must run
  without Docker.
- Test classes named `<ClassUnderTest>Tests`; test methods named
  `<Method>_<Condition>_<Expected>`.
- Cover at minimum: upload happy path, size-limit rejection, download metadata
  headers, expired/missing file 404, TTL applied to keys.

### Commits

Conventional Commits style: `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`.
Keep messages imperative and < 72 chars.

## Adding a New Dependency

Justify first. Prefer the .NET BCL and already-present packages. When unavoidable,
add the package, note the reason in the PR description, and verify it's
transitively safe with `dotnet list src/Stashit package --include-transitive`.

## Out of Scope (do NOT implement)

- Authentication / authorization / rate limiting — handled by the upstream proxy.
- File deletion endpoints, listing, or search.
- Persistence beyond Redis TTL (no disk fallback, no S3).
