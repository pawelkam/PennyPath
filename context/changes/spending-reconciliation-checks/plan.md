# Sprawdzalna zgodność rejestru i podsumowania — Implementation Plan

## Overview

F-01 ustanawia minimalny, wykonywalny wzorzec zgodności rejestru i podsumowania: syntetyczne dane JSON, niezależnie ustalone oczekiwane wyniki oraz kontrolę kwot, dat zakupu, kategorii i podkategorii. Użytkownik zatwierdził wariant referencyjny i złożoność LOW. Dnia 2026-11-05, po ukończeniu fazy 1 w commicie `75de4f3`, przeniósł dawną fazę 2 poza MVP. Aktywny zakres tej zmiany obejmuje wyłącznie ukończoną fazę 1. Wynik będzie podstawą dla integracji rzeczywistego zapisu i agregacji w S-02.

## Current State Analysis

- `backend/PennyPath.slnx:2` zawiera tylko API; projekt `backend/PennyPath.Api/PennyPath.Api.csproj:4` używa .NET 10. Nie ma jeszcze modelu wydatków, agregacji ani projektu testowego. `backend/PennyPath.Api/Program.cs:23` mapuje kontrolery startera.
- `scripts/test-deploy.py:51` ustanawia istniejący wzorzec testów Python `unittest`, z izolowanymi plikami tymczasowymi. `scripts/smoke-test.py:22` sprawdza opublikowany starter, nie zgodność podsumowań wydatków.
- `.github/workflows/containers.yml:19` ma zadanie `verify`: testy wdrożeniowe, budowanie ARM64 i testy Compose. `publish` zależy od niego (`:44`). Jest już miejsce na szybką kontrolę referencyjną przed budowaniem obrazu.
- `README.md:20` opisuje lokalne sprawdzenia. `Dockerfile:11` kopiuje tylko projekt API; wariant referencyjny nie wymaga zmiany budowania obrazu ani dodawania projektu .NET.
- PRD ustala PLN, przypisanie według daty zakupu i zgodność sum kategorii/podkategorii. Roadmapa F-01 oddziela wzorzec referencyjny od integracji w S-02. Jej historyczny Baseline nie uwzględnia istniejącego obecnie CI i wdrożenia; plan opiera się na faktycznych plikach.
- Przy rozpoczęciu planowania w folderze zmiany istniał `change.md` ze statusem `new`. Brak `frame.md`, `research.md`, `context/foundation/lessons.md` i `docs/reference/contract-surfaces.md`; nie są wymagane do tej zmiany.
- Lokalnie dostępny jest Python 3.9.6. Narzędzie `actionlint` nie jest dostępne, więc nie jest lokalną bramką tego planu.

## Desired End State

Z katalogu głównego repozytorium można uruchomić `python3 scripts/check-spending-reconciliation.py` i otrzymać wynik każdego z sześciu przypadków. Sukces oznacza zgodność danych referencyjnych z jawnie zapisanymi oczekiwaniami; rozbieżność daje niezerowy kod wyjścia i wskazuje przypadek oraz pole. Testy wykazują wykrywanie błędnej sumy, granicy dat i przypisania również wtedy, gdy suma ogólna nadal się zgadza.

JSON i opis formatu pozwalają S-02 wykorzystać ten sam zestaw oczekiwań w testach rzeczywistego zapisu i agregacji .NET. Kontrola jest uruchamiana lokalnie; podłączenie jej do `verify` i rozszerzenie instrukcji przekazania odłożono poza MVP. Zielony F-01 potwierdza spójność wzorca, a nie gotowość nieistniejącej jeszcze funkcji aplikacji.

### Key Discoveries:

- Foundation nie potrzebuje serwera, bazy, logowania ani projektu testowego .NET: jego kontraktem jest przenośny zestaw przykładów (`context/foundation/roadmap.md:92`).
- Python jest już częścią lokalnej i CI-owej weryfikacji (`README.md:31`, `.github/workflows/containers.yml:29`). Biblioteka standardowa wystarcza do JSON, dat, dokładnych sum dziesiętnych i testów.
- Kwoty należy tworzyć z tekstu przez `Decimal`, bez przechodzenia przez `float`; dokumentacja modułu wyjaśnia znaczenie dokładnych porównań kwot.
- Sama równość sum ogólnych jest niewystarczająca: kontrola musi porównywać także skład rejestru i każdy poziom przypisania.

