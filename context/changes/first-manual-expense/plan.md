# Pierwszy ręczny wydatek i aktualne podsumowanie — Implementation Plan

## Overview

S-02 dostarcza pierwszy przepływ wydatków: wybór strefy konta, ręczny zapis dodatniego wydatku w PLN, prywatny rejestr i zgodne z nim podsumowanie bieżącego miesiąca z wykresem kategorii oraz podkategoriami. Użytkownik zatwierdził złożoność MEDIUM, siedem pytań rozszerzonych o trzy doprecyzowania oraz cztery fazy opisane poniżej. Plan obejmuje ustawienie strefy jako uzgodnione rozszerzenie S-02; przychody pozostają późniejszą funkcją.

## Current State Analysis

- Stan bazowy kodu: `c4d19d8`. F-01 ma ukończone 4/4 kroki, a S-01 26/26 i `status: implemented`; zależności S-02 są wykonane. Wpis S-01 w roadmapie nadal ma historyczne `in-progress`, a jej opis Baseline poprzedza implementację. Nie traktować tych wpisów jako braku kont ani blokady S-02.
- `backend/PennyPath.Api/Program.cs:20` rejestruje Npgsql i `PennyPathIdentityDbContext`; `Data/PennyPathIdentityDbContext.cs:7` obejmuje tylko Identity. Nie ma modeli wydatków, kategorii, ustawień użytkownika ani agregacji. Stabilnym właścicielem jest `IdentityUser.Id` / `ClaimTypes.NameIdentifier`, zgodnie z `backend/AGENTS.md`.
- `Data/IdentityMigrationRunner.cs:7` stosuje migracje istniejącego kontekstu przez `--migrate`. Normalny start ich nie stosuje; wdrożenie uruchamia migrator przed wymianą aplikacji. Rozbudowa tego samego kontekstu zachowuje obecny mechanizm wydania.
- `Controllers/AuthController.cs:13` jawnie stosuje autoryzację, filtr HTTPS/CSRF i no-store. Samo skonfigurowanie Antiforgery nie chroni nowych kontrolerów. Filtr `Auth/RequireHttpsForAuthFilter.cs:19` waliduje POST; wszystkie nowe mutacje w tym planie będą POST.
- Globalna walidacja w `Auth/AuthConfiguration.cs:127` rozpoznaje wyłącznie email/password, a pozostałe błędy mapuje na `body`. `Auth/AuthContracts.cs:24` daje już bezpieczny ProblemDetails ze stabilnym `code`; trzeba zachować ten format i rozszerzyć mapowanie pól.
- `frontend/src/router/index.ts:24` ma chronione `/app`; `AccountHomeView.vue:24` pokazuje konto i miejsce na przyszłe funkcje. `api/auth.ts:32` zawiera klienta ograniczonego do ścieżek auth, a jego błędy 400 dotyczą emaila/hasła. `auth/session.ts:6` przechowuje sesję w pamięci i chroni ją przed spóźnionymi odpowiedziami. Nie ma klienta wydatków, ustawień ani biblioteki wykresów.
- `tests/fixtures/spending-reconciliation/cases.json` zawiera sześć niezależnie ustalonych oczekiwań. F-01 sprawdza kwoty, zakupowe daty, skład rejestru i hierarchię; nie ustala reguł formularza ani bieżącego okresu. Te decyzje podjęto w tej rozmowie.
- `backend/PennyPath.Api.Tests/TestApplicationFactory.cs:29` zastępuje PostgreSQL przez SQLite i tworzy schemat przez EnsureCreated. `scripts/test-compose.sh` weryfikuje rzeczywisty PostgreSQL/HTTPS, migracje i sesje, lecz jeszcze nie wydatki. `.dockerignore` dopuszcza tylko wybrane katalogi; wspólne dane F-01 nie trafiają obecnie do etapu testowego Dockerfile.
- Brak frame.md/research.md dla tej zmiany, `context/foundation/lessons.md` i `docs/reference/contract-surfaces.md`. Ustalenia opierają się na PRD, roadmapie, poprzednich zmianach, badaniu kodu i jawnych odpowiedziach użytkownika. Nie są potrzebne dodatkowe rytuały discovery.

## Desired End State

Po zalogowaniu użytkownik bez zapisanego ustawienia wybiera strefę czasową; formularz proponuje aktualną strefę przeglądarki. Wybór zapisuje się na koncie i obowiązuje na każdym urządzeniu. Przy kolejnych wejściach aplikacja korzysta z niego bez ponownego pytania; użytkownik może go zmienić w prostych ustawieniach.

Użytkownik dodaje nazwę, dodatnią kwotę PLN, datę zakupu, kategorię oraz opcjonalną podkategorię. Dozwolone są daty przeszłe i przyszłe. Potwierdzony zapis jest trwały, dostępny we własnym rejestrze i uwzględniany w miesiącu daty zakupu. Domyślna lista pokazuje bieżący miesiąc, a przełącznik historii udostępnia wszystkie własne wpisy.

Podsumowanie zawsze dotyczy bieżącego miesiąca kalendarzowego w zapisanej strefie konta. Obejmuje również przyszłe zakupy datowane w tym miesiącu. Pokazuje dokładną sumę, wykres kołowy kategorii i podkategorie wybranej kategorii, w tym „Bez podkategorii”. Historię i podsumowanie opisują jawne zakresy; zmiana listy na całą historię nie zmienia zakresu wykresu. Wynik potwierdzają testy zapisu i agregacji, testy Vue, prawdziwy PostgreSQL przez HTTPS oraz odbiór dwóch kont.

### Key Discoveries:

- Gotowe konto i sesja są podstawą własności, ale każdy nowy odczyt i zapis wymaga sprawdzenia właściciela na serwerze; frontend nie jest granicą autoryzacji (`backend/AGENTS.md`, `AuthController.cs:13`).
- Kwoty trzeba obciąć w API przed zapisem. PostgreSQL sam zaokrągla wartość przekraczającą skalę kolumny; deklaracja numeric z dwoma miejscami nie realizuje decyzji użytkownika o obcięciu w dół.
- Data zakupu jest datą bez godziny. Strefa określa „dzisiaj” i bieżący miesiąc, a nie przelicza historyczne daty zakupów (`cases.json`, dokumentacja Npgsql).
- Klient HTTP, obsługa 401 i rotacja CSRF wymagają współdzielenia między kontem i wydatkami. Pamięciowy stan wydatków musi być czyszczony także po zwykłym wylogowaniu i zmianie użytkownika (`session.ts:23`, `api/auth.ts:47`).

## What We're NOT Doing

