---
bootstrapped_at: 2026-10-04T18:00:00.547353+00:00
starter_id: custom
starter_name: "Vue 3 + TypeScript (Vite) frontend with ASP.NET Core Web API backend and PostgreSQL"
project_name: penny-path
language_family: multi
package_manager: npm
cwd_strategy: subdir-then-move
bootstrapper_confidence: best-effort
phase_3_status: failed
audit_command: null
---

## Hand-off

```markdown
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
```

## Pre-scaffold verification

| Signal | Value | Severity | Notes |
| --- | --- | --- | --- |
| npm recency | not run | unavailable | Custom starter: recency check skipped by protocol. Generator help identified create-vue 3.24.0. |
| GitHub recency | not run | unavailable | Handoff docs URL is not a GitHub repository; no recency signal available. |
| SDK preparation | .NET SDK 10.0.401 | informational | Installed successfully with Homebrew; dotnet --version passed. |

## Scaffold log

**Resolved invocations**:
```sh
npm create vue@latest .bootstrap-scaffold/frontend -- --typescript --router
dotnet new webapi -n PennyPath.Api -o .bootstrap-scaffold/backend --no-restore
```
**Strategy**: subdir-then-move
**Exit codes**: Vue 1; .NET 1
**Files moved**: 0
**Conflicts**: none
**Temporary scaffold**: never created; generators failed before creating project files. Nothing removed.

The initial non-interactive Vue invocation returned 0 at a package-name prompt without creating files. The same command was retried interactively with package name penny-path-frontend.

**Vue stderr**:
```text
Error: ENOENT: no such file or directory, mkdir '/Users/pawelkaminski/Projects/PennyPath/.bootstrap-scaffold/frontend'
```
The temporary parent directory was missing; the generator does not recursively create it.

**.NET stderr**:
```text
System.UnauthorizedAccessException: Access to the path '/Users/pawelkaminski/.dotnet' is denied.
System.IO.IOException: Operation not permitted
```
The generator could not create its first-run settings directory under sandbox permissions.

## Post-scaffold audit

**Audit not run**: scaffolding halted; no generated project to audit.

## Hints recorded but not acted on

| Hint | Value |
| --- | --- |
| bootstrapper_confidence | best-effort |
| quality_override | false |
| path_taken | custom |
| self_check_answers | typed=true; from_official_starter=true; conventions=true; docs_current=true; can_judge_agent=true |
| team_size | solo |
| deployment_target | railway; provisional, revisit at deployment time |
| ci_provider | github-actions |
| ci_default_flow | auto-deploy-on-merge |
| has_auth | true |
| has_payments | false |
| has_realtime | false |
| has_ai | false |
| has_background_jobs | false |

## Next steps

Create .bootstrap-scaffold/ before running the approved generators. Run the .NET generator with permission to initialize its user settings. Retain this failed log and save a retry log as verification-v2.md. Re-invoke /10x-bootstrapper to retry. Existing project context, skills, and repository history were preserved.