## What We're NOT Doing

- Kod agregacji używany przez API, modele domenowe .NET, endpointy, migracje PostgreSQL i interfejs użytkownika.
- Uwierzytelnianie i dowodzenie izolacji danych użytkowników; należą do S-01 i integracji S-02.
- Obliczanie bieżącego/poprzedniego okresu na podstawie zegara i konfiguracji dnia 1–28; przykłady otrzymują jawne granice, a S-03/S-04 rozszerzą zachowanie.
- Operacje dodawania, edycji i usuwania w aplikacji, podziały zakupów, konfiguracja kategorii, import CSV i rozstrzyganie duplikatów. Kolejne przekroje dodadzą przypadki przy pierwszym użyciu tych zachowań.
- Decyzje produktowe o zwrotach, kwotach ujemnych/zerowych, zaokrąglaniu nadmiarowych miejsc dziesiętnych i nieprzypisanych kategoriach. Przykłady obejmują poprawne dodatnie kwoty PLN i pełne przypisania; kontrola formatu fixture nie staje się walidacją formularza produktu.
- Projekt ogólnego silnika reguł, generator oczekiwanych wyników, dodatkowe zależności Python, próg pokrycia, benchmarki, publikacja obrazu i wdrożenie.
- Aktualizacja historycznego Baseline roadmapy poza synchronizacją statusu F-01.
- Dawna faza 2: automatyczne uruchamianie nowych kontroli w CI oraz rozszerzona dokumentacja w głównym README i instrukcji przekazania — przeniesione poza MVP decyzją użytkownika z 2026-11-05.

## Deferred After MVP

Dawna faza 2 „CI i przekazanie do S-02” została usunięta z aktywnego zakresu i `Progress`. Zakres do podjęcia po MVP zapisano również w `context/foundation/roadmap.md`, sekcja Parked:

- Dodać w `.github/workflows/containers.yml` do zadania `verify`, po checkout i przed budowaniem obrazu, obie komendy: `python3 scripts/check-spending-reconciliation.py` i `python3 scripts/test-spending-reconciliation.py -v`, z zachowaniem propagacji błędów i zależności `publish: needs: verify`.
- Rozszerzyć główny `README.md` o obie komendy, Python 3.9+ i link do danych. Uzupełnić `tests/fixtures/spending-reconciliation/README.md` o przykład błędu na kopii pliku i szczegółową instrukcję ponownego wykorzystania JSON w testach .NET.
- Przy podjęciu prac utworzyć osobną zmianę i plan zgodny z aktualnym CI. Źródłowy kontrakt i kryteria dawnej fazy 2 są zachowane w historii Git w commicie `75de4f3`.

Odłożenie dotyczy automatyzacji i rozszerzonej dokumentacji. S-02 nadal integruje rzeczywisty zapis i agregację oraz weryfikuje zgodność z rejestrem w ramach MVP.

## Implementation Approach

Zestaw `tests/fixtures/spending-reconciliation/cases.json` jest wspólnym źródłem przykładów dla obecnego skryptu i przyszłych testów .NET. Każde oczekiwanie jest zapisane jawnie i sprawdzalne ręcznie. Skrypt w `scripts/` jest narzędziem referencyjnym, nie zależnością aplikacji. Testy uruchamiają go również na zmienionych kopiach danych, aby potwierdzić, że błędy nie przechodzą jako sukces.

Format version 1 ma następujący kontrakt:

