# Repository Guidelines

These rules apply to `backend/`; see [../AGENTS.md](../AGENTS.md) for change planning and commit gates. Read the active change plan before implementation. Never edit archived changes.

## Commands

From the repository root:

- `dotnet restore backend/PennyPath.slnx` and `dotnet tool restore` restore projects and the pinned EF tool.
- `dotnet build backend/PennyPath.slnx --no-restore` builds API and tests.
- `dotnet test backend/PennyPath.slnx` runs xUnit v3; `--filter Category=Auth` / `Category=Persistence` select protocol / storage tests.
- `dotnet tool run dotnet-ef migrations script --idempotent --project backend/PennyPath.Api --output artifacts/private-account-access.sql` generates PostgreSQL SQL without a live database; create `artifacts/` first.
- `dotnet run --project backend/PennyPath.Api --launch-profile https` runs Development on dynamically assigned ports; read the URLs from startup logs.

## Structure and conventions

`PennyPath.Api` targets net10.0. `Program.cs` wires middleware and services; `Controllers/` holds HTTP endpoints, `Auth/` holds cookie/CSRF/proxy configuration, and `Data/` holds Identity EF configuration and migrations. `PennyPath.Api.Tests` uses xUnit, WebApplicationFactory, SQLite and a controlled TimeProvider. SQLite is test-only; container tests exercise PostgreSQL.

Use four spaces, file-scoped namespaces, braces on separate lines and PascalCase types/members. Keep files aligned with types. Add new projects to `PennyPath.slnx` and explicitly allow required build sources in the root `.dockerignore`, excluding bin/obj. Docker builds must execute tests in a stage consumed by the final image.

## Account and deployment contracts

- IdentityUser.Id / ClaimTypes.NameIdentifier is the stable owner ID for S-02, independent of email; derive ownership from the authenticated principal. Expense isolation is implemented in S-02.
- Self-registration requires no email confirmation and creates no session. Passwords are 12–128 characters. Password recovery remains S-08.
- `__Host-PennyPath.Auth` is Secure/HttpOnly, persistent for at most 7 days without sliding renewal. Preserve the original deadline during security-stamp refresh.
- All auth POSTs require Antiforgery in X-CSRF-TOKEN, including anonymous register/login. Identity changes require a fresh token. HTTP auth is rejected, API errors are ProblemDetails (401/403, no HTML redirects).
- The fallback policy requires authentication; deliberately public endpoints, static files and API/SPA fallbacks must remain explicitly anonymous. Keep HTTP examples in `PennyPath.Api.http` accurate.
- `UseForwardedHeaders` runs before auth and trusts only `Proxy:KnownProxy`, the exact Caddy IP; never trust all private networks or arbitrary incoming forwarded headers.
- Production requires `DataProtection:KeysPath` (Compose: `/var/lib/pennypath/keys`) with persistent private storage owned by APP_UID and application name `PennyPath`. Development/Testing use different application names; tests use ephemeral keys.
- Database configuration: `Database:Host`, `Port`, `Name`, `Username`, `PasswordFile`. Read the password file; never log credentials, cookie/token contents or provider errors. Compose mounts the file 0444 inside a host directory 0700.
- `dotnet PennyPath.Api.dll --migrate` changes schema once, exits 0/nonzero and never listens for HTTP. Normal startup never migrates. `deploy.sh` migrates before replacing app; explicit rollback skips migration and preserves schema/keys/CA.

The runtime is non-root and serves internal HTTP 8080; Caddy terminates LAN HTTPS. The published app port is loopback-only. `/health` checks the process, not database/schema readiness. Root `scripts/test-auth.py` checks trusted HTTPS sessions; `scripts/test-compose.sh` tests the ARM64 image, PostgreSQL, secret/key permissions and rollback. Run full relevant checks and report unavailable Docker gates honestly; do not substitute SQLite for PostgreSQL or disable TLS/CSRF for tests.
