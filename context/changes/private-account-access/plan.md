# Dostęp do własnego konta — Implementation Plan

## Overview

S-01 daje użytkownikowi rejestrację i logowanie emailem/hasłem w przeglądarce, prywatny obszar oraz wylogowanie. Użytkownik zatwierdził złożoność HIGH, pięć decyzji opisanych niżej i cztery fazy. S-02 wykorzysta tę tożsamość do rzeczywistych wydatków; odzyskanie hasła pozostaje w S-08.

## Current State Analysis

- Backend to jeden projekt net10.0 z OpenAPI, kontrolerem weather i publicznym /health; nie ma Identity, EF, połączenia z bazą ani .NET testów (`backend/PennyPath.Api/Program.cs:5`, `backend/PennyPath.Api/PennyPath.Api.csproj:4`, `backend/PennyPath.slnx:2`).
- PostgreSQL 18 istnieje w Compose, ale sekret jest zamontowany tylko do bazy; app nie otrzymuje połączenia (`compose.yaml:4`, `:17`, `:24`).
- Vue ma starter bez routera, stanu sesji, klienta API i testów (`frontend/package.json:13`, `frontend/src/main.ts:6`, `frontend/src/App.vue:1`). Vite nie ma proxy/HTTPS (`frontend/vite.config.ts:8`).
- Jeden obraz serwuje frontend z API; .dockerignore domyślnie wyklucza nowe katalogi backendu (`Dockerfile:11`, `:14`, `.dockerignore:1`). Wyjątki obejmują obecnie głównie pliki root API i Controllers.
- LAN używa HTTP na 8080 i nie ma trwałego magazynu kluczy aplikacji ani TLS (`compose.yaml:8`, `context/foundation/infrastructure.md:15`). Aktualna decyzja użytkownika rozszerza ten etap o HTTPS w LAN; publiczny tunel nadal jest poza zakresem.
- Skrypt wydania zmienia tylko app i cofa obraz, nie schemat; jego aktualny domyślny katalog to `$HOME/Serwer/penny-path/deploy` (`scripts/deploy.sh:6`), co różni się od starszych instrukcji.
- CI już buduje ARM64 i wykonuje izolowane testy Compose (`.github/workflows/containers.yml:19`). Smoke bada starter, nie konta (`scripts/smoke-test.py:22`).
- Lokalnie dostępne są SDK .NET 10.0.401, Node/npm i Python 3.9.6; Docker, psql, dotnet-ef, actionlint i shellcheck nie są dostępne. dotnet-ef zostanie dodany jako narzędzie repozytorium. Kontrole rzeczywistego PostgreSQL/HTTPS/obrazu z fazy 4 wymagają CI albo osobnego hosta z Dockerem.
- Brak frame/research dla tej zmiany oraz lessons.md. Wymagania o email/haśle, jednej roli i własności danych są już zapisane w PRD. Pytanie 4 PRD/roadmapy o uzyskanie konta i anonimowy dostęp jest rozstrzygnięte decyzjami poniżej.

## Desired End State

Użytkownik w LAN otwiera zaufany adres HTTPS, zakłada konto, przechodzi do logowania i trafia do pierwotnie otwartej prywatnej strony. Konto działa bez potwierdzenia emaila. Prywatny obszar pokazuje jego własną tożsamość, a API odrzuca anonimowy dostęp. Sesja jest zapamiętana na urządzeniu przez maksymalnie 7 dni od logowania; działa po restarcie przeglądarki i wymianie zgodnego kontenera, a wylogowanie kończy bieżącą sesję.

Wykonanie potwierdzają testy modelu i HTTP, testy Vue, kontrola gotowego obrazu z rzeczywistym PostgreSQL i zaufanym TLS oraz ręczny odbiór w LAN. S-01 dowodzi rozdzielenia principal dwóch kont; kontrola własności rzeczywistych wydatków powstaje w S-02.

### Key Discoveries:

- Cookie jest odpowiednim mechanizmem dla przeglądarkowego SPA z API tego samego originu; nie potrzeba tokenów dostępnych dla JavaScript.
- API 401 i przekierowanie strony przez router są osobnymi kontraktami. Autoryzacja API nie może zależeć wyłącznie od guarda Vue.
- Antiforgery token jest związany z tożsamością; po logowaniu/wylogowaniu klient musi pobrać nowy token.
- Sesje wymagają trwałych kluczy Data Protection i stałej nazwy aplikacji, a certyfikaty LAN trwałego CA i zaufania urządzeń.
- Allowlist .dockerignore, uprawnienia wolumenu kluczy i rozszerzenie migratora wpływają na obraz oraz istniejące testy wydania.

## What We're NOT Doing

- Hasła odzyskiwane emailem, potwierdzanie emaila, zmiana emaila/hasła, 2FA, social login, role administratora, zaproszenia i panel zarządzania kontami.
- Publiczna domena, Cloudflare Tunnel, router/firewall, instalacja na rzeczywistym Macu lub automatyczne zaufanie CA na urządzeniach.
- Rejestr/CRUD wydatków, dashboard, model danych wydatków i dowodzenie ich izolacji; to kontrakt S-02 i kolejnych przekrojów.
- Globalne wylogowanie urządzeń, serwerowy rejestr każdej sesji, własny mechanizm kryptografii, JWT w localStorage.
- Automatyczne cofanie schematu PostgreSQL podczas rollbacku obrazu i usuwanie istniejącego wolumenu bazy.
- Wznawianie odłożonej poza MVP fazy automatyzacji kontroli wydatków F-01; nowe sprawdzenia dotyczą S-01.

## Implementation Approach

### Decyzje użytkownika

| Obszar | Uzgodniona decyzja |
| --- | --- |
| Zakładanie kont | Samodzielna rejestracja w aplikacji, dostępna osobom z dostępem do serwera |
| Anonimowe wejście | Logowanie i powrót do pierwotnej lokalnej strony |
| Transport | HTTPS na domowym serwerze w LAN, z zaufaniem lokalnego CA na urządzeniach |
| Sesja | Trwała, maksymalnie 7 dni od logowania, także po ponownym uruchomieniu przeglądarki |
| Email | Konto działa od razu; brak potwierdzenia emaila w MVP |