| Element | Pola i niezmienniki |
| --- | --- |
| Dokument | `schema_version: 1`, `currency: "PLN"`, niepusta lista `cases`; unikalne identyfikatory przypadków |
| Przypadek | `id`, `description`, `period`, `expenses`, `expected` |
| Okres | `start_inclusive`, `end_exclusive`: poprawne daty ISO `YYYY-MM-DD`, początek wcześniejszy niż koniec |
| Wydatek | `id`, `amount`, `purchase_date`, `recorded_date`, `category_id`, `subcategory_id`; identyfikatory wydatków unikalne w przypadku |
| Kwota | Tekst dziesiętny z kropką i dokładnie dwoma miejscami, np. `"0.10"`; dodatnie kwoty wejściowe, nieujemne sumy; żadnych liczb zmiennoprzecinkowych JSON ani tolerancji porównania |
| Oczekiwanie | `expense_ids`: lista bez duplikatów, porównywana jako zbiór; `total`: kwota; `categories`: mapa category_id → kwota; `subcategories`: mapa category_id → mapa subcategory_id → kwota |
| Pusty wynik | `expense_ids: []`, `total: "0.00"`, obie mapy puste; jest to konwencja zestawu referencyjnego, nie decyzja o prezentacji pustego wykresu |

Przynależność do okresu określa wyłącznie `start_inclusive <= purchase_date < end_exclusive`. `recorded_date` jest danymi kontrolnymi, aby wykryć pomylenie daty zakupu z datą wpisu. Kategorie i podkategorie mają identyfikatory, a identyfikator podkategorii jest rozpatrywany w obrębie kategorii. Porównanie nie nadaje znaczenia kolejności rekordów ani kluczy map.

Minimalne przypadki, z wartościami ustalonymi przed implementacją:

| ID | Rejestr i okres | Jawny wynik |
| --- | --- | --- |
| `empty-register` | Brak wydatków, okres `[2026-01-01, 2026-02-01)` | Pusty wynik, `0.00` |
| `exact-cents` | W styczniu: `c1` = `0.10`, `c2` = `0.20` w `food/groceries`; `c3` = `0.01` w `food/drinks` | IDs `c1,c2,c3`; razem i food `0.31`; groceries `0.30`, drinks `0.01` |
| `category-hierarchy` | W styczniu: `h1` = `15.00` w `food/other`, `h2` = `20.00` w `transport/other`, `h3` = `5.00` w `food/staples` | IDs `h1,h2,h3`; razem `40.00`; food `20.00`, transport `20.00`; food/other `15.00`, food/staples `5.00`, transport/other `20.00` |
| `purchase-date-boundaries` | Okres styczniowy; `b0` = `99.00`, zakup 2025-12-31, wpis 2026-01-02; `b1` = `10.00`, zakup 2026-01-01, wpis 2026-02-03; `b2` = `20.00`, zakup 2026-01-31, wpis 2026-02-02; `b3` = `88.00`, zakup 2026-02-01, wpis 2026-02-02; wszystkie food/groceries | Tylko IDs `b1,b2`; razem, food i food/groceries `30.00` |
| `calendar-year-boundary` | Okres `[2025-12-01, 2026-01-01)`; `y0` = `9.00` dnia 2025-11-30, `y1` = `1.25` dnia 2025-12-01, `y2` = `2.75` dnia 2025-12-31, `y3` = `8.00` dnia 2026-01-01; wszystkie food/groceries | Tylko IDs `y1,y2`; razem, food i food/groceries `4.00` |
| `distinct-register-rows` | `r1` i `r2` po `0.50`, oba zakupione 2026-01-15 w food/drinks, okres styczniowy | Oba IDs `r1,r2`; razem, food i food/drinks `1.00`; podobieństwo daty i kwoty nie usuwa wiersza |

Dla przypadków bez jawnej różnicy dat wpisu i zakupu `recorded_date` jest równe `purchase_date`. Daty styczniowe w `exact-cents` i `category-hierarchy` mogą być dowolnym stałym dniem stycznia 2026. Żaden przypadek nie zależy od bieżącego czasu.

## Phase 1: Przypadki referencyjne i wykonywalna kontrola

### Overview

