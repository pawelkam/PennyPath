# Dostęp do własnego konta — Plan Brief

> Full plan: [plan.md](plan.md)

## What & Why

S-01 daje użytkownikowi samodzielną rejestrację, logowanie emailem/hasłem i prywatny obszar w przeglądarce.
Rozstrzyga otwarte pytanie o uzyskanie konta i anonimowy dostęp; tożsamość będzie podstawą prywatnych wydatków w S-02.

## Starting Point

Istnieją Vue i API .NET 10, wspólny obraz ARM64, PostgreSQL 18 i CI z testami wdrożeniowymi.
Nie ma kont, połączenia API z bazą, routera Vue ani testów produktu; serwer LAN działa obecnie przez HTTP.

## Desired End State

Użytkownik otwiera zaufany HTTPS w LAN, rejestruje konto i loguje się z powrotem do żądanej prywatnej strony.
Sesja jest zapamiętana maksymalnie 7 dni od logowania, także po ponownym uruchomieniu przeglądarki.
Prywatny obszar i API rozpoznają własne konto; wylogowanie kończy bieżącą sesję.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Konto | Samodzielna rejestracja w aplikacji | Uzgodnione z użytkownikiem; brak ręcznego zakładania kont |
| Anonimowe wejście | Logowanie i bezpieczny powrót do lokalnej strony | Zachowuje pierwotny cel wejścia |
| Sesja | Trwała do 7 dni, bez przedłużania aktywnością | Wybrana wygoda, z jednoznaczną maksymalną granicą |
| Email | Brak potwierdzania przed logowaniem | Konto działa od razu; wysyłka i odzyskanie hasła są poza S-01 |
| Transport | HTTPS w LAN przez Caddy z lokalnym CA | Uzgodniony dostęp domowy bez publicznego tunelu |
| Technika | Identity + EF Core 10/Npgsql, cookie, Antiforgery | Korzysta ze standardowego .NET i istniejącego PostgreSQL |
| Własność | Trwałe userId z principal, niezależne od emaila | Kontrakt dla właściciela przyszłych wydatków S-02 |

## Scope

**In scope:** magazyn kont, migracja, register/login/logout/me, CSRF i limity, routing/formularze Vue, trwałe klucze, HTTPS w LAN oraz testy.
**Out of scope:** odzyskanie hasła (S-08), potwierdzanie emaila, 2FA, role/admin, globalne odwoływanie sesji, wydatki i publiczny tunel.
Rejestracja tworzy konto, a potem prowadzi do osobnego logowania; nie loguje automatycznie.

## Architecture / Approach

Przeglądarka → Caddy HTTPS → API z Identity → PostgreSQL; cookie auth jest Secure/HttpOnly, userId jest sprawdzane serwerowo.
Vue ma publiczne /login i /register oraz chronione /app. Stan konta pozostaje w pamięci; po zmianie tożsamości odświeżany jest CSRF.
Caddy CA i klucze Data Protection mają trwałe wolumeny. HTTP app pozostaje dostępne tylko lokalnie do healthchecków.
App ufa nagłówkom proxy wyłącznie od jawnego PROXY_IP. Migracja kończy się przed wymianą działającego wydania.
Test Compose jawnie używa osobnej pary `EDGE_SUBNET=172.30.91.0/24` i `PROXY_IP=172.30.91.2`; przed utworzeniem sieci wykrywa konflikt i kończy test bez zmieniania istniejących sieci.
App i migrator czytają ten sam sekret PostgreSQL tylko do odczytu: plik `0444` w prywatnym katalogu hosta `0700`; `.env` pozostaje `0600`. Przygotowanie fixture i instrukcji sekretu należy do fazy 1, a rzeczywisty odczyt bez roota jest sprawdzany z Dockerem w fazie 4.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Trwałe konta | Identity/PostgreSQL, migracja, ID, testy modelu | Unikalność emaila i zachowanie istniejącej bazy |
| 2. API logowania i sesji | Endpointy, CSRF, cookie 7 dni i testy HTTP | Brak przedłużania sesji i odrzucanie anonimowego dostępu |
| 3. Przepływ w Vue | Formularze, routing, powrót, wygasanie | Otwarty redirect i prywatny widok przed odczytem sesji |
| 4. Integracja HTTPS w LAN | Caddy, klucze, migrator wydania i realne testy | Trust CA, pakowanie i sesje podczas aktualizacji |

**Prerequisites:** SDK .NET 10.0.401, Node zgodny z repo i Python; faza 4 wymaga Dockera ARM64/Compose w CI lub na osobnym hoście.
**Estimated effort:** około 4–6 sesji, orientacyjnie 12–20 godzin wraz z testami i odbiorem; bez czasu oczekiwania na dostęp do serwera.

## Open Risks & Assumptions

- W tym workspace nie ma Dockera ani PostgreSQL; SQLite w testach nie zastępuje kontroli prawdziwego providera z fazy 4.
- Operator ustawia stały LAN_HOST i ufa CA na urządzeniach; sekret/klucz prywatny CA nie trafia do klienta ani repo.
- Nowe katalogi muszą być w allowlist .dockerignore; wolumen kluczy musi działać z użytkownikiem app bez roota.
- Rollback obrazu nie cofa schematu. Błąd migracji musi pozostawić działający app, a test niezdrowego obrazu musi dochodzić do healthchecku.
- Zaakceptowane ryzyko F3: przy aktywnym DEPLOY_ENABLED osobne merge'e faz mogą wdrożyć pośredni obraz przed konfiguracją i migracjami z fazy 4; do uwzględnienia podczas implementacji. Nie dodano proponowanej bramki wyłączenia deployu przed pierwszym merge'em.
- S-01 rozdziela tożsamości kont; izolacja rzeczywistych wydatków nadal powstaje w S-02.

## Success Criteria (Summary)

- Dwa konta działają niezależnie; brak ważnej sesji daje 401, a brak CSRF odrzuca POST.
- Powrót po logowaniu jest lokalny; sesja działa po restarcie przeglądarki, wygasa najpóźniej po 7 dniach i kończy się przez logout.
- Obraz, migracje, HTTPS i trwałość kluczy są sprawdzone z PostgreSQL oraz odebrane w LAN.

## References

- [Roadmapa S-01](../../foundation/roadmap.md), [PRD](../../foundation/prd.md), [pełny plan i kontrakty](plan.md).
- [Identity SPA](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0), [Caddy HTTPS](https://caddyserver.com/docs/automatic-https).
