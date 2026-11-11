---
starter_id: custom
custom_starter:
  name: "Vue 3 + TypeScript (Vite) frontend with ASP.NET Core Web API backend and PostgreSQL"
  docs_url: https://vuejs.org/guide/quick-start.html
package_manager: npm
project_name: penny-path
hints:
  language_family: multi
  team_size: solo
  deployment_target: mac-m1-orbstack-compose
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: best-effort
  path_taken: custom
  quality_override: false
  self_check_answers:
    typed: true
    from_official_starter: true
    conventions: true
    docs_current: true
    can_judge_agent: true
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: false
---

## Why this stack

PennyPath uses Vue 3 with TypeScript and Vite for its browser frontend, ASP.NET Core Web API for its backend, and PostgreSQL for expense data. The solo developer wants to learn Vue while retaining a familiar .NET backend within a five-week, after-hours MVP. Both component starters pass the registry's four agent-friendly criteria; their combined setup is custom and has best-effort scaffolding support. Use the official Vue and ASP.NET Core project generators, npm for the frontend, and dotnet/NuGet for the backend. The backend documentation is https://learn.microsoft.com/aspnet/core/. Authentication and CSV import are in scope; payments, real-time synchronization, AI processing, and background jobs are not. The selected hosting target is a home Mac M1 with OrbStack and Docker Compose; Railway remains an alternative. GitHub Actions checks PRs on hosted ARM64 runners, publishes a commit-tagged image to GHCR on main, and deploys its digest through a dedicated Mac runner once DEPLOY_ENABLED=true. PostgreSQL software is free; electricity and backups remain operational costs. All five readiness statements were confirmed. Flutter is reserved for future mobile work outside the MVP.

## Account and deployment baseline (S-01)

- Vue 3 / TypeScript / Vue Router with Vite 8. Vitest + Vue Test Utils / jsdom cover forms, sessions and routing; `npm run type-check` includes tests. Node 24 >=24.12.0, `npm ci`, unit tests and full production build run in the image build.
- ASP.NET Core net10.0, SDK 10.0.401, Identity / EF Core 10.0.12 and Npgsql EF provider 10.0.3. xUnit v3 + WebApplicationFactory / SQLite / controlled TimeProvider test storage and HTTP; SQLite is not a production database. A pinned dotnet-ef tool generates idempotent PostgreSQL SQL offline.
- Identity implements self-registration and separate login without email confirmation. Secure/HttpOnly cookies last at most 7 days without sliding renewal, and Antiforgery protects all auth POSTs. Recovery remains S-08. Stable `IdentityUser.Id` / `ClaimTypes.NameIdentifier` is the owner contract for S-02.
- The single non-root `linux/arm64` app image serves Vue and API on internal HTTP 8080. The host HTTP port is loopback-only. Caddy 2.11.9-alpine terminates LAN HTTPS at `https://<LAN_HOST>:8443` using a local CA; operators install trust explicitly on devices. Public Tunnel remains outside this change.
- PostgreSQL 18 persists under `/var/lib/postgresql`, with a healthcheck and no published port. App/migrator read the existing password file (0444 under private host directory 0700). Runtime database settings are `Database:Host/Port/Name/Username/PasswordFile`.
- Compose project `pennypath` preserves `postgres_data`, `app_keys`, `proxy_data` and `proxy_config`. Production Data Protection uses application name `PennyPath` and `/var/lib/pennypath/keys` owned by APP_UID (0700); dev/test keys are separate. Proxy trust is restricted to the exact `PROXY_IP` on `edge`. Dynamic app/migrator addresses come from `EDGE_IP_RANGE` (default `172.30.90.128/25`), inside `EDGE_SUBNET` and excluding `PROXY_IP`. Adjust all three settings together when required.
- Deployment state lives in `$HOME/Serwer/penny-path/deploy` (override `DEPLOY_DIR`). `deploy.sh` pulls, migrates once, then replaces only app; migration failure retains the old container/release record. Rollback skips the older migrator, preserves schema/keys/CA and never deletes data. Normal server start does not migrate.
- `verify` executes image tests and isolated trusted HTTPS/PostgreSQL/session/rollback checks before `publish`. Container tests use their own project, synthetic accounts, ports and IPAM subnet, checking network overlap before creating resources. Local build success does not replace unavailable Docker checks.
- OrbStack and the runner must be running after user login. Actual LAN trust and two-account acceptance remain operator checks; the instructions do not install or deploy to real devices automatically.

See [deploy-plan.md](../deployment/deploy-plan.md) and [mac-m1-setup.md](../deployment/mac-m1-setup.md) for the current operational contract. [infrastructure.md](infrastructure.md) records the earlier hosting research; its original HTTP starter baseline predates S-01.