Utworzyć sześć przypadków i kontrolę, która oblicza ich wkład według dat zakupu, a następnie porównuje wynik z niezależnie zapisanymi oczekiwaniami. Zapewnić czytelne wykrywanie rozbieżności i błędnych plików wejściowych.

### Changes Required:

#### 1. Zestaw danych i jego kontrakt

**Files**: `tests/fixtures/spending-reconciliation/cases.json`, `tests/fixtures/spending-reconciliation/README.md`

**Intent**: Ustanowić mały wzorzec, który można zweryfikować ręcznie i wykorzystać ponownie w S-02 bez zależności od Pythona.

**Contract**: JSON realizuje version 1 oraz wszystkie sześć przypadków i wyniki z tabeli powyżej. README opisuje pola, jednostkę PLN, granice okresu, zakres kategorii/podkategorii, ręczne wyprowadzenie sum i ograniczenie do danych syntetycznych. Oczekiwanych wyników nie wolno generować skryptem podlegającym kontroli.

#### 2. Wykonywalna kontrola

**File**: `scripts/check-spending-reconciliation.py`

**Intent**: Zapewnić jedno polecenie do sprawdzenia wzorca i dokładne wskazanie rozbieżności.

**Contract**: `python3 scripts/check-spending-reconciliation.py [fixture-path]`; brak argumentu wybiera repozytoryjny `cases.json` względem lokalizacji skryptu. Opcjonalna ścieżka służy sprawdzaniu kopii danych. Python 3.9+, wyłącznie biblioteka standardowa, `Decimal` tworzony z tekstu, daty bez stref czasowych. Skrypt sprawdza format i niepustą listę przypadków, porównuje wybrane IDs, sumę ogólną, komplet map kategorii i podkategorii oraz zgodność sum między poziomami. Kod `0` oznacza wszystkie przypadki zgodne, `1` rozbieżność oczekiwania, `2` błąd odczytu/formatu. Wynik zawiera ID każdego sprawdzonego przypadku; rozbieżności wskazują pole, wartość oczekiwaną i obliczoną. Błędy wejścia trafiają do stderr bez nieobsłużonego tracebacku. Kontrola nie modyfikuje danych i nie kontaktuje się z usługami.

#### 3. Testy użyteczności kontroli

**File**: `scripts/test-spending-reconciliation.py`

**Intent**: Wykazać, że zielony wynik oznacza sprawdzenie istotnych niezmienników, a uszkodzone dane lub oczekiwania są wykrywane.

**Contract**: `python3 scripts/test-spending-reconciliation.py -v`, wzorzec `unittest` i izolowane pliki tymczasowe. Testy obejmują poprawny komplet sześciu przypadków, zmianę sumy o grosz, zamianę kwot podkategorii przy niezmienionej sumie kategorii/ogólnej, zmianę przypisania kategorii przy niezmienionej sumie ogólnej, przesunięcie daty zakupu przez granicę przy zachowaniu daty wpisu, błędne oczekiwane IDs oraz brakujący plik, niepoprawny JSON i pusty zestaw. Testy CLI sprawdzają odpowiednio kody `0/1/2` i wskazanie przypadku/pola, nie dokładną interpunkcję komunikatów. Oczekiwania pozostają jawne; testy nie odtwarzają algorytmu agregacji.

### Success Criteria:

#### Automated Verification:

- Kontrola potwierdza wszystkie sześć przypadków: `python3 scripts/check-spending-reconciliation.py`.
- Testy kontroli i wykrywania rozbieżności przechodzą: `python3 scripts/test-spending-reconciliation.py -v`.
- Zmiany nie zawierają błędów whitespace: `git diff --check`.

#### Manual Verification:

- Użytkownik potwierdził ręczne wyliczenie sześciu przypadków z tabeli, w tym `0.31`, rozdzielenie obu podkategorii `other` oraz wyłącznie `b1,b2` w styczniu.

**Implementation Note**: Po automatycznej weryfikacji zatrzymać się na potwierdzenie ręcznej kontroli. W kontroli celowego zepsucia kodem pod testem jest skrypt referencyjny: tymczasowo dopuścić datę równą `end_exclusive`, uruchomić test poprawnego zestawu i potwierdzić czerwony wynik, następnie bezwarunkowo przywrócić skrypt. Nie zmieniać oczekiwań, aby przywrócić zielony wynik.