- Przychody, saldo, budżety, prognozy oszczędności, osobny stan „opłacony/planowany”, harmonogramy płatności, cykliczne wydatki i powiadomienia. Przyszła data jest zwykłą datą wydatku, zgodnie z wyborem użytkownika.
- Ujemne wydatki, zwroty, kwoty zerowe i wielowalutowość. Nie dodawać teraz modelu przychodów ani discriminatorów dla przyszłych modułów.
- Porównanie z poprzednim okresem (S-03) i konfigurowalny dzień początku okresu 1–28 (S-04). S-02 używa miesięcy kalendarzowych.
- Edycja/usuwanie wydatków, podział zakupu, edycja/usuwanie kategorii, odzyskanie hasła oraz CSV; pozostają odpowiednio w kolejnych przekrojach roadmapy.
- Automatyczna klasyfikacja, automatyczne odrzucanie podobnych zakupów, offline, prywatne dane w localStorage/sessionStorage i automatyczne ponawianie POST.
- Przywracanie odłożonej fazy CI F-01, aktualizacja historycznych opisów całej roadmapy, nowa usługa, drugi kontekst bazy, infrastruktura cache lub wdrożenie na rzeczywisty serwer w ramach pisania tego planu.

## Implementation Approach

Rozszerzyć istniejący kontekst EF i API. Dodać prywatne ustawienie strefy, osobisty zestaw kategorii i wydatki. Po pierwszym zatwierdzeniu strefy inicjalizować zestaw kategorii dokładnie raz w tej samej transakcji; dotyczy to zarówno istniejących kont S-01, jak i nowych. GET nie tworzy danych. Korzystać z dotychczasowego migratora, cookie, HTTPS, CSRF i narzędzi testowych. Vue używa jednego współdzielonego transportu HTTP oraz małych komponentów formularza, rejestru i wykresu SVG; nie potrzeba nowej biblioteki wykresów ani globalnego magazynu stanu.

### Decyzje użytkownika

| Obszar | Uzgodniona reguła |
| --- | --- |
| Okres | Miesiąc kalendarzowy: pierwszy dzień włącznie, pierwszy dzień następnego miesiąca wyłącznie |
| Strefa | Wybierana raz po zalogowaniu, jeśli jeszcze jej nie zapisano; propozycja z przeglądarki; zapis na koncie i późniejsza edycja |
| Kategorie | Sześć kategorii z poniższego zestawu; osobna kopia dla każdego konta |
| Przypisanie | Kategoria wymagana; podkategoria opcjonalna, brak widoczny jako „Bez podkategorii” |
| Kwoty | Wyłącznie dodatnie wydatki; minimum po obcięciu 0,01 PLN; zero i wartości ujemne odrzucane |
| Precyzja | Formularz nie dopuszcza więcej niż dwóch miejsc; API przy obejściu formularza obcina w dół, nigdy nie zaokrągla do najbliższej wartości |
| Daty | Przeszłe i przyszłe daty dozwolone; przyszły wydatek liczy się w miesiącu swojej daty zakupu |
| Rejestr | Bieżący miesiąc domyślnie, przełącznik całej historii; podsumowanie pozostaje miesięczne |
| Przychody | Osobna późniejsza funkcja; obecnie zapisujemy wyłącznie wydatki |

### Zestaw początkowy

Identyfikatory są stałymi technicznymi w obrębie konta, niezależnymi od etykiet i przyszłego przemianowania kategorii.

| CategoryId | Etykieta | Podkategorie: id — etykieta |
| --- | --- | --- |
| food | Jedzenie | groceries — zakupy spożywcze; restaurants — restauracje; drinks — napoje; other — inne |
| transport | Transport | fuel — paliwo; public-transport — komunikacja; other — inne |
| home | Dom | bills — czynsz i opłaty; furnishings — wyposażenie; other — inne |
| health | Zdrowie | medicines — leki; consultations — wizyty; other — inne |
| entertainment | Rozrywka | subscriptions — subskrypcje; outings — wyjścia; other — inne |
| miscellaneous | Pozostałe | other — inne |

„Bez podkategorii” jest prezentacją null, a nie dodatkową podkategorią „inne”. Nie łączyć tych grup. Fixture F-01 może tworzyć własne kategorie testowe, np. food/staples; nie zmienia to katalogu początkowego produktu.

### Kontrakty danych i kwot

- `SpendingProfile`: klucz `OwnerId` powiązany z Identity oraz wymagane `TimeZoneId` w formacie IANA lub UTC. Brak wiersza oznacza potrzebę pierwszego wyboru; migracja nie zgaduje strefy istniejących użytkowników.
- `ExpenseCategory`: klucz `(OwnerId, CategoryId)`, wymagane `Name` i porządek prezentacji. `ExpenseSubcategory`: klucz `(OwnerId, CategoryId, SubcategoryId)`, nazwa i porządek; FK do kategorii. Te klucze rozdzielają takie same identyfikatory „other” między kategoriami i kontami.
- `Expense`: generowane serwerowo `Id` UUID, wymagane `OwnerId`, `Name` po trim o długości 1–200, `Amount` decimal / PostgreSQL numeric(14,2), `PurchaseDate` DateOnly / date, `RecordedAtUtc` UTC timestamp, wymagane `CategoryId`, nullable `SubcategoryId`. Kwota po normalizacji mieści się w 0,01–999999999999,99 PLN. To techniczny limit reprezentacji, walidowany przed zapisem; nie ucinać części całkowitej.
- Baza wymusza dodatnią kwotę, właściciela oraz FK `(OwnerId, CategoryId)` i `(OwnerId, CategoryId, SubcategoryId)`; drugi jest opcjonalny. Nie można przypisać podkategorii innej kategorii lub konta. Zapisy nie przyjmują OwnerId/RecordedAtUtc od klienta. Indeks rejestru zaczyna się od OwnerId i PurchaseDate.
- Formularz przyjmuje `12`, `12,3`, `12,30` i analogiczne zapisy z kropką. Trzecią cyfrę po separatorze oraz wklejenie większej precyzji blokuje z czytelną informacją; nie wysyła takiej wartości. Samo `step=0.01` lub błąd dopiero po wysłaniu nie spełnia tej reguły.
- JSON API używa kwoty jako tekstu dziesiętnego z kropką, bez separatorów tysięcy, wykładnika, NaN/Infinity i znaków waluty. API przyjmuje nadmiarowe cyfry ułamka i obcina je przed konwersją/zapisem; nie polega na zaokrąglaniu decimal.Parse ani kolumny bazy. Ograniczyć długość tekstu wejścia do 64 znaków i liczbę cyfr całkowitych do 12; nadmiar reprezentacji daje błąd pola, nie 500.
- Przykłady kontraktu API: `12.349 → 12.34`, `12.999 → 12.99`, `0.019 → 0.01`; `0.009 → 0.00` i odrzucenie. Zero, każda kwota ujemna i przekroczenie limitu są odrzucane. UI pokazuje potwierdzoną wartość zwróconą przez serwer.
- Kwoty odpowiedzi i sum mają zawsze dwa miejsca i są tekstem. Sumy powstają dokładnie na serwerze, bez double/float i bez tolerancji testowej. Frontend nie wylicza sum finansowych; przybliżenia liczbowe mogą służyć wyłącznie geometrii wykresu.

