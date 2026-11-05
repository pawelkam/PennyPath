# Repository Guidelines

This guide covers PennyPath's `frontend/` package, currently a Vue 3, TypeScript, and Vite scaffold. Paths below are relative to this directory.

## Build, Test, and Development Commands

Run commands from `frontend/`. Use a Node.js version satisfying `engines.node` in @package.json: `^22.18.0 || >=24.12.0`.

- `npm ci`: install the versions recorded in `package-lock.json`; use this for verification and CI. Use `npm install` when intentionally updating dependencies and commit the updated lockfile.
- `npm run dev`: start the Vite development server with Vue DevTools.
- `npm run type-check`: check TypeScript and Vue components using `vue-tsc --build`.
- `npm run build`: run type checking and the production build in parallel, producing `dist/`.
- `npm run build-only`: build without type checking; use the full build for validation.
- `npm run preview`: serve an existing production build locally.

## Project Structure & Module Organization

- `src/main.ts` imports global CSS and mounts `src/App.vue`, the root component.
- `src/components/` contains Vue components; `src/components/icons/` contains SVG icon components.
- `src/assets/` holds imported assets and global styles. `base.css` defines theme variables; `main.css` imports it and styles the application layout.
- `public/` holds static assets such as `favicon.ico`.
- @vite.config.ts and @tsconfig.app.json both map `@/` to `src/`.

## Coding Style & Naming Conventions

Follow @src/components/HelloWorld.vue for typed props, `<script setup lang="ts">`, and component-scoped styles. Name component files in PascalCase, such as `WelcomeItem.vue`; icon components use the `Icon` prefix.

Match existing two-space indentation, single-quoted TypeScript strings, and omitted TypeScript semicolons. Keep shared theme values in @src/assets/base.css. No ESLint, Prettier, or formatting scripts are configured.

## Testing Guidelines

No test framework, test files, test script, or coverage threshold is configured. Validate changes with `npm run build` and browser checks through `npm run dev`; check changed layouts at mobile and desktop widths.

@tsconfig.app.json excludes `src/**/__tests__/*`, so future tests there require separate test/type-check configuration.

## Commit & Pull Request Guidelines

History contains only `Init first lesson` and `Second lesson`; no established prefix convention is evident. Use short, descriptive commit subjects.

The root `../.github/workflows/containers.yml` builds the frontend inside the ARM64 image and checks HTTP behavior. For frontend PRs, describe the changed behavior, report build and browser validation, link related issues when applicable, and include screenshots for visible changes.