---

## Testing Strategy

### Unit Tests:

- Sześć ustalonych przypadków stanowi jawny wzorzec kwot, granic dat, składu rejestru i hierarchii grupowania.
- Negatywne przypadki na kopiach danych wykazują wykrycie błędów, których nie ujawnia sama suma ogólna, oraz poprawne rozróżnienie błędu danych i rozbieżności wyniku.
- Testy nie powielają obliczeń narzędzia. Zmiana kodu filtrowania dat musi zepsuć test poprawnego zestawu przy niezmienionych oczekiwaniach.

### Integration Tests:

- Testy procesu CLI uruchamiają faktyczny skrypt z plikiem domyślnym i plikami tymczasowymi; sprawdzają status procesu i przydatną diagnostykę.
- Testy referencyjne są uruchamiane lokalnie. Podłączenie tych samych komend do CI jest pracą po MVP. Testy wdrożeniowe pozostają osobnym sprawdzeniem infrastruktury.
- Integracja HTTP, bazy i rzeczywistego dashboardu jest poza F-01. Uruchomienie całego workflow w GitHub po publikacji zmiany jest późniejszym potwierdzeniem środowiska CI, a nie wynikiem lokalnych testów.

### Manual Testing Steps:

1. Po fazie 1 porównać dane JSON z tabelą sześciu przypadków i ręcznie potwierdzić wskazane kwoty i granice okresów.
2. Przeczytać opis danych jak osoba rozpoczynająca S-02: musi rozróżniać spójność wzorca od testu rzeczywistej funkcji.

## Performance Considerations

Stały zestaw sześciu małych przypadków działa w pamięci i nie uruchamia aplikacji, bazy ani kontenerów. Brak wymagań wydajnościowych produktu w tej zmianie; nie dodawać benchmarków ani mechanizmów cache.

## Migration Notes

Brak migracji i zmian danych aplikacji. Dodawane są wyłącznie syntetyczne pliki referencyjne, narzędzie, jego testy i opis danych. Format `schema_version: 1` dotyczy danych testowych, nie publicznego API ani schematu bazy. Wycofanie polega na cofnięciu tych plików; nie wymaga operacji na PostgreSQL. Krok workflow pozostaje pracą po MVP.

## References

- Tożsamość: `context/changes/spending-reconciliation-checks/change.md`.
- Roadmapa: `context/foundation/roadmap.md`, F-01 oraz granica integracji S-02.
- Wymagania: `context/foundation/prd.md`, FR-002, FR-004, Business Logic i Non-Functional Requirements.
- Potwierdzone decyzje: `context/foundation/shape-notes.md`; technologia: `context/foundation/tech-stack.md`.
- Wzorzec testów: `scripts/test-deploy.py:51`; istniejący CI: `.github/workflows/containers.yml:19`; instrukcje: `README.md:20`.
- [Python Decimal — dokładne kwoty i tworzenie z tekstu](https://docs.python.org/3/library/decimal.html).
- [Python unittest — testy i uruchamianie CLI](https://docs.python.org/3/library/unittest.html).

## Progress

### Phase 1: Przypadki referencyjne i wykonywalna kontrola

#### Automated

- [x] 1.1 Kontrola potwierdza wszystkie sześć przypadków: `python3 scripts/check-spending-reconciliation.py`. — 75de4f3
- [x] 1.2 Testy kontroli i wykrywania rozbieżności przechodzą: `python3 scripts/test-spending-reconciliation.py -v`. — 75de4f3
- [x] 1.3 Zmiany nie zawierają błędów whitespace: `git diff --check`. — 75de4f3

#### Manual

- [x] 1.4 Użytkownik potwierdził ręczne wyliczenie sześciu przypadków z tabeli, w tym `0.31`, rozdzielenie obu podkategorii `other` oraz wyłącznie `b1,b2` w styczniu. — 75de4f3