### Kontrakt HTTP

Wszystkie poniższe endpointy wymagają konta, HTTPS i no-store. POST wymaga aktualnego CSRF w `X-CSRF-TOKEN`. `OwnerId` pochodzi wyłącznie z principal. Przy nieistniejącym lub nieprawidłowym koncie zwracane jest 401; ID innego użytkownika w danych przypisania daje ogólny błąd pola bez ujawniania jego danych.

| Metoda i ścieżka | Wejście | Wynik |
| --- | --- | --- |
| GET /api/spending/time-zones | — | 200 `{timeZoneIds:[...]}` z obsługiwanymi identyfikatorami IANA i UTC |
| GET /api/spending/settings | — | 200 `{timeZoneId:null}` przed konfiguracją albo `{timeZoneId}` |
| POST /api/spending/settings | `{timeZoneId}` | 200 `{timeZoneId,today,period:{startInclusive,endExclusive}}`; pierwszy zapis inicjalizuje kategorie atomowo; kolejne zmieniają tylko strefę |
| GET /api/expense-categories | — | 200 `{categories:[{id,name,subcategories:[{id,name}]}]}` z własnym zestawem w stałym porządku |
| POST /api/expenses | `{name,amount,purchaseDate,categoryId,subcategoryId:null|string}` | 201 zapisany ExpenseDto, z kanoniczną kwotą i serwerowym Id |
| GET /api/spending/overview | `scope=current|all`, `page` od 1, `pageSize` 1–100, domyślnie 50 | 200 `{timeZoneId,today,period,register,summary}` opisane poniżej |

ExpenseDto zawiera `id,name,amount,purchaseDate,categoryId,categoryName,subcategoryId,subcategoryName,recordedAtUtc`. Brak podkategorii to null w obu polach podkategorii. Nazwy pobierane są z własnego katalogu, nie z żądania klienta. Daty zakupu i granice mają format `YYYY-MM-DD`, a recordedAtUtc format ISO UTC.

`register` ma `{scope,items,page,pageSize,totalCount}` i obejmuje wybrany zakres. Porządek: PurchaseDate malejąco, RecordedAtUtc malejąco, Id malejąco jako stabilne rozstrzygnięcie identycznych dat. To wybór techniczny prezentacji, bez zmiany znaczenia wydatków. Podobne zakupy są osobnymi rekordami; nie deduplikować ich automatycznie.

`summary` dotyczy pełnego bieżącego miesiąca, niezależnie od strony rejestru i scope=all: `{expenseCount,total,categories:[{id,name,total,subcategories:[{id:null|string,name,total}]}]}`. Null podkategorii ma etykietę „Bez podkategorii”. Pusty wynik: total `0.00`, expenseCount 0 i pusta lista kategorii. Zwracać tylko grupy z wpisami; kliknięcie kategorii nie wymaga pobierania cudzych ani szerszych danych. Odczyt rejestru i agregacji w jednej odpowiedzi musi przedstawiać spójny stan danych, także przy równoległym zapisie; zapewnić wspólny snapshot odczytu. Nie sumować wyłącznie widocznej strony.

Brak profilu dla categories/expenses/overview daje 409 `settings_required`; GET settings i time-zones pozostają dostępne. Błędy walidacji mają 400 `validation_failed`, mapę bezpiecznych kodów pod polami `name`, `amount`, `purchaseDate`, `categoryId`, `subcategoryId`, `timeZoneId`, `scope`, `page`, `pageSize` lub `body`. Zachować authowe email/password i dotychczasowe kody `unauthorized`, `https_required`, `csrf_invalid`, `server_error`; nie ujawniać wyjątków providerów.

## Critical Implementation Details

Kolumna numeric(14,2) zaokrągla nadmiarową precyzję, dlatego obcięcie dodatniej kwoty musi nastąpić w API przed zapisem. Nie zmieniać fixture F-01 ani jego oczekiwań, aby dostosować je do błędnej agregacji; nullable podkategoria wymaga dodatkowych przypadków poza oryginalną szóstką.

Ustalenie strefy i inicjalizacja katalogu muszą być atomowe i odporne na dwa pierwsze wejścia naraz. Zmiana strefy nie przelicza PurchaseDate ani nie inicjalizuje katalogu ponownie. Po logout/401/zmianie konta spóźniony odczyt nie może przywrócić danych poprzedniego użytkownika.

Nowe źródła oraz JSON F-01 muszą trafiać do konsumowanego etapu testowego Dockerfile od fazy, która ich używa. Fixture migracji i dane testowe nie mogą przypadkowo trafić do publikacji runtime.

## Phase 1: Model danych i migracja

### Overview

Dodać trwałe wydatki, osobiste kategorie i strefę konta bez zmiany działającego uwierzytelniania. Zapewnić addytywny upgrade istniejącej bazy i testy niezmienników modelu.

### Changes Required:

#### 1. Model i konfiguracja EF

**Files**: `backend/PennyPath.Api/Spending/Expense.cs`, `SpendingProfile.cs`, `ExpenseCategory.cs`, `ExpenseSubcategory.cs`, `Data/PennyPathIdentityDbContext.cs` — ścieżki Spending są pod `backend/PennyPath.Api/`.

**Intent**: Przechowywać prywatne dane produktu i wymusić poprawne przypisania również w bazie.

**Contract**: Pola, klucze, kwoty i daty zgodne z kontraktami powyżej. W istniejącym kontekście dodać DbSets i konfigurację ograniczeń/FK/indeksów. Nie zastępować IdentityUser, nie zmieniać stabilnych ID ani istniejącej migracji Identity. Usuwanie kategorii nie może kaskadowo usuwać wydatków; samo zarządzanie kategoriami jest poza S-02.

#### 2. Migracja i pakowanie

**Files**: `backend/PennyPath.Api/Data/Migrations/` (nowa migracja `InitialSpending`, designer i snapshot), `Data/IdentityMigrationRunner.cs`, `.dockerignore`.

**Intent**: Rozszerzyć wdrożony schemat przez istniejący tryb migratora i uwzględnić nowe źródła w obrazie.

**Contract**: Migracja wyłącznie dodaje tabele/ograniczenia/indeksy S-02; zachowuje konta i dotychczasowe dane. Nie ustawia stref użytkownikom i nie tworzy kategorii bez pierwszego zatwierdzenia ustawienia. `--migrate` stosuje obie migracje i pozostaje idempotentny; komunikaty mogą mówić ogólnie o migracjach bazy. Allowlist dopuszcza `Spending/**` i potrzebne testy, nadal wykluczając bin/obj oraz sekrety. Wygenerowany SQL do ręcznego review trafia do ignorowanego `artifacts/first-manual-expense.sql`.

#### 3. Testy modelu

**Files**: `backend/PennyPath.Api.Tests/SpendingPersistenceTests.cs`, istniejąca konfiguracja projektu testów.

