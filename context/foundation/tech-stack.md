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

## Deployment baseline (2026-10-13)

- Node 24 (at least 24.12.0), `npm ci` with the committed lockfile, and `npm run build` compile and type-check Vue.
- SDK .NET 10.0.401 publishes the backend and frontend into one `linux/arm64` image. The runtime uses the non-root .NET app user and serves HTTP on port 8080.
- PostgreSQL 18 has a persistent Compose volume under `/var/lib/postgresql`, a healthcheck, and no published port. Its password lives in a server-side file. API integration and migrations remain future work.
- The Compose project is `pennypath`; app updates retain the database container and volume. Deployment state and the previous image digest live in `~/pennypath/deploy`.
- LAN access is the initial release scope. Cloudflare Tunnel, authentication and expense features are later stages. OrbStack must be running; operation before user login is not assumed.

See [infrastructure.md](infrastructure.md) and [deploy-plan.md](../deployment/deploy-plan.md) for the operational contract.
