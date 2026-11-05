---
bootstrapped_at: 2026-10-10T13:03:13.448259+00:00
starter_id: custom
starter_name: "Vue 3 + TypeScript (Vite) frontend with ASP.NET Core Web API backend and PostgreSQL"
project_name: penny-path
language_family: multi
package_manager: npm
cwd_strategy: subdir-then-move
bootstrapper_confidence: best-effort
phase_3_status: ok
audit_command: null
---

## Hand-off

```yaml
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
```

## Why this stack

PennyPath uses Vue 3 with TypeScript and Vite for its browser frontend, ASP.NET Core Web API for its backend, and PostgreSQL for expense data. The solo developer wants to learn Vue while retaining a familiar .NET backend within a five-week, after-hours MVP. Both component starters pass the registry's four agent-friendly criteria; their combined setup is custom and has best-effort scaffolding support. Use the official Vue and ASP.NET Core project generators, npm for the frontend, and dotnet/NuGet for the backend. The backend documentation is https://learn.microsoft.com/aspnet/core/. Authentication and CSV import are in scope; payments, real-time synchronization, AI processing, and background jobs are not. Railway is a provisional hosting target whose setup and costs will be revisited at deployment time. GitHub Actions should run checks and deploy after merging to main. PostgreSQL software is free; hosting may incur costs. All five readiness statements were confirmed. Flutter is reserved for future mobile work outside the MVP.

## Pre-scaffold verification

| Signal | Value | Severity | Notes |
| --- | --- | --- | --- |
| npm package | not run | n/a | Custom starter: npm recency check skipped by skill contract. |
| GitHub repo | not run | n/a | docs_url is not a GitHub URL; no recency signal available. |

Official generator sources checked:
- https://vuejs.org/guide/quick-start.html
- https://github.com/vuejs/create-vue#usage
- https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api?view=aspnetcore-10.0
- Local `dotnet new webapi --help` and `dotnet new sln --help`.

Local toolchain: Node v26.10.0; .NET SDK 10.0.401, runtime 10.0.12.

## Scaffold log

**Resolved invocations**, in execution order:

```bash
npm create vue@latest -- .bootstrap-scaffold/frontend --typescript
dotnet new webapi --name PennyPath.Api --output .bootstrap-scaffold/backend/PennyPath.Api --use-controllers --framework net10.0 --no-restore
dotnet new sln --name PennyPath --output .bootstrap-scaffold/backend
dotnet sln .bootstrap-scaffold/backend/PennyPath.slnx add .bootstrap-scaffold/backend/PennyPath.Api/PennyPath.Api.csproj
dotnet restore .bootstrap-scaffold/backend/PennyPath.slnx
dotnet new gitignore --output .bootstrap-scaffold/backend
```

**Session adjustment**: user requested a .NET project together with a solution. Solution generated in the default .NET 10 `.slnx` format and API project added to it. Backend output directory refined to `backend/PennyPath.Api` to keep solution and project separate.
**Strategy**: subdir-then-move
**Exit codes**: 0 for successful generator, solution-add, restore, and gitignore invocations.
**Files moved**: 40 (includes NuGet-generated restore metadata).
**Conflicts (.scaffold siblings)**: none
**.gitignore handling**: frontend/.gitignore and backend/.gitignore moved silently; no root .gitignore supplied by generators.
**.bootstrap-scaffold cleanup**: deleted after merge.
**Existing context and AGENTS.md**: preserved.

### CLI output and retries

The initial npm attempt failed with exit 1: `ENOTFOUND registry.npmjs.org`. Retried outside the sandbox. The non-interactive run returned 0 at the package-name prompt without writing frontend files, so it was rerun in an interactive terminal. A sandboxed retry was canceled before execution; the approved network retry completed successfully.

Vue output:
```text
Package name: penny-path
Scaffolding project in /Users/pawelkaminski/Projects/PennyPath/.bootstrap-scaffold/frontend...
Done. Now run:
  cd .bootstrap-scaffold/frontend
  npm install
  npm run dev
```

.NET output:
```text
Pomyślnie utworzono szablon "Internetowy interfejs API platformy ASP.NET Core".
Pomyślnie utworzono szablon "Plik rozwiązania".
Dodano projekt „PennyPath.Api/PennyPath.Api.csproj” do rozwiązania.
Przywrócono element PennyPath.Api.csproj.
Pomyślnie utworzono szablon "plik dotnet gitignore".
```

Initial sandboxed restore returned exit 1 because access to `/Users/pawelkaminski/.nuget/NuGet/NuGet.Config` was denied. Approved retry outside the sandbox returned 0.