**Intent**: Dowieść trwałości danych i granic właściciela przed dodaniem endpointów.

**Contract**: `Category=SpendingPersistence`; SQLite jako test relacyjny obejmuje zapis i ponowne otwarcie danych, opcjonalną podkategorię, odrzucenie podkategorii z innej kategorii/właściciela, rozdzielenie identycznych CategoryId między kontami i zachowanie Identity. Testy modelu nie zastępują migracji ani dokładnych obliczeń PostgreSQL w fazie 4.

### Success Criteria:

#### Automated Verification:

- Rozwiązanie kompiluje się: `dotnet build backend/PennyPath.slnx`.
- Testy trwałości danych wydatków przechodzą: `dotnet test backend/PennyPath.slnx --filter Category=SpendingPersistence`.
- Powstaje idempotentny SQL do przeglądu: `dotnet tool run dotnet-ef migrations script --idempotent --project backend/PennyPath.Api --output artifacts/first-manual-expense.sql` po utworzeniu artifacts i przywróceniu narzędzia EF.
- Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual Verification:

- Użytkownik potwierdził addytywną migrację, zachowanie kont i brak automatycznie zgadywanej strefy.

**Implementation Note**: Po automatycznej weryfikacji uzyskać potwierdzenie ręcznego kryterium przed przejściem dalej. To test modelu i przegląd SQL; odbiór migracji na PostgreSQL pozostaje w fazie 4.

---

## Phase 2: API i zgodność podsumowań

### Overview

Udostępnić ustawienia, katalog, zapis i odczyt zgodny z rzeczywistym rejestrem. Sprawdzić prywatność danych, grosze, daty oraz inicjalizację istniejących i nowych kont.

### Changes Required:

#### 1. Strefa, inicjalizacja i walidacja kwot

**Files**: `backend/PennyPath.Api/Spending/SpendingSettingsService.cs`, `InitialExpenseCategories.cs`, `SpendingPeriod.cs`, `ExpenseAmount.cs`, `SpendingContracts.cs`, `Program.cs`.

**Intent**: Oddzielić reguły finansowe i okres od kontrolerów oraz zapewnić deterministyczne dane startowe.

**Contract**: Rozpoznawać strefy IANA/UTC przez systemowe TimeZoneInfo; lista wyboru pochodzi z serwera i obejmuje UTC. Obsłużyć alias zgłoszony przez przeglądarkę, jeśli runtime go rozpoznaje; błędna strefa jest błędem pola, nie cichym przejściem na UTC. TimeProvider z S-01 określa dzisiaj po konwersji do zapisanej strefy; miesiąc ma granice DateOnly [start,end). Wszystkie PurchaseDate pozostają literalnymi datami.

Pierwszy POST settings tworzy profil i pełny zatwierdzony katalog w jednej transakcji, z unikalnością kluczy i obsługą wyścigu dwóch inicjalizacji. Ponowienie nie dubluje katalogu; późniejsza zmiana strefy zachowuje wszystkie kategorie i wydatki. ExpenseAmount realizuje dokładny kontrakt tekstu, obcięcia i dodatniej wartości. Użyć istniejącego DI i zegara; nie dodawać możliwości sterowania zegarem produkcji przez HTTP.

#### 2. Endpointy i bezpieczne błędy

**Files**: `backend/PennyPath.Api/Controllers/SpendingSettingsController.cs`, `ExpenseCategoriesController.cs`, `ExpensesController.cs`, `SpendingOverviewController.cs`, `Auth/AuthConfiguration.cs`, `Auth/AuthContracts.cs`, `PennyPath.Api.http`.

**Intent**: Zrealizować uzgodniony HTTP bez osłabienia sesji, CSRF ani walidacji kont.

**Contract**: Ścieżki, statusy i DTO z tabeli HTTP. Każdy kontroler stosuje auth, HTTPS/CSRF i no-store. ID użytkownika pobierać serwerowo; każda walidacja kategorii i każde zapytanie zawiera właściciela. Rozszerzyć mapowanie ModelState o pola S-02, zachowując format dotychczasowych błędów auth i ukrywanie nieznanych szczegółów pod `body`. POST zwraca dopiero zapisane dane; błąd nie udaje sukcesu. Przykłady HTTP pokazują pobranie świeżego CSRF po login, ustawienie strefy, katalog, zapis i oba zakresy rejestru.

#### 3. Odczyt i agregacja

**Files**: `backend/PennyPath.Api/Spending/SpendingReadService.cs`, `SpendingContracts.cs`.

**Intent**: Dostarczyć rejestr i pełne podsumowanie miesiąca z jednego spójnego odczytu, bez obliczeń finansowych w Vue.

**Contract**: Własność, zakresy, sortowanie, stronicowanie i snapshot zgodne z opisem HTTP. Finansowa agregacja używa dokładnych typów; sumy kategorii równają się całości, a podkategorii sumie rodzica. Null podkategorii pozostaje osobną grupą. Wewnętrzna ścieżka odczytu akceptuje jawne granice DateOnly dla testów F-01; publiczny endpoint nadal udostępnia wyłącznie bieżący miesiąc i przełącznik historii. Nie dodawać publicznego endpointu testowego ani dowolnych okresów.

#### 4. Testy zapisu, reguł i izolacji

**Files**: `backend/PennyPath.Api.Tests/SpendingApiTests.cs`, `SpendingReconciliationTests.cs`, `SpendingRulesTests.cs`, `TestApplicationFactory.cs`, `TestTimeProvider.cs`, `PennyPath.Api.Tests.csproj`, `.dockerignore`, `Dockerfile`.

**Intent**: Powiązać istniejące oczekiwania F-01 z rzeczywistym zapisem i agregacją oraz chronić nowe wybory użytkownika.

**Contract**: Testy `Category=Spending` zapisują wszystkie sześć przypadków F-01 w relacyjnym magazynie, uruchamiają produkcyjną ścieżkę odczytu dla granic fixture i porównują IDs oraz każdą mapę/sumę bez tolerancji. Jeśli Expense.Id jest UUID, jawnie mapować identyfikatory fixture na wygenerowane ID; nie zmieniać oczekiwań. Testowe katalogi mogą zawierać food/staples, nie tylko zestaw produktu. Projekt kopiuje oryginalny JSON do wyjścia testów; Dockerfile kopiuje go do etapu SDK, a allowlist dopuszcza wyłącznie potrzebny plik i jego katalogi nadrzędne.

Dodatkowe testy obejmują brak podkategorii obok other; dodatnie kwoty i obcięcie z powyższych przykładów; walidację pól; obie strony granicy miesiąca/roku, luty przestępny i zmianę DST; jednoczesny czas UTC odpowiadający różnym miesiącom w dwóch strefach; przyszły zakup w/poza bieżącym miesiącem. Kontrolowany zegar pozostaje testowy. HTTP sprawdza anonimowe 401, HTTPS i CSRF, kategorię/podkategorię obcego konta, niezmienność właściciela, dwa osobne rejestry i sumy, wyścig pierwszej konfiguracji i zachowanie katalogu po jej zmianie. Dla ponad 50 wpisów sumować cały miesiąc, a nie stronę. Zmiana istniejącego mapowania błędów zachowuje wszystkie testy auth.

