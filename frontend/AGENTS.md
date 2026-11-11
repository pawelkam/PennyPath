# Repository Guidelines

This guide covers PennyPath's `frontend/` package, a Vue 3, TypeScript, Vite and Vue Router application. Paths below are relative to this directory.

## Build, Test, and Development Commands

Run commands from `frontend/`. Use a Node.js version satisfying `engines.node` in @package.json: `^22.18.0 || >=24.12.0`.

- `npm ci`: install the versions recorded in `package-lock.json`; use this for verification and CI. Use `npm install` when intentionally updating dependencies and commit the updated lockfile.
- `npm run dev`: start the Vite development server with Vue DevTools.
- `npm run type-check`: check TypeScript and Vue components using `vue-tsc --build`.
- `npm run build`: run type checking and the production build in parallel, producing `dist/`.
- `npm run build-only`: build without type checking; use the full build for validation.
- `npm run preview`: serve an existing production build locally.
- `npm run test:unit -- --run`: run Vitest / Vue Test Utils in jsdom.

## Project Structure & Module Organization

- `src/main.ts` imports global CSS, installs the router and mounts `src/App.vue`.
- `src/views/` contains login, registration and the private account page; `src/router/` validates private return URLs.
- `src/api/auth.ts` manages relative same-origin requests/CSRF; `src/auth/session.ts` keeps session state in memory.
- `src/components/` contains Vue components; `src/components/icons/` contains SVG icon components.
- `src/assets/` holds imported assets and global styles. `base.css` defines theme variables; `main.css` imports it and styles the application layout.
- `public/` holds static assets such as `favicon.ico`.
- @vite.config.ts and @tsconfig.app.json both map `@/` to `src/`.

## Coding Style & Naming Conventions

Follow @src/components/HelloWorld.vue for typed props, `<script setup lang="ts">`, and component-scoped styles. Name component files in PascalCase, such as `WelcomeItem.vue`; icon components use the `Icon` prefix.

Match existing two-space indentation, single-quoted TypeScript strings, and omitted TypeScript semicolons. Keep shared theme values in @src/assets/base.css. No ESLint, Prettier, or formatting scripts are configured.

## Testing Guidelines

Vitest + Vue Test Utils with jsdom test forms, sessions, failures and routing in `src/__tests__/`. `tsconfig.vitest.json` is a root project reference with its own tsbuildinfo; `npm run type-check` must check tests too. Run tests and the full production build. Check 320px/mobile and desktop layouts, labels, focus and keyboard handling manually.

Account forms require HTTPS and must never send passwords from an HTTP origin. Development HTTPS/proxy options are in `.env.example` / `vite.config.ts`; preserve certificate verification. The root README also describes a built frontend served directly by the API's trusted development HTTPS certificate. Never commit certificate private keys or set `secure: false` on the proxy.

Self-registration leads to separate login with a validated local return URL. Protected views wait for `/api/auth/me`; 401 clears private state, while network/5xx errors allow retry. Never automatically retry a POST. Keep the user and CSRF token in memory, never local/sessionStorage. Invalidate CSRF after login/logout. Sessions last at most 7 days; password recovery is S-08 and expense data is S-02. Do not add fabricated expenses to the account page.

## Commit & Pull Request Guidelines

Follow the active change plan and repository skill gates; phase commits use Conventional Commits and require the prescribed approval.

The root `../.github/workflows/containers.yml` runs Vue tests and type checking inside the ARM64 image and checks HTTPS sessions with PostgreSQL. For frontend PRs, describe the changed behavior, report build and browser validation, link related issues when applicable, and include screenshots for visible changes.
