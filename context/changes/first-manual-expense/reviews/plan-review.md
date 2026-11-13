<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Pierwszy ręczny wydatek i aktualne podsumowanie

- **Plan**: [plan.md](../plan.md)
- **Brief**: [plan-brief.md](../plan-brief.md)
- **Mode**: Deep
- **Date**: 2026-11-12
- **Verdict**: SOUND
- **Findings**: 0 critical, 1 warning, 0 observations; warning FIXED podczas triage
- **Source revision**: c4d19d8f36b746204d7bd9d49dd54bf8d053ae55; plan i brief z bieżącego drzewa roboczego

## Verdicts

Werdykty po zatwierdzonej poprawce F1. Przed poprawką Blind Spots miał WARNING; ogólny werdykt pozostaje SOUND.

| Dimension | Verdict |
| --- | --- |
| End-State Alignment | PASS |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | PASS |
| Plan Completeness | PASS |

## Grounding

Grounding: 10/10 paths ✓, 3/3 symbols ✓, brief↔plan ✓.

- Sprawdzone istniejące cele edycji: `backend/PennyPath.Api/Data/PennyPathIdentityDbContext.cs`, `Data/IdentityMigrationRunner.cs`, `Auth/AuthConfiguration.cs`, `Program.cs`, `frontend/src/api/auth.ts`, `auth/session.ts`, `views/AccountHomeView.vue`, `Dockerfile`, `.dockerignore`, `scripts/test-compose.sh`. Skrócone ścieżki backendu i frontendu odnoszą się do odpowiednich poprzedzających katalogów pakietów. Nowe moduły i testy są celowo nowymi plikami.
- Symbole: `AddPrivateAccountAccess`, `CreateBrowser`, `createAuthApi` istnieją i odpowiadają opisowi planu.
- Progress: dokładnie jedna sekcja na końcu; 4/4 fazy i 24/24 kryteria mają odpowiadające kroki w prawidłowych podsekcjach Automated/Manual. Brak checkboxów poza Progress; wszystkie kroki są niezaznaczone.
- Plan i brief zgodnie opisują strefę konta, opcjonalną podkategorię, dodatnie kwoty/obcięcie, przyszłe daty, miesięczny rejestr z historią oraz cztery fazy. Poprawka kolejności testów nie zmienia tych decyzji ani briefu.

## Findings

### F1 — Testy mogą przekroczyć limit prób logowania

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 4 — Testy rzeczywistych wydatków; `plan.md:320`
- **Detail**: Pierwotny plan zachowywał test auth i dodawał konfigurację dwóch kont wydatków po uruchomieniu HTTPS, bez rozdzielenia ich limitowanych żądań. `scripts/test-auth.py:91–101` wykonuje osiem POST register/login: dwa odrzucone przez CSRF, dwie rejestracje, duplikat, błędny login i dwa poprawne logowania. Dwa kolejne konta wymagają normalnie czterech rejestracji/logowań, co daje 12 przy limicie 10/IP/min. Oddzielne cookie jars nie rozdzielają partycji IP. Przy wykonaniu w tym samym oknie test może dostać 429 zamiast oczekiwanej odpowiedzi.
- **Evidence**: `backend/PennyPath.Api/Auth/AuthConfiguration.cs:137–148` definiuje partycję IP, limit 10, minutowe okno i brak kolejki. `Program.cs:54` uruchamia limiter przed filtrem MVC, więc niepoprawny CSRF też zużywa próbę. `scripts/test-compose.sh:168–169` już odtwarza app i sprawdza zachowaną sesję; nowy proces ma świeży limiter w pamięci.
- **Fix**: Uruchomić konfigurację kont i test wydatków dopiero po istniejącym pierwszym odtworzeniu app i `resume_session`; zapisać state i sprawdzać go przy późniejszych odtworzeniach/aktualizacji/rollbacku. Zachować limit produkcyjny oraz brak automatycznych powtórzeń POST.
- **Decision**: FIXED — użytkownik wybrał „Popraw w planie”; zastosowano pokazaną edycję w akapicie fazy 4. Pozostały akapit i wszystkie kroki Progress zachowano.

## Applied Change

Przed:

> `test-compose.sh` uruchamia test wydatków po zaufanym HTTPS, zapisuje state i wywołuje resume po odtworzeniu app i PostgreSQL, aktualizacji, błędzie migratora i rollbacku.

Po:

> `test-compose.sh` uruchamia konfigurację kont i test wydatków dopiero po istniejącym pierwszym odtworzeniu app oraz `resume_session` (obecnie linie 168–169). Nowy proces aplikacji ma świeży limit prób logowania w pamięci. Test zapisuje state i wywołuje resume po późniejszych odtworzeniach app i PostgreSQL, aktualizacji, błędzie migratora i rollbacku. Nie zmieniać limitu produkcyjnego ani automatycznie ponawiać POST.

## Verification Scope

- Przeczytano plan i brief, sprawdzono ich kontrakty, zakres, fazy i mechaniczny Progress.
- Weryfikacja kodu potwierdziła możliwość ponownego użycia kontekstu/migratora, jawnego HTTPS/CSRF i ProblemDetails. Plan obejmuje rozszerzenie authowego mapowania pól oraz osobne potwierdzenie providera PostgreSQL.
- Sprawdzono promień zmian klienta HTTP: `LoginView.vue`, `RegisterView.vue` i router korzystają z obecnego adaptera auth. Zachowanie jego eksportów i interfejsu sesji pozwala je pozostawić bez edycji. Plan obejmuje zmiany sesji, odpowiednie testy i czyszczenie stanu także po zwykłym logout.
- Sprawdzono allowlist i etapy Dockerfile; plan przewiduje dodanie źródeł i JSON F-01 do konsumowanego etapu testowego, z zachowaniem oddzielnego runtime API.
- Przegląd nie uruchamiał testów produktu ani Dockera. Wynik SOUND dotyczy wykonalności planu; właściwe kontrole runtime pozostają kryteriami jego implementacji.

## Triage Summary

- **Fixed**: F1
- **Skipped**: none
- **Accepted**: none
- **Dismissed**: none
- **Pending**: none
- **Verdict after fixes**: SOUND