### Success Criteria:

#### Automated Verification:

- Kontrola referencyjna i jej testy nadal przechodzą: `python3 scripts/check-spending-reconciliation.py` oraz `python3 scripts/test-spending-reconciliation.py -v`.
- Testy API, reguł i sześciu przypadków przez zapis/agregację przechodzą: `dotnet test backend/PennyPath.slnx --filter Category=Spending`.
- Cały zestaw backendu przechodzi: `dotnet test backend/PennyPath.slnx`.
- Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual Verification:

- Użytkownik potwierdził kontrakt strefy konta, obcięcia 12.349 do 12.34, opcjonalnej podkategorii oraz przyszłych wydatków liczonych w miesiącu zakupu.

**Implementation Note**: Po automatycznej weryfikacji uzyskać potwierdzenie kryterium ręcznego. Nie oznaczać SQLite jako potwierdzenia prawdziwego providera; nie poszerzać CI o odłożone osobne kroki Python F-01.

---

## Phase 3: Przepływ w Vue

### Overview

Zastąpić miejsce na przyszłe funkcje działającymi ustawieniami i rejestrem z podsumowaniem. Zapewnić czytelność formularza, zakresów, stanów błędu oraz działania na małym ekranie.

### Changes Required:

#### 1. Wspólny klient i czas życia prywatnego stanu

**Files**: `frontend/src/api/http.ts`, `api/auth.ts`, `api/spending.ts`, `auth/session.ts`, `spending/state.ts`, `spending/amount.ts`.

**Intent**: Użyć jednej sesji i jednego mechanizmu CSRF dla wszystkich endpointów oraz zapobiec wyciekom stanu przy zmianie konta.

**Contract**: Jedna instancja transportu na AuthSession obsługuje względne `/api/*`, same-origin credentials, no-store, HTTPS, pamięciowy CSRF i bezpieczne ProblemDetails z errors. Adapter auth zachowuje dotychczasowe zachowania; API spending używa tej samej instancji. Login/logout/401 rotują lub unieważniają CSRF, a 401 wywołuje istniejącą obsługę sesji/routera. Błędy wydatków nie korzystają z tekstu o emailu i haśle.

Stan wydatków i profilu jest wyłącznie w pamięci, związany z userId i rewizją żądań. Logout, 401 i zmiana konta czyszczą formularz, katalog, rejestr, wykres i wybraną kategorię; anulowanie/rewizja ignoruje spóźnione odpowiedzi. Odpowiedzi wcześniejszego scope, strony lub strefy nie zastępują aktualnych. Błąd sieci/5xx nie wylogowuje. Nie ponawiać POST automatycznie. Formatter waluty nie używa obliczeń zmiennoprzecinkowych do zmiany kwoty tekstowej; geometria wykresu może używać przybliżeń.

#### 2. Pierwszy wybór i edycja strefy

**Files**: `frontend/src/components/spending/TimeZoneSettings.vue`, `views/AccountHomeView.vue`, `spending/state.ts`.

**Intent**: Umożliwić świadomy wybór strefy i korzystanie z tego samego ustawienia na wszystkich urządzeniach.

**Contract**: Po zalogowaniu pobrać settings; przy null pokazać wybór przed formularzem wydatków. Propozycja pochodzi z `Intl.DateTimeFormat().resolvedOptions().timeZone`; zapisać dopiero po zatwierdzeniu. Lista korzysta z identyfikatorów wspieranych przez serwer; jeśli wykryta strefa jest nieobsługiwana, wymagać jawnego wyboru zamiast zapisywać inny domyślny identyfikator. Istniejąca zapisana wartość wygrywa nad wykrytą na innym urządzeniu.

Proste ustawienie strefy jest później dostępne z `/app`. Po udanym zapisie odświeżyć dzisiaj i miesiąc z odpowiedzi serwera oraz rejestr/wykres; nie zmieniać literalnych dat zakupów ani wpisanej ręcznie daty formularza. Przy błędzie zachować ostatnie potwierdzone ustawienie. Rozróżnić ładowanie, brak konfiguracji i błąd; 5xx nie oznacza nowego konta. Po powrocie karty do aktywności odświeżyć dane okresu, żeby pozostawiona otwarta aplikacja nie utrwaliła poprzedniego miesiąca.

#### 3. Formularz i rejestr

**Files**: `frontend/src/components/spending/ExpenseForm.vue`, `ExpenseRegister.vue`, `views/AccountHomeView.vue`, `assets/main.css`.

**Intent**: Umożliwić ręczny zapis bez utraty danych i pokazać jego wynik w jednoznacznym zakresie.

**Contract**: Polskie etykiety, nazwa, tekstowa kwota z klawiaturą dziesiętną, DateOnly z domyślną datą serwera dla strefy konta, wymagana kategoria i opcjonalna podkategoria. Zmiana kategorii czyści niepasującą podkategorię. Ograniczenie precyzji obejmuje wpisywanie i wklejanie oraz oba separatory; błędy API przypisać do pól. Blokować ponowne wysłanie w toku, zachować wpisane dane po porażce i nie pokazywać fikcyjnego zapisu.

Po 201 pokazać potwierdzenie z kanoniczną kwotą, wyczyścić nazwę/kwotę i odświeżyć rejestr oraz podsumowanie. Gdy odświeżenie nie powiedzie się po potwierdzonym zapisie, komunikat mówi „zapisano, nie udało się odświeżyć” i pozwala ponowić wyłącznie GET. Przy utracie odpowiedzi POST komunikat mówi, że wynik jest nieznany i należy sprawdzić rejestr przed ponownym zapisem.

Domyślny scope=current, przełącznik „Cała historia”, stronicowanie poprzednia/następna oraz opis okresu i liczby wpisów. Nowy wydatek spoza bieżącego miesiąca dostaje potwierdzenie i wskazanie przełącznika historii; nie musi być widoczny w miesięcznej liście. Daty przyszłe nie są blokowane. Rozróżnić pusty miesiąc, pustą historię i błąd odczytu; błąd nie może wyglądać jak suma 0. Kontrolki i układ działają od 320 px, z klawiaturą i czytną kolejnością focus.

#### 4. Wykres i podkategorie

**Files**: `frontend/src/components/spending/SpendingOverview.vue`, `CategoryPieChart.vue`, `assets/main.css`.

**Intent**: Pokazać strukturę wydatków i umożliwić przejście od kategorii do jej podkategorii.

