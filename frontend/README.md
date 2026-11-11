# PennyPath frontend

Vue 3, TypeScript, Vue Router and Vite provide account registration, login and a private account page.

## Recommended IDE Setup

[VS Code](https://code.visualstudio.com/) + [Vue (Official)](https://marketplace.visualstudio.com/items?itemName=Vue.volar) (and disable Vetur).

## Recommended Browser Setup

- Chromium-based browsers (Chrome, Edge, Brave, etc.):
  - [Vue.js devtools](https://chromewebstore.google.com/detail/vuejs-devtools/nhdogjmejiglipccpnnnanhbledajbpd)
  - [Turn on Custom Object Formatter in Chrome DevTools](http://bit.ly/object-formatters)
- Firefox:
  - [Vue.js devtools](https://addons.mozilla.org/en-US/firefox/addon/vue-js-devtools/)
  - [Turn on Custom Object Formatter in Firefox DevTools](https://fxdx.dev/firefox-devtools-custom-object-formatters/)

## Type Support for `.vue` Imports in TS

TypeScript cannot handle type information for `.vue` imports by default, so we replace the `tsc` CLI with `vue-tsc` for type checking. In editors, we need [Volar](https://marketplace.visualstudio.com/items?itemName=Vue.volar) to make the TypeScript language service aware of `.vue` types.

## Customize configuration

See [Vite Configuration Reference](https://vite.dev/config/).

## Project Setup

```sh
npm ci
```

### Compile and Hot-Reload for Development

```sh
npm run dev
```

### Type-Check, Compile and Minify for Production

```sh
npm run build
```

## Konto i lokalny HTTPS

`npm run test:unit -- --run` uruchamia testy formularzy, sesji i routingu.
`npm run type-check` sprawdza także testy. API używa względnego `/api/auth/*`
i cookie tego samego originu. Stan konta i token CSRF pozostają w pamięci.

Formularze wysyłają hasła wyłącznie z originu HTTPS. Zwykłe `npm run dev`
pozwala obejrzeć interfejs i komunikat wymagania HTTPS, ale nie wysyła haseł.
Do pracy z API skopiuj `.env.example` do `.env.local` i ustaw jawny
`DEV_API_TARGET=https://localhost:<port-z-logu-API>` oraz parę plików
`DEV_HTTPS_CERT`/`DEV_HTTPS_KEY` z lokalnie zaufanym certyfikatem dla hosta Vite.
Uruchom API jego profilem `https`, a Vite przez `npm run dev`.
Zaufaj CA w przeglądarce i w Node (`NODE_EXTRA_CA_CERTS`, gdy potrzebne).
Proxy zachowuje sprawdzanie certyfikatu TLS; prywatne klucze nie trafiają do repo.
Opcje `DEV_*` są czytane przez konfigurację serwera, nie przez kod klienta.

Alternatywnie zbuduj Vue przez `npm run build`, skopiuj zawartość `dist/`
do `backend/PennyPath.Api/wwwroot/` i uruchom z katalogu głównego
`dotnet run --project backend/PennyPath.Api --launch-profile https`.
Otwórz adres HTTPS wypisany przez API z zaufanym certyfikatem deweloperskim.
Frontend i API mają wtedy wspólny origin bez proxy. Konto wymaga skonfigurowanego PostgreSQL i migracji `--migrate`; nazwy zmiennych i komendy są w [głównym README](../README.md). Wariant Docker/Caddy udostępnia ten sam frontend i API pod `https://<LAN_HOST>:8443` z root CA zaufanym na każdym urządzeniu.

Rejestracja nie wymaga potwierdzenia emaila i prowadzi do osobnego logowania.
Sesja jest trwała przez maksymalnie 7 dni od logowania, bez przedłużania aktywnością.
Odzyskiwanie hasła należy do S-08. `userId` z API jest stabilnym ID użytkownika
(przyszły właściciel wydatków w S-02), niezależnym od emaila. Odbiór obejmuje dwa
konta w osobnych kontekstach przeglądarki, klawiaturę, mobile/desktop, bezpieczny
powrót oraz ponowne otwarcie przeglądarki i wylogowanie.
