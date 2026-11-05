# Weryfikacja przygotowania Docker Compose

Data: 2026-10-13. Workspace deweloperski: macOS ARM64, SDK .NET 10.0.401, Node 26.10.0, npm 11.19.1, Python 3.9. Docker i OrbStack nie są tutaj zainstalowane.

## Sprawdzenia wykonane

| Sprawdzenie | Wynik |
| --- | --- |
| Utworzenie `frontend/package-lock.json` i instalacja przez `npm ci` | OK |
| `npm run build` (sprawdzenie typów i produkcyjny build Vue) | OK |
| `dotnet build backend/PennyPath.slnx --no-restore` | OK, bez ostrzeżeń |
| Publikacja Release .NET z buildem Vue w `wwwroot/` | OK |
| `python3 scripts/smoke-test.py http://127.0.0.1:18081` przeciwko opublikowanemu backendowi w Production | OK |
| Strona Vue i zasoby JS/CSS | HTTP 200, poprawne typy treści |
| `/expenses/example` | Ta sama strona SPA |
| `/weatherforecast` | HTTP 200, pięć obiektów JSON |
| `/health` | HTTP 200, `status=healthy` |
| `/api`, `/api/unknown`, `/api/unknown.json`, `/API/unknown`, `/assets/missing.js` | HTTP 404, bez HTML SPA |
| `python3 scripts/test-deploy.py` | OK, 15 testów transakcji wdrożeniowej |
| Składnia Bash obu skryptów | OK |
| ShellCheck 0.11.0 | OK |
| actionlint 1.7.12 (z konfiguracją własnej etykiety runnera) | OK |
| Parsowanie YAML Compose i workflow | OK |

Testy skryptu używają kontrolowanych atrap Docker/curl. Sprawdzają zapis poprzedniego wydania, pobranie przed zmianą, przywrócenie po błędzie healthchecku lub Compose, brak zmian PostgreSQL, pierwsze wydanie, ręczny rollback, brak poprzedniego wydania, brak lokalnego obrazu do odtwarzania, odmowę przy rozbieżnym stanie oraz blokadę współbieżnych wdrożeń. Nie zastępują testu prawdziwych kontenerów.

Lockfile i lokalny build użyły Vite 8.3.4 zgodnego z zakresem deklarowanym w `package.json`. Build obrazu użyje Node 24, co musi jeszcze przejść test w CI.

## Sprawdzenia przygotowane do CI

Workflow `.github/workflows/containers.yml` uruchamia na `ubuntu-24.04-arm` pełny build Dockerfile i `scripts/test-compose.sh`. Test ma osobny projekt Compose, bazę, wolumen, katalog stanu i lokalny rejestr, a po zakończeniu usuwa swoje kontenery oraz wolumen. Weryfikuje:

- architekturę ARM64 i użytkownika bez roota;
- stronę, statyczne zasoby, SPA, API, `/health` i odpowiedzi 404;
- brak publikacji portu PostgreSQL oraz montowanie wolumenu w miejscu wymaganym przez PostgreSQL 18;
- przetrwanie danych po odtworzeniu kontenera PostgreSQL i aktualizacji aplikacji;
- brak odtworzenia kontenera bazy przez wdrożenie aplikacji;
- nieudane pobranie digestu bez wymiany działającej aplikacji;
- przywrócenie poprzedniego obrazu po nieudanym wdrożeniu oraz ręczny rollback.

Te testy nie zostały uruchomione w bieżącym środowisku ze względu na brak Docker/OrbStack. Przed pierwszą instalacją wymagany jest zielony wynik zadania `verify`.

## Odbiór wymagający GitHub i osobnego Maca

Nie wykonano push do repozytorium, publikacji obrazu, zmiany widoczności GHCR, rejestracji runnera ani instalacji na docelowym Macu. Dostęp LAN z drugiego urządzenia, merge → faktyczna aktualizacja kontenera, kopia/odtworzenie na serwerze i restart Maca wymagają odbioru według [deploy-plan.md](deploy-plan.md). `DEPLOY_ENABLED` ma pozostać wyłączone do przygotowania serwera.