**Contract**: SVG rysuje wykres kołowy wyłącznie z summary serwera; dokładne wartości PLN i nazwy występują także w legendzie/listach. Wybór kategorii jest dostępny myszą i przez przyciski klawiaturą; nie polega wyłącznie na kolorze. Wyświetlić podkategorie, w tym null jako „Bez podkategorii”, oraz powrót do wszystkich kategorii. Obsłużyć jedną kategorię, wiele kategorii, 0 wpisów i brak wybranej kategorii po odświeżeniu. Nie rysować zerowego koła ani nie tworzyć kategorii bez danych. Opis wyjaśnia, że suma miesiąca obejmuje również przyszłe wpisane daty; przełączenie historii nie rozszerza wykresu.

#### 5. Testy UI i regresja konta

**Files**: `frontend/src/__tests__/spending.spec.ts`, `spending-api.spec.ts`, `spending-amount.spec.ts`, `auth.spec.ts`, `routing.spec.ts`.

**Intent**: Sprawdzić cały przepływ oraz granice stanu, których sam build nie potwierdzi.

**Contract**: Vitest/Vue Test Utils i istniejące mocki fetch. Pokryć konfigurację strefy z propozycją przeglądarki i zapisanym priorytetem konta, jej edycję, precyzję/type/paste, null podkategorii, przyszłą datę, zmianę zakresu/strony, wykres i drilldown. Zweryfikować 401, 400 pól, 5xx, podwójny submit, potwierdzony zapis z nieudanym GET, nieznany wynik POST, wylogowanie i opóźnioną odpowiedź starego konta/scope. Dostosować istniejące testy do dodatkowych GET i wielu przycisków, zachowując ich znaczenie zamiast osłabiać oczekiwania.

### Success Criteria:

#### Automated Verification:

- Testy Vue, klienta i pól kwoty przechodzą: `npm --prefix frontend run test:unit -- --run`.
- Kontrola typów aplikacji i testów przechodzi: `npm --prefix frontend run type-check`.
- Pełny build produkcyjny przechodzi: `npm --prefix frontend run build`.
- Regresja backendu nadal przechodzi: `dotnet test backend/PennyPath.slnx`.
- Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual Verification:

- Użytkownik potwierdził czytelność i klawiaturę na 320 px oraz desktopie, wybór/edycję strefy, formularz, historię i dostępny wybór kategorii z podkategoriami.

**Implementation Note**: Ręczny odbiór UI wymaga udostępnionego środowiska ze sprawnym API; można wykorzystać host Docker przeznaczony do fazy 4. Pełne kontrole wdrożeniowe i odbiór trwałości pozostają w fazie 4. Po zielonych kontrolach automatycznych uzyskać potwierdzenie kryterium ręcznego.

---

## Phase 4: Weryfikacja na PostgreSQL i HTTPS

### Overview

Sprawdzić nową funkcję przez gotowy obraz i prawdziwy provider oraz zachować istniejące gwarancje migracji, sesji, kluczy i rollbacku.

### Changes Required:

#### 1. Testy rzeczywistych wydatków

**Files**: `scripts/test-expenses.py`, `scripts/test-compose.sh`, `tests/fixtures/first-manual-expense/identity-baseline.sql`.

**Intent**: Wykryć różnice PostgreSQL/SQLite, błędy migracji i problemy trwałości, których nie ujawnią mocki przeglądarki.

**Contract**: Nowy skrypt Python korzysta wyłącznie z biblioteki standardowej, zaufanego HTTPS, jawnego CA, osobnych cookie jars i syntetycznych kont. Nie wyłącza weryfikacji TLS ani nie wypisuje haseł/cookie. Sprawdza brak dostępu anonimowego, CSRF POST ustawień i wydatków, dwie strefy konta, stały katalog po jego zmianie, dodatnie kwoty/obcięcie, null obok other, odrzucenie obcych przypisań, przyszłą datę oraz dokładne sumy obu kont. Daty testu wyprowadza z today/period zwróconych przez serwer, obejmując pierwszy dzień miesiąca, poprzedni dzień oraz pierwszy dzień kolejnego miesiąca. Przypadki groszowe, granice dat i suma poza pierwszą stroną muszą działać na PostgreSQL. Całe sześć oryginalnych fixture z jawnymi historycznymi okresami weryfikują testy .NET fazy 2; nie udawać przestawienia zegara produkcyjnego w HTTP.

Skrypt ma tryby `--state PATH` i `--resume PATH`: zachowuje wyłącznie syntetyczną sesję, ID i oczekiwane dane testowe w prywatnym pliku 0600, a resume potwierdza dokładnie te same wydatki, ustawienie i sumy bez ponownego tworzenia wpisów. Jest to ulotny artefakt kontroli, nie plik postępu zmiany. Brak automatycznego ponawiania zapisów.

Przed pierwszym wdrożeniem w izolowanym Compose odtworzyć schemat S-01 z baseline SQL wygenerowanego przypiętym EF wyłącznie do migracji `InitialIdentity`, łącznie z historią EF; nie pisać ręcznie alternatywnego schematu Identity. Wstawić syntetyczny rekord konta i sentinel, następnie uruchomić nowy migrator i potwierdzić ich zachowanie. Wymaga to zapisanej fixture baseline; nigdy nie stosować jej do istniejącej bazy operatora. Testy fazy 1/2 nadal weryfikują tworzenie pełnego nowego modelu; realny pierwszy start z pustym PostgreSQL również sprawdzić w odrębnym, jednorazowym testowym katalogu bazy, bez niszczenia upgrade fixture.

`test-compose.sh` uruchamia konfigurację kont i test wydatków dopiero po istniejącym pierwszym odtworzeniu app oraz `resume_session` (obecnie linie 168–169). Nowy proces aplikacji ma świeży limit prób logowania w pamięci. Test zapisuje state i wywołuje resume po późniejszych odtworzeniach app i PostgreSQL, aktualizacji, błędzie migratora i rollbacku. Nie zmieniać limitu produkcyjnego ani automatycznie ponawiać POST. Zachować wcześniejsze kontrole auth, sentinel, CA, non-root, mountów, sieci i nieudanego wdrożenia. Nowe zasoby są wyłącznie własnymi zasobami projektu testowego. Rollback testuje zachowanie danych między zgodnymi obrazami; nie deklarować, że wcześniejszy obraz S-01 obsługuje endpointy wydatków.

#### 2. Pakowanie i uruchomienie w obecnej bramce

**Files**: `Dockerfile`, `.dockerignore`, `.github/workflows/containers.yml`.

**Intent**: Potwierdzić, że artefakt wdrożeniowy zawiera działającą implementację i że kontrole blokują publikację obrazu.

**Contract**: Build ARM64 uruchamia pełne testy .NET z JSON F-01 i testy Vue w etapach konsumowanych przez runtime. Runtime nie zawiera testów, fixture SQL, prywatnego stanu ani sekretów. Istniejące verify uruchamia rozszerzony test Compose przed publish; ewentualna zmiana opisu kroku nie dodaje odłożonej automatyzacji skryptów F-01. Nie zmieniać warunku DEPLOY_ENABLED ani konfigurować rzeczywistego runnera.

