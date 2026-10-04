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
  deployment_target: railway
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

PennyPath uses Vue 3 with TypeScript and Vite for its browser frontend, ASP.NET Core Web API for its backend, and PostgreSQL for expense data. The solo developer wants to learn Vue while retaining a familiar .NET backend within a five-week, after-hours MVP. Both component starters pass the registry's four agent-friendly criteria; their combined setup is custom and has best-effort scaffolding support. Use the official Vue and ASP.NET Core project generators, npm for the frontend, and dotnet/NuGet for the backend. The backend documentation is https://learn.microsoft.com/aspnet/core/. Authentication and CSV import are in scope; payments, real-time synchronization, AI processing, and background jobs are not. Railway is a provisional hosting target whose setup and costs will be revisited at deployment time. GitHub Actions should run checks and deploy after merging to main. PostgreSQL software is free; hosting may incur costs. All five readiness statements were confirmed. Flutter is reserved for future mobile work outside the MVP.