Frontend dependencies were not installed: create-vue only generates files, and the skill does not add installation beyond generator commands. PostgreSQL and authentication are recorded requirements; the official generators do not configure them.

### File-by-file move log

| Path | Action |
| --- | --- |
| `backend/.gitignore` | moved |
| `backend/PennyPath.Api/Controllers/WeatherForecastController.cs` | moved |
| `backend/PennyPath.Api/PennyPath.Api.csproj` | moved |
| `backend/PennyPath.Api/PennyPath.Api.http` | moved |
| `backend/PennyPath.Api/Program.cs` | moved |
| `backend/PennyPath.Api/Properties/launchSettings.json` | moved |
| `backend/PennyPath.Api/WeatherForecast.cs` | moved |
| `backend/PennyPath.Api/appsettings.Development.json` | moved |
| `backend/PennyPath.Api/appsettings.json` | moved |
| `backend/PennyPath.Api/obj/PennyPath.Api.csproj.nuget.dgspec.json` | moved |
| `backend/PennyPath.Api/obj/PennyPath.Api.csproj.nuget.g.props` | moved |
| `backend/PennyPath.Api/obj/PennyPath.Api.csproj.nuget.g.targets` | moved |
| `backend/PennyPath.Api/obj/project.assets.json` | moved |
| `backend/PennyPath.Api/obj/project.nuget.cache` | moved |
| `backend/PennyPath.slnx` | moved |
| `frontend/.gitignore` | moved |
| `frontend/.vscode/extensions.json` | moved |
| `frontend/.vscode/settings.json` | moved |
| `frontend/README.md` | moved |
| `frontend/env.d.ts` | moved |
| `frontend/index.html` | moved |
| `frontend/package.json` | moved |
| `frontend/public/favicon.ico` | moved |
| `frontend/src/App.vue` | moved |
| `frontend/src/assets/base.css` | moved |
| `frontend/src/assets/logo.svg` | moved |
| `frontend/src/assets/main.css` | moved |
| `frontend/src/components/HelloWorld.vue` | moved |
| `frontend/src/components/TheWelcome.vue` | moved |
| `frontend/src/components/WelcomeItem.vue` | moved |
| `frontend/src/components/icons/IconCommunity.vue` | moved |
| `frontend/src/components/icons/IconDocumentation.vue` | moved |
| `frontend/src/components/icons/IconEcosystem.vue` | moved |
| `frontend/src/components/icons/IconSupport.vue` | moved |
| `frontend/src/components/icons/IconTooling.vue` | moved |
| `frontend/src/main.ts` | moved |
| `frontend/tsconfig.app.json` | moved |
| `frontend/tsconfig.json` | moved |
| `frontend/tsconfig.node.json` | moved |
| `frontend/vite.config.ts` | moved |

### Build validation

`dotnet sln backend/PennyPath.slnx list` returned 0 and listed `PennyPath.Api/PennyPath.Api.csproj`.

`dotnet build backend/PennyPath.slnx --no-restore` returned 0:
```text
PennyPath.Api -> backend/PennyPath.Api/bin/Debug/net10.0/PennyPath.Api.dll
Kompilacja powiodła się.
Ostrzeżenia: 0
Liczba błędów: 0
```

Frontend build was not run because its dependencies are not installed.

## Post-scaffold audit

**Tool**: skipped — no built-in audit tool for multi.
**Reason**: no single audit tool covers this multi-language stack; skipped according to bootstrapper-config.yaml. Findings are unavailable, not zero.
**Recommended next step**: audit each component separately with `npm audit --json` and `dotnet list package --vulnerable --include-transitive` after installing dependencies.

## Hints recorded but not acted on

| Hint | Value |
| --- | --- |
| bootstrapper_confidence | best-effort |
| quality_override | false |
| path_taken | custom |
| self_check_answers | typed: true; from_official_starter: true; conventions: true; docs_current: true; can_judge_agent: true |
| team_size | solo |
| deployment_target | railway |
| ci_provider | github-actions |
| ci_default_flow | auto-deploy-on-merge |
| has_auth | true |
| has_payments | false |
| has_realtime | false |
| has_ai | false |
| has_background_jobs | false |

## Next steps

A future skill will set up agent context (AGENTS.md). The scaffold is generated and the backend build passes; security audit was skipped for this multi-language stack.

- Frontend: run `npm install` in `frontend/`, then `npm run dev` or `npm run build`.
- Backend: run `dotnet run --project backend/PennyPath.Api`.
- Configure PostgreSQL, authentication, and application features in a separate implementation change.