#### 3. Instrukcje i odbiór

**Files**: `README.md`, `backend/AGENTS.md`, `frontend/AGENTS.md`, `context/foundation/tech-stack.md`, `context/deployment/deploy-plan.md`.

**Intent**: Umożliwić uruchomienie i odtworzenie testu oraz przekazać następnej zmianie rzeczywisty stan funkcji.

**Contract**: Opisać nowe komendy/filtry, zasady kwot, null podkategorii, strefę konta, przyszłe daty i oba zakresy listy. Uzupełnić lokalizacje modułów i kontrakt wspólnego klienta. Instrukcja wdrożenia zachowuje istniejący sekret/klucze/CA, wymaga kopii bazy i migracji przed aplikacją; nie cofa schematu przy rollbacku. Instrukcja odbioru na dwóch kontach obejmuje zmianę strefy na drugim urządzeniu i ponowne logowanie bez utraty danych. Nie publikować ani wdrażać automatycznie w ramach implementacji plików.

### Success Criteria:

#### Automated Verification:

- Pełny zestaw backendu przechodzi: `dotnet test backend/PennyPath.slnx`.
- Testy i pełny build Vue przechodzą: `npm --prefix frontend run test:unit -- --run` oraz `npm --prefix frontend run build`.
- Regresja wdrożenia i składnia Bash przechodzą: `python3 scripts/test-deploy.py`, `bash -n scripts/deploy.sh`, `bash -n scripts/test-compose.sh`.
- Gotowy obraz ARM64 z testami buduje się na hoście Docker: `docker build --platform linux/arm64 -t pennypath:ci .`.
- Rozszerzony test PostgreSQL, HTTPS, wydatków, upgrade i trwałości przechodzi na tym hoście: `bash scripts/test-compose.sh`.
- Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual Verification:

- Użytkownik potwierdził na dwóch kontach niezależne wydatki i sumy, strefę zapamiętaną na drugim urządzeniu, pustą podkategorię, przyszły zakup oraz przełączanie miesiąca i historii.
- Operator potwierdził zachowanie wcześniejszych kont, wydatków, ustawień, kluczy i CA po aktualizacji oraz czytelność instrukcji; wyników PostgreSQL/Dockera nie zastąpiono SQLite lub lokalnym buildem.

**Implementation Note**: Kontrole Dockera wymagają rzeczywistego hosta ARM64 z Compose, np. Maca M1 lub istniejącego CI. Jeżeli są niedostępne lokalnie, pozostają oczekujące do uruchomienia i uzyskania wyniku; nie oznaczać fazy jako ukończonej. Po testach uzyskać oba potwierdzenia ręczne.

---

## Testing Strategy

### Unit Tests:

- Dodatnia kwota i dokładne obcięcie: 12.349→12.34, 12.999→12.99, 0.019→0.01; 0.009→0.00 z odrzuceniem. Osobno zły format, minus/zero, limit wartości i długości. Bez polegania na niejawnych zaokrągleniach parsera.
- Okres z zegara i strefy, w tym UTC `2026-11-01T00:30:00Z`: Europe/Warsaw jest w listopadzie, America/Los_Angeles w październiku. Zmiana strefy zmienia bieżący okres, nigdy literalną PurchaseDate. Granice miesiąca/roku, luty przestępny i DST mają jawne oczekiwania.
- Vue: ograniczenie wpisywania/wklejania, opcjonalna podkategoria, wykres jednej/wielu kategorii, wybór klawiaturą, rozróżnienie zera i błędu oraz stan po zapisanym POST z nieudanym odczytem.

### Integration Tests:

- Sześć niezmienionych oczekiwań JSON przez rzeczywisty model relacyjny i produkcyjny odczyt; porównanie pełnego składu wpisów, każdej kategorii i podkategorii bez tolerancji. Dodatkowe przypadki null nie zastępują ani nie przepisują oryginalnych.
- HTTPS/WebApplicationFactory: dwa konta, brak właściciela od klienta, prywatność katalogu/rejestru/wykresu/ustawień, POST CSRF, inicjalizacja i wyścig dwóch pierwszych zapisów, zmiana strefy bez utraty danych, sumy ponad stroną rejestru.
- Prawdziwy PostgreSQL w izolowanym Compose: migracja pustej i wcześniejszej bazy, dokładne kwoty i mapowanie DateOnly, granice bieżącego miesiąca, przyszłe daty, kategorie/null, snapshot oraz trwałość po wymianie kontenerów i rollbacku zgodnego obrazu.
- W fazach dodających testy wykazać znaczenie kluczowego oczekiwania krótką kontrolowaną mutacją: np. włączyć endExclusive, pominąć owner predicate lub zamienić obcięcie na zaokrąglenie. Odpowiedni test ma stać się czerwony; przywrócić kod bez zmieniania oczekiwań.

### Manual Testing Steps:

1. Potwierdzić addytywną migrację bez modyfikacji kont i bez narzuconej strefy.
2. Na koncie A wybrać zaproponowaną strefę; na innym urządzeniu zobaczyć zapisany wybór bez ponownego pytania. Zmienić go w ustawieniach i sprawdzić zakres bieżącego miesiąca.
3. Dodać 0,10 PLN i 0,20 PLN oraz wydatek 0,01 PLN bez podkategorii; sprawdzić sumę 0,31 PLN i oddzielną grupę „Bez podkategorii”.
4. Dodać przyszły zakup w bieżącym miesiącu i kolejny w następnym. Pierwszy zwiększa bieżącą sumę, drugi jest widoczny w historii i nie zwiększa sumy bieżącej.
5. Przejść z wykresu kategorii do podkategorii klawiaturą, wrócić do całości i sprawdzić te same dokładne kwoty co w odpowiednich wpisach rejestru.
6. Przełączyć na B i sprawdzić brak danych A, następnie wrócić do A, potwierdzić zapis i trwałość po restarcie/aktualizacji. Sprawdzić układ 320 px/desktop i błędy bez fałszywego sukcesu.

## Performance Considerations

Początkowo do 10 użytkowników; brak dodatkowego cache, globalnego store i infrastruktury rozproszonej. Rejestr jest stronicowany po 50, maksymalnie 100 rekordów, z indeksem właściciel/data. Agregacja obejmuje cały miesiąc po filtrze właściciela i zakresu, nigdy całą bazę ani tylko stronę. Zachować spójność odpowiedzi odczytu przy konkurencyjnym zapisie, np. transakcją o odpowiedniej izolacji; SQLite w testach nie definiuje zachowania PostgreSQL. Nie dodawać nieuzgodnionego SLA ani benchmarków jako bramki MVP.

## Migration Notes