Technicznie: Identity/EF Core 10 + Npgsql wewnątrz istniejącego API, cookie sesji, ASP.NET Antiforgery, Vue Router i pamięciowy stan użytkownika. Caddy kończy TLS, API jest osiągalne dla użytkowników przez wspólny HTTPS origin; PostgreSQL i klucze pozostają po stronie serwera.

### Kontrakt HTTP

| Metoda i ścieżka | Wejście / uprawnienia | Wynik |
| --- | --- | --- |
| GET /api/auth/csrf | Anonimowy lub zalogowany, HTTPS | 200 `{requestToken}`, cookie Antiforgery; Cache-Control: no-store |
| POST /api/auth/register | HTTPS, CSRF, JSON `{email,password}` | 201 bez sesji; 400 walidacja, 409 niedostępny adres, 429 limit |
| POST /api/auth/login | HTTPS, CSRF, JSON `{email,password}` | 200 `{userId,email}` i trwałe cookie; 401 ogólny błąd, 429 limit |
| GET /api/auth/me | Zalogowany | 200 `{userId,email}`; 401 bez ważnej sesji |
| POST /api/auth/logout | Zalogowany, HTTPS, CSRF | 204 i usunięcie cookie; 401 przy wygasłej sesji |
| /health i nieznane /api/* | Jak dotychczas | Odpowiednio 200 i 404; brak HTML zamiast błędu API |

Błędy mają spójny ProblemDetails i stabilny `code`, walidacja dodatkowo mapę `errors`; szczegóły providerów i sekrety nie trafiają do odpowiedzi. Błąd CSRF daje 400 `csrf_invalid`, brak HTTPS 400 `https_required`, błąd poświadczeń 401 `invalid_credentials`. Me i pozostałe odpowiedzi zawierające stan konta mają no-store. Konto jest jedną rolą; userId w odpowiedzi i principal jest identyfikatorem przyszłego właściciela wydatków.

## Critical Implementation Details

Middleware zaufanego proxy działa przed auth i Antiforgery. Formularze są odbierane z rzeczywistymi hasłami dopiero po wdrożeniu HTTPS; pośrednie wydania zachowują odrzucenie operacji auth po HTTP. Zmiana tożsamości unieważnia zapamiętany request token CSRF.

Nowe katalogi muszą być dopuszczone w .dockerignore w fazie, w której powstają, aby każdy pośredni obraz kompilował się z tym samym kodem co lokalny checkout. Testy uruchamiane w nieużywanym etapie Dockerfile nie stanowią bramki finalnego obrazu.

Migracja poprzedza wymianę działającego app; jawny rollback pomija migrator starszego obrazu. Fixture niezdrowego wydania musi przechodzić tryb --migrate, aby nadal sprawdzać faktyczny rollback po błędzie healthchecku.

## Phase 1: Trwałe konta

### Overview

Dodać przechowywanie kont przez Identity i EF Core z PostgreSQL, migrację oraz testy modelu niezależne od lokalnego Dockera. Ustalić trwały identyfikator właściciela danych dla S-02.

### Changes Required:

#### 1. Magazyn i konfiguracja kont

**Files**: `backend/PennyPath.Api/PennyPath.Api.csproj`, `backend/PennyPath.Api/Data/PennyPathIdentityDbContext.cs`, `backend/PennyPath.Api/Data/DatabaseConfiguration.cs`, `backend/PennyPath.Api/Data/PennyPathIdentityDbContextFactory.cs`, `backend/PennyPath.Api/Program.cs`

**Intent**: Wykorzystać gotowe hashowanie haseł, normalizację emaila i magazyn użytkowników Identity. Zapewnić jawne połączenie aplikacji z wybranym PostgreSQL.

**Contract**: `PennyPathIdentityDbContext` dziedziczy po `IdentityDbContext<IdentityUser>`. `IdentityUser.Id` jest niepustym, niezmiennym identyfikatorem tekstowym nadawanym przez serwer; email nie jest kluczem właściciela. Normalizacja i sprawdzanie haseł korzystają z Identity. Wymagany unikalny indeks `NormalizedEmail`, także przy równoległej rejestracji. Pakiety `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design` i `Npgsql.EntityFrameworkCore.PostgreSQL` mają zgodną linię EF Core 10; przypiąć konkretne opublikowane wersje bez preview i wildcardów. Nie zmieniać docelowego `net10.0`.

Konfiguracja `Database:Host`, `Port`, `Name`, `Username`, `PasswordFile` jest składana przez `NpgsqlConnectionStringBuilder`; sekret jest odczytywany z pliku, nie logowany ani przekazywany w argumentach procesu. Compose używa hosta `postgres`, portu 5432 i istniejącej bazy/użytkownika `pennypath`. Fabryka design-time pozwala wygenerować migrację i SQL bez działającego PostgreSQL i bez odczytu produkcyjnego sekretu; niezmienniki modelu są wspólne z runtime.

#### 2. Migracja i tryb jednorazowy

**Files**: `backend/PennyPath.Api/Data/Migrations/` (migracja `InitialIdentity` i snapshot), `backend/PennyPath.Api/Data/IdentityMigrationRunner.cs`, `.config/dotnet-tools.json`, `backend/PennyPath.Api/Program.cs`

**Intent**: Oddzielić zmianę schematu od uruchomienia serwera HTTP i umożliwić zastosowanie migracji z gotowego obrazu.

**Contract**: Przypięte narzędzie `dotnet-ef` w linii EF Core 10. `dotnet PennyPath.Api.dll --migrate` uruchamia migracje i kończy proces: kod 0 po sukcesie, niezerowy po błędzie, bez nasłuchiwania HTTP i bez wypisywania poświadczeń. Ponowienie jest idempotentne. Migracja dodaje wyłącznie tabele/indeksy Identity i historię EF; nie usuwa istniejących danych ani tabel kontrolnych. Zwykły start API nie wykonuje migracji.

#### 3. Dostarczenie konfiguracji i pakowanie

**Files**: `compose.yaml`, `.env.example`, `.dockerignore`, `scripts/test-compose.sh`, `context/deployment/deploy-plan.md`, `context/deployment/mac-m1-setup.md`

**Intent**: Udostępnić aplikacji istniejący sekret PostgreSQL i zapewnić obecność nowych źródeł w obrazie od pierwszej fazy.

**Contract**: Serwis `app` otrzymuje konfigurację bazy i montowanie sekretu `postgres_password` z lokalizacją `/run/secrets/postgres_password`; nie zmieniać wolumenu ani hasła istniejącego PostgreSQL. Dodać wyjątki allowlist dla `Data/**` i plików migracji. Zachować wykluczenie `bin/`, `obj/`, sekretów i danych testowych z obrazu aplikacji.

Jeden plik hasła jest współdzielony przez PostgreSQL, app i migrator jako sekret tylko do odczytu. Na hoście znajduje się w prywatnym katalogu `secrets` o uprawnieniach `0700`, a sam plik ma `0444`; przykładowa konfiguracja to `POSTGRES_PASSWORD_FILE=./secrets/postgres-password`. Najpierw zabezpieczyć katalog, dopiero potem nadać plikowi `0444`. Zachować istniejącą wartość hasła; `.env` pozostaje `0600`. Nie polegać na remapowaniu `uid/gid/mode` sekretu Compose o źródle plikowym.

Już w fazie 1 dostosować `.env.example`, przygotowanie sekretu w `scripts/test-compose.sh` i odpowiadające mu fragmenty obu instrukcji wdrożeniowych. Istniejący plik przenieść do prywatnego katalogu i zaktualizować ścieżkę bez generowania nowego hasła dla istniejącej bazy. Fixture tworzy syntetyczny sekret we własnym prywatnym katalogu. Na hoście z Dockerem sprawdzić niezerowy UID procesu app/migratora i rzeczywisty odczyt `/run/secrets/postgres_password`, kierując zawartość do `/dev/null`; nie wypisywać sekretu ani umieszczać go w argumentach. Końcowa kontrola mountu wchodzi w kryterium 4.6; lokalna kompilacja/SQLite nie potwierdza praw mountu.

#### 4. Pierwszy projekt testowy .NET

**Files**: `backend/PennyPath.Api.Tests/PennyPath.Api.Tests.csproj`, `backend/PennyPath.Api.Tests/PersistenceTests.cs`, `backend/PennyPath.slnx`

**Intent**: Sprawdzać niezmienniki kont lokalnie bez wymogu PostgreSQL lub Dockera.

**Contract**: Projekt xUnit dla `net10.0`, dodany do rozwiązania i referujący API, z relacyjnym SQLite wyłącznie jako dostawcą testowym. Testy oznaczone `Category=Persistence` obejmują niejawne hashowanie (brak przechowania hasła jawnego), odrzucenie niepoprawnego hasła, unikalność znormalizowanego emaila, dwa różne ID i trwałość ID po ponownym otwarciu testowej bazy. Nie traktować SQLite jako dowodu poprawności migracji PostgreSQL; rzeczywisty provider jest sprawdzany w fazie 4.

### Success Criteria:

#### Automated Verification:

- Zależności rozwiązania i lokalne narzędzie EF są przywrócone: `dotnet restore backend/PennyPath.slnx` oraz `dotnet tool restore`.
- Rozwiązanie kompiluje się: `dotnet build backend/PennyPath.slnx --no-restore`.
- Testy trwałości i niezmienników kont przechodzą: `dotnet test backend/PennyPath.Api.Tests/PennyPath.Api.Tests.csproj --no-restore --filter Category=Persistence`.
- Powstaje idempotentny SQL bez połączenia z bazą: `dotnet tool run dotnet-ef migrations script --idempotent --project backend/PennyPath.Api --output artifacts/private-account-access.sql`.
- Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual Verification:

- Użytkownik potwierdził, że wygenerowana migracja dodaje tabele Identity i unikalny indeks emaila oraz nie zmienia istniejących danych.

**Implementation Note**: Po zielonej weryfikacji automatycznej zatrzymać się na potwierdzenie ręcznej kontroli tej fazy. Faza 4 wymaga rzeczywistego środowiska Docker/CI; niedostępne kontrole pozostają oczekujące, bez oznaczania sukcesu.

---

## Phase 2: API logowania i sesji

### Overview

Dostarczyć minimalny kontrakt konta i sesji, chroniony serwerowo. Rejestracja pozwala od razu się zalogować; maksymalny czas sesji wynosi 7 dni od logowania.

### Changes Required:

#### 1. Kontrakty i endpointy konta

**Files**: `backend/PennyPath.Api/Controllers/AuthController.cs`, `backend/PennyPath.Api/Auth/AuthContracts.cs`, `backend/PennyPath.Api/Auth/AuthConfiguration.cs`, `backend/PennyPath.Api/Program.cs`, `backend/PennyPath.Api/PennyPath.Api.http`

**Intent**: Udostępnić wyłącznie operacje wymagane przez S-01, z uwierzytelnieniem obsługiwanym przez Identity.

**Contract**: Endpointy i statusy zgodne z tabelą w Implementation Approach. Email jest przycinany i normalizowany przez Identity; hasło nigdy nie jest przycinane ani normalizowane. Hasło ma 12–128 znaków, bez obowiązku klas znaków; formularz rejestracji powtarza hasło po stronie klienta. Duplikat emaila, również wyścig zapisów na indeksie, daje kontrolowany błąd bez informacji o bazie. Rejestracja nie loguje automatycznie; sukces prowadzi do formularza logowania. Nie wymagać potwierdzenia emaila i nie mapować całego `MapIdentityApi`, które udostępniłoby także operacje spoza S-01.

#### 2. Sesja, autoryzacja i ochrona żądań

**Files**: `backend/PennyPath.Api/Auth/AuthConfiguration.cs`, `backend/PennyPath.Api/Auth/RequireHttpsForAuthFilter.cs`, `backend/PennyPath.Api/Controllers/AuthController.cs`, `backend/PennyPath.Api/Controllers/WeatherForecastController.cs`, `backend/PennyPath.Api/Program.cs`, `.dockerignore`

**Intent**: Zapewnić serwerową kontrolę tożsamości, skończony czas dostępu i ochronę operacji korzystających z cookie.

**Contract**: Cookie `__Host-PennyPath.Auth`: `HttpOnly`, `Secure=Always`, `SameSite=Lax`, `Path=/`, bez Domain. Trwałe cookie i ticket wygasają najpóźniej 7 dni po poprawnym logowaniu; `SlidingExpiration=false`, brak odświeżenia wydłużającego ten termin. Walidacja odrzuca także dokładną granicę `now >= ExpiresUtc`, zachowując standardową kontrolę security stamp Identity; oba mechanizmy korzystają z tego samego TimeProvider. Odnowienie principal lub cookie nie przesuwa pierwotnego terminu. Wylogowanie kończy bieżącą sesję przeglądarki przez usunięcie cookie; globalne wylogowanie urządzeń i odwołanie skopiowanego cookie są poza zakresem.

Domyślna polityka endpointów wymaga zalogowania. Jawnie publiczne pozostają rejestracja, logowanie, wydanie tokenu CSRF, `/health`, starterowy weather, statyczne pliki, fallback SPA i fallback nieznanych `/api/*` zwracający 404. Chroniony endpoint `me` pobiera ID wyłącznie z uwierzytelnionego principal; nie przyjmuje ID właściciela od klienta. API zwraca 401/403, bez przekierowania do HTML. Dodać allowlist dla `Auth/**` w obrazie.

Wszystkie POST auth wymagają tokenu ASP.NET Antiforgery w `X-CSRF-TOKEN`; cookie anty-CSRF też jest Secure/HttpOnly. Rejestracja, logowanie i wydanie tokenu są odrzucane przez HTTP z 400 ProblemDetails `code=https_required` przed obsługą danych. Żaden produkcyjny tryb nie wyłącza Secure lub CSRF. Domyślne limity: 10 żądań logowania/rejestracji na minutę na IP (429 i Retry-After), 5 błędnych prób hasła blokuje konto na 5 minut; niepoprawny login i hasło mają ten sam ogólny komunikat 401. Nie logować haseł, tokenów ani treści cookie.

#### 3. Testy protokołu i czasu

**Files**: `backend/PennyPath.Api.Tests/AuthApiTests.cs`, `backend/PennyPath.Api.Tests/TestApplicationFactory.cs`, `backend/PennyPath.Api.Tests/TestTimeProvider.cs`, `backend/PennyPath.Api/PennyPath.Api.csproj`, `backend/PennyPath.Api/Program.cs`

**Intent**: Sprawdzić rzeczywistą obsługę HTTP i middleware, w tym granicę 7 dni, bez czekania na upływ czasu.

**Contract**: `WebApplicationFactory` z bazą SQLite, bazowym adresem HTTPS, osobnymi cookie jars i kontrolowanym `TimeProvider`; testowy provider/clock nie są dostępne jako tryb produkcyjny. Testy `Category=Auth`: rejestracja i późniejsze logowanie bez potwierdzenia emaila, duplikat i walidacja, poprawne cookie, anonimowe me=401, odrzucenie brakującego/błędnego CSRF, zmiana tożsamości tokenu po logowaniu, logout, rozróżnienie dwóch kont, blokada prób/429, odrzucenie HTTP i brak wycieku błędów DB. Sesja jest ważna przed terminem i nieważna przy/powyżej 7 dni; wcześniejsze żądania nie przesuwają wygaśnięcia.

### Success Criteria:

#### Automated Verification:

- Testy HTTP, CSRF i maksymalnego czasu sesji przechodzą: `dotnet test backend/PennyPath.Api.Tests/PennyPath.Api.Tests.csproj --filter Category=Auth`.
- Cały zestaw testów backendu przechodzi: `dotnet test backend/PennyPath.slnx`.
- Przykłady HTTP i kod przechodzą kompilację: `dotnet build backend/PennyPath.slnx --no-restore`.
- Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual Verification:

- Użytkownik potwierdził kontrakt rejestracja → osobne logowanie, prywatny odczyt bieżącego użytkownika i maksymalnie 7-dniową sesję bez jej automatycznego przedłużania.

**Implementation Note**: Po zielonej weryfikacji automatycznej zatrzymać się na potwierdzenie ręcznej kontroli tej fazy. Faza 4 wymaga rzeczywistego środowiska Docker/CI; niedostępne kontrole pozostają oczekujące, bez oznaczania sukcesu.

---

## Phase 3: Przepływ w Vue

### Overview

Zastąpić starter formularzami i prywatnym obszarem z tożsamością bieżącego użytkownika. S-01 nie dodaje jeszcze rejestru wydatków ani dashboardu.

### Changes Required:

#### 1. Routing i klient sesji

**Files**: `frontend/package.json`, `frontend/package-lock.json`, `frontend/src/main.ts`, `frontend/src/router/index.ts`, `frontend/src/auth/session.ts`, `frontend/src/api/auth.ts`, `frontend/vite.config.ts`, `frontend/.env.example`

**Intent**: Zapewnić jeden mechanizm odczytu sesji i chronionego wejścia, gotowy do rozszerzenia o S-02.

**Contract**: Vue Router z publicznymi `/login`, `/register` i chronionym `/app`; `/` kieruje do `/app`. Guard przed renderowaniem prywatnego widoku rozstrzyga stan sesji przez me. Niezalogowany trafia do `/login?returnUrl=...`; powrót jest do istniejącej lokalnej trasy oznaczonej jako prywatna, domyślnie `/app`. Zewnętrzne URL, `//host`, backslash, kodowane obejścia i trasy logowania/rejestracji nie są akceptowane jako powrót.

Klient używa względnych `/api/auth/*`, cookie przeglądarki i tokenu CSRF pobieranego przed POST. Po zalogowaniu/wylogowaniu unieważnia pamięciowy token CSRF; kolejne operacje pobierają token dla aktualnej tożsamości. Stan użytkownika jest tylko w pamięci; bez tokenów w local/sessionStorage. HTTP 401 czyści prywatny stan i kieruje do logowania z adresem powrotu. Błąd sieci lub 5xx daje możliwość ponowienia, nie jest utożsamiany z wylogowaniem. Nie ponawiać automatycznie POST. Wylogowanie jest potwierdzone dopiero po 204 lub stwierdzeniu wygasłej sesji (401).

Vite może proxy'ować `/api` do jawnie skonfigurowanego lokalnego API; nie wyłączać weryfikacji TLS proxy. Instrukcja deweloperska wskazuje też wariant: build Vue serwowany z API przez jego zaufany certyfikat HTTPS. Formularze nie wysyłają haseł z originu HTTP; pokazują komunikat wymagania HTTPS.

#### 2. Widoki i wspólny układ

**Files**: `frontend/src/App.vue`, `frontend/src/views/LoginView.vue`, `frontend/src/views/RegisterView.vue`, `frontend/src/views/AccountHomeView.vue`, `frontend/src/assets/main.css`

**Intent**: Dostarczyć kompletny przepływ konta i czytelne stany, zachowując mały zakres UI.

**Contract**: Rejestracja: email, hasło, powtórzenie hasła; po 201 komunikat sukcesu i osobne logowanie z zachowanym bezpiecznym returnUrl. Logowanie: email/hasło, stan wysyłania, ogólny błąd, obsługa 429; bez checkboxa pamiętania, bo trwałe 7 dni jest wybraną polityką. Prywatny widok pokazuje email i przycisk wylogowania oraz miejsce na przyszłe funkcje; nie pokazuje wymyślonych wydatków. Formularze mają etykiety, autocomplete, dostępne błędy i obsługę klawiatury. Usunąć układ startera z globalnym dwukolumnowym gridem; widoki mają działać na szerokości 320 px i desktopie. Brak migania prywatnego widoku przed rozstrzygnięciem sesji.

#### 3. Testy UI i konfiguracja typów

**Files**: `frontend/vitest.config.ts`, `frontend/tsconfig.vitest.json`, `frontend/tsconfig.json`, `frontend/package.json`, `frontend/package-lock.json`, `frontend/src/__tests__/auth.spec.ts`, `frontend/src/__tests__/routing.spec.ts`, `.dockerignore`

**Intent**: Automatycznie chronić stan sesji i przejścia formularzy, które samo budowanie Vue nie sprawdza.

**Contract**: Vitest + Vue Test Utils z jsdom; skrypt `test:unit` obsługuje `--run`. Osobny projekt typów testów jest włączony do root references, z oddzielnym tsbuildinfo; nie pomijać testów w kontroli typów. Mockowane odpowiedzi API sprawdzają pełne ścieżki formularzy, powrót, brak prywatnego renderowania przed me, 401/5xx i nieudane wylogowanie. Testy obejmują zewnętrzne/kodowane returnUrl oraz rotację tokenu po zmianie tożsamości. Dodać wyjątek obrazu dla `vitest.config.ts`; kod i fixtures testowe nie trafiają do bundla produkcyjnego.

### Success Criteria:

#### Automated Verification:

- Testy UI i routingu przechodzą: `npm --prefix frontend run test:unit -- --run`.
- Kontrola typów aplikacji i testów przechodzi: `npm --prefix frontend run type-check`.
- Produkcjny build Vue przechodzi: `npm --prefix frontend run build`.
- Testy backendu nadal przechodzą: `dotnet test backend/PennyPath.slnx`.
- Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual Verification:

- Użytkownik potwierdził czytelność formularzy na mobile i desktopie, obsługę klawiatury, brak prywatnego widoku przed potwierdzeniem sesji oraz komunikat wymagania HTTPS; odbiór pełnego przepływu z PostgreSQL następuje w fazie 4.

**Implementation Note**: Po zielonej weryfikacji automatycznej zatrzymać się na potwierdzenie ręcznej kontroli tej fazy. Faza 4 wymaga rzeczywistego środowiska Docker/CI; niedostępne kontrole pozostają oczekujące, bez oznaczania sukcesu.

---

## Phase 4: Integracja HTTPS w LAN

### Overview

Połączyć funkcję z docelowym wdrożeniem LAN i sprawdzić gotowy obraz, PostgreSQL, HTTPS oraz zachowanie sesji podczas aktualizacji. Ta faza wymaga hosta ARM64 z Dockerem/Compose lub wykonania kontroli w istniejącym CI.

### Changes Required:

#### 1. HTTPS, trwałe klucze i zaufane proxy

**Files**: `compose.yaml`, `deploy/Caddyfile`, `.env.example`, `Dockerfile`, `backend/PennyPath.Api/Auth/AuthConfiguration.cs`, `backend/PennyPath.Api/Program.cs`

**Intent**: Udostępnić konta przez HTTPS w LAN i zachować sesje przy wymianie kontenera.

**Contract**: Serwis `proxy` z oficjalnego stabilnego obrazu Caddy 2 (przypięta wersja przy implementacji) kończy TLS przez `tls internal` i przekazuje ruch do `app:8080`. Konfiguracja używa wymaganego `LAN_HOST` (stały adres IP lub nazwa osiągalna na urządzeniach) oraz `HTTPS_PORT` domyślnie 8443. Caddy nie wymaga publicznego DNS/ACME; redirect z publicznego HTTP jest wyłączony. Dostęp użytkownika: `https://<LAN_HOST>:8443` przy porcie domyślnym. Wewnętrzne HTTP aplikacji pozostaje dla healthchecków, a publikowane `APP_PORT` jest związane wyłącznie z `127.0.0.1`. PostgreSQL nadal bez publikowanego portu.

Osobna sieć `edge`: domyślny `EDGE_SUBNET=172.30.90.0/24`, `PROXY_IP=172.30.90.2`; oba parametry są jawne i zmieniane razem przy konflikcie sieci. App łączy edge z obecną siecią bazy. ASP.NET przetwarza Forwarded-For/Proto wyłącznie od dokładnego `PROXY_IP`, przed auth/CSRF; Caddy zastępuje niezaufane nagłówki wejściowe. Nie ufać wszystkim proxy ani wszystkim prywatnym sieciom.

Nazwane wolumeny `proxy_data` i `proxy_config` zachowują CA/konfigurację, a `app_keys` przechowuje Data Protection w `/var/lib/pennypath/keys`. Ustawić stałą nazwę aplikacji `PennyPath`; katalog kluczy ma właściciela runtime APP_UID i ograniczone uprawnienia, potwierdzone przy uruchomieniu bez roota. Testy/development mają odrębne klucze. Root CA jest dodawane do zaufanych na urządzeniach przez operatora; prywatny klucz CA pozostaje wyłącznie na serwerze. Brak zapisu TLS/secrets do repozytorium i brak obejścia ostrzeżeń certyfikatu jako odbioru.

#### 2. Migracja w transakcji wdrożenia i pierwsza konfiguracja

**Files**: `scripts/deploy.sh`, `scripts/test-deploy.py`, `context/deployment/deploy-plan.md`, `context/deployment/mac-m1-setup.md`

**Intent**: Nie uruchamiać nowego wydania z brakującym schematem i zachować obecny kontrakt wycofania obrazu.

**Contract**: Po pobraniu nowego obrazu, przed zmianą działającego app, uruchomić jednorazowo jego `--migrate` przez Compose. Błąd migracji kończy wydanie i zachowuje działający kontener oraz release.env; nie cofa automatycznie schematu. Jawny `rollback` pomija migrator starszego obrazu i zachowuje bazę/klucze/proxy. Domyślny aktualny DEPLOY_DIR to `$HOME/Serwer/penny-path/deploy`; dokumentacja i override mają być spójne z faktycznym skryptem. Testy atrap obejmują także błąd migracji przed wymianą app.

Jednorazowa instrukcja operatora: kopia/odtworzenie bazy, wyłączenie DEPLOY_ENABLED na czas zmiany konfiguracji, synchronizacja Compose i Caddyfile do trwałego katalogu, zachowanie istniejącego hasła/volumenu, ustawienie LAN_HOST/portu/sieci, start proxy i zaufanie root CA na urządzeniach. Automatyczny deploy aktualizuje app; utrzymanie proxy/CA jest jawnym krokiem operacyjnym. Planowanie i implementacja plików nie wykonują instalacji, publikacji ani zmian trust store na rzeczywistych urządzeniach.

#### 3. Testy gotowego obrazu i sesji na PostgreSQL

**Files**: `scripts/test-auth.py`, `scripts/test-compose.sh`, `scripts/smoke-test.py`, `Dockerfile`, `.dockerignore`, `.github/workflows/containers.yml`

**Intent**: Sprawdzić produkcyjne pakowanie, rzeczywisty provider i zachowanie sesji przez HTTPS.

**Contract**: Build obrazu uruchamia testy backendu w etapie SDK i testy Vue przed buildem frontendowym; runtime zawiera tylko opublikowane API i statyczny frontend. Wszystkie potrzebne źródła/testy SDK mają jawne wyjątki .dockerignore, bez bin/obj; nie polegać na nieużywanym przez final image etapie testowym.

`scripts/test-auth.py` używa biblioteki standardowej Python: URL HTTPS, `--ca-cert PATH` i cookie jars; opcje `--state PATH` zapisują kontrolną syntetyczną sesję i ID, `--resume PATH` weryfikują ten sam principal po aktualizacji/rollbacku. Plik stanu jest wyłącznie ulotnym artefaktem testu z uprawnieniami 0600, bez haseł i bez wypisywania cookie, nigdy plikiem stanu realizacji zmiany. Brak --insecure lub wyłączania weryfikacji certyfikatu.

`scripts/test-compose.sh` nadal izoluje projekt, bazę, wolumeny i registry. Używa `LAN_HOST=localhost`, `APP_PORT=18080`, `HTTPS_PORT=18443`, `EDGE_SUBNET=172.30.91.0/24`, `PROXY_IP=172.30.91.2`, eksportuje wyłącznie certyfikat root CA proxy i sprawdza register/login/me/logout/CSRF, dwa konta, migrację na pustym PostgreSQL i idempotentne ponowienie. Zapisana sesja działa po odtworzeniu app, aktualizacji oraz rollbacku zgodnego obrazu; konto ma to samo ID. Zachować dotychczasowe sprawdzenia ARM64, app non-root, trwałości bazy, failed pull i health rollback. Fixture niezdrowego obrazu musi obsługiwać --migrate sukcesem, żeby test nadal dochodził do błędu healthchecku, a nie kończył się wcześniej na migracji. Sprzątanie usuwa wyłącznie własny projekt testowy.

Przed utworzeniem sieci test sprawdza nakładanie podsieci z istniejącymi sieciami Dockera. Konflikt kończy test czytelnym błędem; test nie zmienia ani nie usuwa istniejących sieci. Obie wartości EDGE_SUBNET/PROXY_IP są jawnie ustawiane dla projektu testowego, niezależnie od konfiguracji wdrożenia.

Existing smoke nadal sprawdza HTML/assets/SPA, publiczny health/weather i API 404; nie traktować go jako testu sesji. Nowe kontrole działają w istniejącym `verify`, blokując `publish`. Nie przywracać odłożonej fazy CI dla F-01.

#### 4. Instrukcja uruchomienia i odbioru

**Files**: `README.md`, `frontend/README.md`, `backend/AGENTS.md`, `frontend/AGENTS.md`, `context/foundation/tech-stack.md`

**Intent**: Umożliwić odtworzenie konfiguracji i późniejsze rozwijanie prywatnych wydatków.

**Contract**: Opisać polecenia restore/build/test, generowanie SQL i tryb --migrate, nazwy zmiennych bez sekretów, lokalny HTTPS oraz granicę wspólnego originu. Dokumentować przyjęte zasady rejestracji i 7 dni, odzyskiwanie hasła jako S-08 oraz stabilny userId/claim dla S-02. Zaktualizować informacje o faktycznie dodanych narzędziach testowych. Rzeczywisty odbiór LAN wykonuje operator na dwóch kontach i co najmniej dwóch kontekstach przeglądarki.

### Success Criteria:

#### Automated Verification:

- Cały zestaw backendu przechodzi: `dotnet test backend/PennyPath.slnx`.
- Testy i produkcyjny build Vue przechodzą: `npm --prefix frontend run test:unit -- --run` oraz `npm --prefix frontend run build`.
- Testy transakcji wdrożeniowej z migratorem przechodzą: `python3 scripts/test-deploy.py`.
- Skrypty Bash mają poprawną składnię: `bash -n scripts/deploy.sh` oraz `bash -n scripts/test-compose.sh`.
- Gotowy obraz przechodzi build i wbudowane testy na hoście ARM64 z Dockerem: `docker build --platform linux/arm64 -t pennypath:ci .`.
- Pełna kontrola HTTPS/PostgreSQL/sesji i regresji przechodzi na tym samym hoście: `bash scripts/test-compose.sh`.
- Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual Verification:

- Użytkownik potwierdził zaufany HTTPS w LAN na dwóch urządzeniach/kontekstach, rejestrację dwóch kont, powrót po logowaniu, ponowne otwarcie przeglądarki z zachowaniem sesji i wylogowanie bez dostępu do drugiego konta.
- Operator potwierdził zachowanie istniejącej bazy, wykonanie migracji przed wydaniem, trwałe klucze/CA i czytelność instrukcji konfiguracji; wyników Dockera nie zastąpiono lokalną kompilacją.

Doprecyzowanie kryteriów 4.6 i 4.9 po review F1: test Compose sprawdza katalog sekretu `0700`, plik `0444` i odczyt sekretu przez rzeczywisty proces app/migratora bez roota, bez wypisywania zawartości. Operator potwierdza ten sam układ uprawnień przed pierwszym uruchomieniem na docelowym hoście. Brak Dockera pozostawia weryfikację mountu oczekującą.

Doprecyzowanie kryterium 4.6 po review F2: test używa osobnej pary EDGE_SUBNET/PROXY_IP, sprawdza kolizję podsieci przed jej utworzeniem i przy konflikcie kończy się bez ingerencji w istniejące sieci.

**Implementation Note**: Po zielonej weryfikacji automatycznej zatrzymać się na potwierdzenie ręcznej kontroli tej fazy. Faza 4 wymaga rzeczywistego środowiska Docker/CI; niedostępne kontrole pozostają oczekujące, bez oznaczania sukcesu.

---

## Testing Strategy

### Unit Tests:

- Identity przez relacyjny testowy magazyn: hashowanie, normalizacja, unikalność, trwałe ID.
- Kontrolowany czas sesji: t < login + 7 dni jest dopuszczony, t >= login + 7 dni daje 401; brak przedłużenia po aktywności.
- Vue: formularze, stan ładowania/błędu, walidacja returnUrl, brak prywatnego renderowania, odświeżenie CSRF po zmianie tożsamości.

### Integration Tests:

- WebApplicationFactory/HTTPS i odrębne cookie jars; CSRF działa również na anonimowym logowaniu/rejestracji. Brak CSRF, błędne dane, brak sesji i błąd połączenia nie kończą się fałszywym sukcesem.
- Gotowy obraz + rzeczywisty PostgreSQL 18 + Caddy w izolowanym Compose. Rejestracja dwóch kont, niezależne principal, ponowne uruchomienie app i rollback zachowują tożsamość oraz schema/key state.
- W fazach dodających testy kontrola celowego zepsucia dotyczy chronionego zachowania: faza 1 — ominąć unikalność modelu lub hashowanie; faza 2 — wydłużyć sesję poza 7 dni lub ominąć CSRF; faza 3 — dopuścić zewnętrzny returnUrl; faza 4 — pominąć migrator, aby test błędu migracji stał się czerwony. Zawsze przywrócić kod; nie osłabiać oczekiwań.

### Manual Testing Steps:

1. Przejrzeć migrację i rozdzielenie stabilnego ID od emaila.
2. Potwierdzić stany formularzy, dostępność klawiaturą i układ mobile/desktop.
3. Na dwóch oddzielnych kontekstach przeglądarki utworzyć A/B; sprawdzić odczyt własnego emaila, bezpieczny powrót z chronionej strony oraz brak dostępu po logout.
4. Ponownie uruchomić przeglądarkę w okresie ważności i potwierdzić zachowanie logowania; granicę 7 dni sprawdzają testy zegara.
5. Zaufać wyłącznie root CA konkretnego serwera na urządzeniach LAN i potwierdzić HTTPS bez obchodzenia błędów.
6. Potwierdzić prawdziwe kontrole Docker/PG i zachowanie bazy, CA oraz kluczy podczas wymiany app.

## Performance Considerations

Brak cache danych kont poza pamięciowym stanem klienta i standardową sesją. Guard współdzieli trwający odczyt me, aby nie mnożyć równoległych żądań. Indeksy znormalizowanego emaila wspierają logowanie. Limity prób i hash Identity są wystarczające dla początkowych do 10 użytkowników; nie wprowadzać infrastruktury rozproszonej ani arbitralnych benchmarków.

## Migration Notes

Migracja jest addytywna. Przed pierwszym użyciem rzeczywistej bazy operator tworzy i sprawdza kopię, zachowuje istniejące hasło oraz wolumen PostgreSQL i synchronizuje nową konfigurację. Nowy obraz stosuje migrację przed przełączeniem app, a rollback zmienia tylko obraz i nie usuwa danych ani kluczy. Pierwszy powrót do startera sprzed S-01 może oznaczać brak funkcji konta, lecz pozostawia tabele Identity do kolejnego zgodnego wydania.

Zaakceptowane ryzyko F3 z [plan-review.md](reviews/plan-review.md): przy aktywnym DEPLOY_ENABLED i osobnych merge'ach faz do main pośredni obraz może zostać wdrożony przed przygotowaniem konfiguracji i migracji z fazy 4. Użytkownik akceptuje ryzyko do uwzględnienia podczas implementacji; proponowana bramka wyłączenia deployu przed pierwszym merge'em nie została dodana do planu.

CA i Data Protection keys wymagają kopii/ochrony jak inne sekrety; prywatny klucz CA nie trafia na urządzenia klienckie. Utrata kluczy może wymusić ponowne logowanie; nie usuwać ich przy zwykłym wdrożeniu. Lokalny SQLite w testach nie jest alternatywną bazą produkcyjną.

## References

- `context/changes/private-account-access/change.md`; `context/foundation/roadmap.md`, S-01 oraz granice S-02/S-08.
- `context/foundation/prd.md`, FR-001, Access Control, US-01 i Non-Functional Requirements.
- `context/foundation/tech-stack.md`, `context/foundation/infrastructure.md`, `context/deployment/deploy-plan.md`.
- `backend/AGENTS.md`, `frontend/AGENTS.md`, `compose.yaml`, `.dockerignore`, `scripts/deploy.sh`, `scripts/test-compose.sh`, `scripts/test-deploy.py`.
- [Identity dla SPA i sesje cookie](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0).
- [Zachowanie 401/403 dla API w .NET 10](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/api-endpoint-auth?view=aspnetcore-10.0).
- [Data Protection w kontenerze](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).
- [Npgsql EF Core 10](https://www.npgsql.org/efcore/release-notes/10.0.html), [migracje EF Core](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).
- [Caddy Local HTTPS](https://caddyserver.com/docs/automatic-https), [reverse proxy](https://caddyserver.com/docs/caddyfile/directives/reverse_proxy).
- [Vite dev proxy](https://vite.dev/config/server-options#server-proxy).

## Progress

### Phase 1: Trwałe konta

#### Automated

- [x] 1.1 Zależności rozwiązania i lokalne narzędzie EF są przywrócone: `dotnet restore backend/PennyPath.slnx` oraz `dotnet tool restore`. — f4c0ad2
- [x] 1.2 Rozwiązanie kompiluje się: `dotnet build backend/PennyPath.slnx --no-restore`. — f4c0ad2
- [x] 1.3 Testy trwałości i niezmienników kont przechodzą: `dotnet test backend/PennyPath.Api.Tests/PennyPath.Api.Tests.csproj --no-restore --filter Category=Persistence`. — f4c0ad2
- [x] 1.4 Powstaje idempotentny SQL bez połączenia z bazą: `dotnet tool run dotnet-ef migrations script --idempotent --project backend/PennyPath.Api --output artifacts/private-account-access.sql`. — f4c0ad2
- [x] 1.5 Zmiany nie zawierają błędów whitespace: `git diff --check`. — f4c0ad2

#### Manual

- [x] 1.6 Użytkownik potwierdził, że wygenerowana migracja dodaje tabele Identity i unikalny indeks emaila oraz nie zmienia istniejących danych. — f4c0ad2

### Phase 2: API logowania i sesji

#### Automated

- [x] 2.1 Testy HTTP, CSRF i maksymalnego czasu sesji przechodzą: `dotnet test backend/PennyPath.Api.Tests/PennyPath.Api.Tests.csproj --filter Category=Auth`. — ed05d8f
- [x] 2.2 Cały zestaw testów backendu przechodzi: `dotnet test backend/PennyPath.slnx`. — ed05d8f
- [x] 2.3 Przykłady HTTP i kod przechodzą kompilację: `dotnet build backend/PennyPath.slnx --no-restore`. — ed05d8f
- [x] 2.4 Zmiany nie zawierają błędów whitespace: `git diff --check`. — ed05d8f

#### Manual

- [x] 2.5 Użytkownik potwierdził kontrakt rejestracja → osobne logowanie, prywatny odczyt bieżącego użytkownika i maksymalnie 7-dniową sesję bez jej automatycznego przedłużania. — ed05d8f

### Phase 3: Przepływ w Vue

#### Automated

- [x] 3.1 Testy UI i routingu przechodzą: `npm --prefix frontend run test:unit -- --run`. — bcdcab7
- [x] 3.2 Kontrola typów aplikacji i testów przechodzi: `npm --prefix frontend run type-check`. — bcdcab7
- [x] 3.3 Produkcjny build Vue przechodzi: `npm --prefix frontend run build`. — bcdcab7
- [x] 3.4 Testy backendu nadal przechodzą: `dotnet test backend/PennyPath.slnx`. — bcdcab7
- [x] 3.5 Zmiany nie zawierają błędów whitespace: `git diff --check`. — bcdcab7

#### Manual

- [x] 3.6 Użytkownik potwierdził czytelność formularzy na mobile i desktopie, obsługę klawiatury, brak prywatnego widoku przed potwierdzeniem sesji oraz komunikat wymagania HTTPS; odbiór pełnego przepływu z PostgreSQL następuje w fazie 4. — 4497189

### Phase 4: Integracja HTTPS w LAN

#### Automated

- [x] 4.1 Cały zestaw backendu przechodzi: `dotnet test backend/PennyPath.slnx`. — 4497189
- [x] 4.2 Testy i produkcyjny build Vue przechodzą: `npm --prefix frontend run test:unit -- --run` oraz `npm --prefix frontend run build`. — 4497189
- [x] 4.3 Testy transakcji wdrożeniowej z migratorem przechodzą: `python3 scripts/test-deploy.py`. — 4497189
- [x] 4.4 Skrypty Bash mają poprawną składnię: `bash -n scripts/deploy.sh` oraz `bash -n scripts/test-compose.sh`. — 4497189
- [x] 4.5 Gotowy obraz przechodzi build i wbudowane testy na hoście ARM64 z Dockerem: `docker build --platform linux/arm64 -t pennypath:ci .`. — 4497189
- [x] 4.6 Pełna kontrola HTTPS/PostgreSQL/sesji i regresji przechodzi na tym samym hoście: `bash scripts/test-compose.sh`. — 4497189
- [x] 4.7 Zmiany nie zawierają błędów whitespace: `git diff --check`. — 4497189

#### Manual

- [x] 4.8 Użytkownik potwierdził zaufany HTTPS w LAN na dwóch urządzeniach/kontekstach, rejestrację dwóch kont, powrót po logowaniu, ponowne otwarcie przeglądarki z zachowaniem sesji i wylogowanie bez dostępu do drugiego konta. — 4497189
- [x] 4.9 Operator potwierdził zachowanie istniejącej bazy, wykonanie migracji przed wydaniem, trwałe klucze/CA i czytelność instrukcji konfiguracji; wyników Dockera nie zastąpiono lokalną kompilacją. — 4497189