Migracja InitialSpending jest addytywna w obecnym kontekście. Wcześniejsze konta otrzymują profil i katalog przy pierwszym zatwierdzeniu strefy, a nie podczas migracji lub GET. Kolejne zmiany strefy nie tworzą nowych kategorii i nie modyfikują dat wydatków. Przed rzeczywistym wydaniem operator zachowuje kopię bazy i istniejące hasła/wolumeny/klucze/CA. Migrator działa przed wymianą app; rollback cofa obraz, pozostawia schemat oraz dane. Wcześniejszy zgodny S-01 może nie pokazywać wydatków, ale nie może ich usunąć; nie wykonywać automatycznego Down w celu przywrócenia obrazu.

Każda faza musi dawać pakowalny obraz. Faza 1 dodaje schemat bez aktywowania funkcji, faza 2 endpointy, faza 3 UI; bieżąca transakcja wdrożenia już obsługuje migrator. Plan nie wykonuje merge, publikacji ani wdrożenia na serwer operatora. Nie zmieniać automatycznie statusów innych zmian ani archiwum.

## References

- Tożsamość: [change.md](change.md); zakres S-02: [roadmap.md](../../foundation/roadmap.md); US-01, FR-001/002/004/006 i Business Logic: [prd.md](../../foundation/prd.md).
- [shape-notes.md](../../foundation/shape-notes.md), [tech-stack.md](../../foundation/tech-stack.md), ukończone plany [S-01](../private-account-access/plan.md) i [F-01](../spending-reconciliation-checks/plan.md).
- `backend/PennyPath.Api/{Program.cs,Data/PennyPathIdentityDbContext.cs,Data/IdentityMigrationRunner.cs,Auth/AuthConfiguration.cs,Auth/RequireHttpsForAuthFilter.cs,Controllers/AuthController.cs}`; `backend/PennyPath.Api.Tests/{TestApplicationFactory.cs,PersistenceTests.cs,TestTimeProvider.cs}`.
- `frontend/src/{api/auth.ts,auth/session.ts,router/index.ts,views/AccountHomeView.vue,__tests__/auth.spec.ts,__tests__/routing.spec.ts}`; `.dockerignore`, `Dockerfile`, `.github/workflows/containers.yml`, `scripts/{test-auth.py,test-compose.sh}`.
- `tests/fixtures/spending-reconciliation/{README.md,cases.json}`; `scripts/check-spending-reconciliation.py`; instrukcje `backend/AGENTS.md` i `frontend/AGENTS.md`.
- [PostgreSQL numeric: dokładność i zaokrąglanie przy skali](https://www.postgresql.org/docs/18/datatype-numeric.html); [Npgsql: DateOnly, timestamp i osobny identyfikator strefy](https://www.npgsql.org/doc/types/datetime.html).
- [TimeZoneInfo.FindSystemTimeZoneById](https://learn.microsoft.com/en-us/dotnet/api/system.timezoneinfo.findsystemtimezonebyid?view=net-10.0); [Intl.DateTimeFormat.resolvedOptions](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Intl/DateTimeFormat/resolvedOptions); [ograniczenia providera SQLite](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations).

## Progress

### Phase 1: Model danych i migracja

#### Automated

- [x] 1.1 Rozwiązanie kompiluje się: `dotnet build backend/PennyPath.slnx`.
- [x] 1.2 Testy trwałości danych wydatków przechodzą: `dotnet test backend/PennyPath.slnx --filter Category=SpendingPersistence`.
- [x] 1.3 Powstaje idempotentny SQL do przeglądu: `dotnet tool run dotnet-ef migrations script --idempotent --project backend/PennyPath.Api --output artifacts/first-manual-expense.sql` po utworzeniu artifacts i przywróceniu narzędzia EF.
- [x] 1.4 Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual

- [x] 1.5 Użytkownik potwierdził addytywną migrację, zachowanie kont i brak automatycznie zgadywanej strefy.

### Phase 2: API i zgodność podsumowań

#### Automated

- [ ] 2.1 Kontrola referencyjna i jej testy nadal przechodzą: `python3 scripts/check-spending-reconciliation.py` oraz `python3 scripts/test-spending-reconciliation.py -v`.
- [ ] 2.2 Testy API, reguł i sześciu przypadków przez zapis/agregację przechodzą: `dotnet test backend/PennyPath.slnx --filter Category=Spending`.
- [ ] 2.3 Cały zestaw backendu przechodzi: `dotnet test backend/PennyPath.slnx`.
- [ ] 2.4 Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual

- [ ] 2.5 Użytkownik potwierdził kontrakt strefy konta, obcięcia 12.349 do 12.34, opcjonalnej podkategorii oraz przyszłych wydatków liczonych w miesiącu zakupu.

### Phase 3: Przepływ w Vue

#### Automated

- [ ] 3.1 Testy Vue, klienta i pól kwoty przechodzą: `npm --prefix frontend run test:unit -- --run`.
- [ ] 3.2 Kontrola typów aplikacji i testów przechodzi: `npm --prefix frontend run type-check`.
- [ ] 3.3 Pełny build produkcyjny przechodzi: `npm --prefix frontend run build`.
- [ ] 3.4 Regresja backendu nadal przechodzi: `dotnet test backend/PennyPath.slnx`.
- [ ] 3.5 Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual

- [ ] 3.6 Użytkownik potwierdził czytelność i klawiaturę na 320 px oraz desktopie, wybór/edycję strefy, formularz, historię i dostępny wybór kategorii z podkategoriami.

### Phase 4: Weryfikacja na PostgreSQL i HTTPS

#### Automated

- [ ] 4.1 Pełny zestaw backendu przechodzi: `dotnet test backend/PennyPath.slnx`.
- [ ] 4.2 Testy i pełny build Vue przechodzą: `npm --prefix frontend run test:unit -- --run` oraz `npm --prefix frontend run build`.
- [ ] 4.3 Regresja wdrożenia i składnia Bash przechodzą: `python3 scripts/test-deploy.py`, `bash -n scripts/deploy.sh`, `bash -n scripts/test-compose.sh`.
- [ ] 4.4 Gotowy obraz ARM64 z testami buduje się na hoście Docker: `docker build --platform linux/arm64 -t pennypath:ci .`.
- [ ] 4.5 Rozszerzony test PostgreSQL, HTTPS, wydatków, upgrade i trwałości przechodzi na tym hoście: `bash scripts/test-compose.sh`.
- [ ] 4.6 Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual

- [ ] 4.7 Użytkownik potwierdził na dwóch kontach niezależne wydatki i sumy, strefę zapamiętaną na drugim urządzeniu, pustą podkategorię, przyszły zakup oraz przełączanie miesiąca i historii.
- [ ] 4.8 Operator potwierdził zachowanie wcześniejszych kont, wydatków, ustawień, kluczy i CA po aktualizacji oraz czytelność instrukcji; wyników PostgreSQL/Dockera nie zastąpiono SQLite lub lokalnym buildem.
