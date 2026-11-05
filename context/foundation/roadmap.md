---
project: PennyPath
version: 1
status: draft
created: 2026-10-10
updated: 2026-10-10
prd_version: 1
main_goal: speed
top_blocker: decisions
milestone_id: private-expenses-with-csv
milestone_seq: 1
milestone_status: open
---

# Roadmap: PennyPath

> Źródło: `context/foundation/prd.md` (v1), uzupełnione wcześniejszymi ustaleniami i automatycznie zbadanym stanem kodu.
> Dokument rozwija się w miejscu; pełne zastąpienie wymaga zachowania poprzedniej wersji zgodnie z konwencją foundation.
> Elementy są uporządkowane według zależności. Tabela „At a glance” jest ich indeksem.

## Milestone

**M-1: Prywatny rejestr i podsumowanie wydatków z importem CSV** — Status: open

- **Intent:** Użytkownik rozumie własne wydatki dzięki ręcznym wpisom, kategoriom i porównaniu okresów, a następnie zasila ten sam rejestr zweryfikowanym CSV z kontrolą podejrzanych duplikatów. Pierwszy używalny rezultat powstaje w S-02 i jest rozwijany w S-03 oraz S-04.
- **Source materials:** `context/foundation/prd.md` (v1); uzupełniająco `context/foundation/shape-notes.md`, `context/foundation/tech-stack.md` i potwierdzona inwentaryzacja kodu.
- **Done when:** każdy F-NN i S-NN poniżej ma status `done`; oba główne kryteria sukcesu PRD są spełnione, podsumowania zgadzają się z rejestrem, a użytkownicy mają dostęp wyłącznie do własnych wydatków.
- **Scope anchors:** US-01, FR-001–FR-008; sekcje Success Criteria, Business Logic, Access Control oraz Non-Functional Requirements.
- **Sequencing rationale:** `speed` wybrano dla dostarczenia działającego MVP. Najpierw ręczny wpis i podsumowanie zgodnie z wcześniejszym ustaleniem w shape-notes; `decisions` wskazuje nierozstrzygnięte decyzje dostępu i CSV.
- **Investment focus:** backend i dane wymagają szczególnej uwagi ze względu na prywatność oraz zgodność sum. Frontend i infrastruktura pozostają proste; potrzebne elementy techniczne powstają przy pierwszym przepływie, który ich używa.

## Vision recap

Osoby podobne do twórcy mają trudność ze śledzeniem wydatków: pod koniec miesiąca odkrywają, że wydały więcej, niż planowały, i chcą zrozumieć, na co poszły pieniądze. Obecnie ręcznie przeglądają transakcje bankowe i dopasowują je do zamówień lub list zakupów, co jest trudne i pracochłonne. PennyPath dostarcza zestawienia według kategorii i podkategorii, pozwalające zobaczyć, które zakupy odpowiadają za wydatki i rozważyć zmianę nawyków.

## North star

Gwiazda przewodnia oznacza tutaj najmniejszy przepływ od działania użytkownika do widocznego wyniku, który pokazuje, że produkt pomaga rozumieć wydatki.

**S-02: Pierwszy ręczny wydatek i aktualne podsumowanie** — Realizuje US-01 i ręczną część pierwszego kryterium sukcesu; dlatego pojawia się bezpośrednio po warunkach koniecznych do prywatnego, sprawdzalnego zapisu.

Porównanie okresów w S-03 i ustawienie ich początku w S-04 domykają pierwsze kryterium sukcesu PRD. Zakończenie całego M-1 obejmuje także dalsze wymagania konieczne, w tym CSV.

## At a glance

| ID | Change ID | Outcome (user can …) | Prerequisites | PRD refs | Status |
| --- | --- | --- | --- | --- | --- |
| F-01 | spending-reconciliation-checks | (foundation) Istnieją minimalne przypadki referencyjne i sposób uruchomienia sprawdzenia zgodności rejestru i podsumowania dla kwot, dat zakupu oraz kategorii i podkategorii. | — | FR-002, FR-004, Business Logic, Non-Functional Requirements | ready |
| S-01 | private-account-access | Użytkownik może zalogować się adresem email i hasłem w przeglądarce oraz uzyskać dostęp do własnego obszaru, z kontrolą dostępu dla niezalogowanych osób. | — | FR-001, US-01, Access Control, Non-Functional Requirements | blocked |
| S-02 | first-manual-expense | Użytkownik może dodać wydatek z nazwą, kwotą w PLN, datą zakupu i kategorią/podkategorią z zestawu początkowego, zobaczyć go we własnym rejestrze i odświeżonym podsumowaniu bieżącego okresu. | F-01, S-01 | US-01, FR-001, FR-002, FR-004, FR-006, Business Logic, Non-Functional Requirements | proposed |
| S-03 | compare-spending-periods | Użytkownik może porównać wydatki bieżącego i poprzedniego okresu według kategorii oraz podkategorii, z sumami zgodnymi z rejestrem. | S-02 | FR-002, US-01, Business Logic, Non-Functional Requirements | proposed |
| S-04 | configure-spending-period | Użytkownik może ustawić dzień początku okresu od 1 do 28 i zobaczyć przeliczone zestawienia bieżącego oraz poprzedniego okresu według dat zakupu. | S-03 | FR-003, FR-002, Business Logic, Success Criteria | proposed |
| S-05 | edit-expense | Użytkownik może zmienić nazwę, kwotę, datę zakupu lub ręczne przypisanie kategorii/podkategorii własnego wydatku i zobaczyć zaktualizowany rejestr oraz podsumowanie. | S-02 | FR-004, US-01, Business Logic, Non-Functional Requirements | proposed |
| S-06 | delete-expense-with-confirmation | Użytkownik może usunąć własny wydatek po potwierdzeniu i zobaczyć rejestr oraz podsumowanie bez jego wkładu; anulowanie zachowuje wydatek. | S-02 | FR-004, Business Logic, Non-Functional Requirements | proposed |
| S-07 | split-mixed-purchase | Użytkownik może rozdzielić własny zakup między kategorie/podkategorie, zaakceptować podział tylko przy zgodności sum z pierwotną kwotą i zobaczyć każdą alokację w odpowiednim zestawieniu. | S-02, S-05, S-06 | FR-005, FR-002, FR-004, Business Logic, Non-Functional Requirements | proposed |
| S-08 | recover-account-password | Użytkownik może odzyskać dostęp do własnego konta po zapomnieniu hasła i ponownie korzystać ze swoich zapisanych wydatków. | S-01 | FR-001, Access Control | proposed |
| S-09 | configure-personal-categories | Użytkownik może rozbudować lub zmienić własny zestaw kategorii i podkategorii oraz używać go przy ręcznym przypisywaniu wydatków i odczycie podsumowania. | S-02, S-05 | FR-006, FR-004, FR-002, US-01 | proposed |
| S-10 | reassign-before-category-deletion | Użytkownik może usunąć własną kategorię lub podkategorię dopiero po przeniesieniu powiązanych wydatków i alokacji, a podsumowanie pokazuje ich nowe przypisanie. | S-07, S-09 | FR-006, FR-005, FR-002, Business Logic | proposed |
| S-11 | preview-bank-csv | Użytkownik może wczytać lub przeciągnąć bankowy CSV jednego uzgodnionego układu, obejrzeć rekordy oraz dopasować separator, separator dziesiętny i ustawienia zakończeń linii. | S-01 | FR-007 | blocked |
| S-12 | import-reviewed-bank-csv | Użytkownik może zaimportować zweryfikowane rekordy CSV po przejrzeniu podejrzanych duplikatów i zobaczyć je w prywatnym rejestrze oraz podsumowaniu; ręczne przypisanie kategorii korzysta z przepływu edycji wydatku. | S-02, S-05, S-11 | FR-008, FR-007, FR-004, US-01, Business Logic, Non-Functional Requirements | blocked |

`ready` oznacza gotowość do `/10x-plan`, `proposed` oznacza oczekiwanie na zależności, a `blocked` oznacza własne pytanie z `Block: yes`. Blokada warunku wstępnego nie dodaje automatycznie nowej niewiadomej do wszystkich elementów zależnych.

## Streams

Pomoc nawigacyjna grupuje elementy według wspólnego ciągu warunków wstępnych. Strzałki w kolumnie Chain pokazują proponowaną kolejność czytania; faktyczne zależności i możliwą równoległość określają pola Prerequisites oraz Parallel with.

| Stream | Theme | Chain | Note |
| --- | --- | --- | --- |
| A | Rejestr i zestawienia wydatków | `F-01` → `S-02` → `S-03` → `S-04` → `S-05` → `S-06` → `S-07` → `S-09` → `S-10` | Łączy się z B w S-02, dostarczając najpierw ręczny przepływ zgodnie z celem MVP. |
| B | Dostęp do prywatnego konta | `S-01` | Zapewnia warunek prywatnego użycia dla A, C i D. |
| C | Odzyskanie hasła | `S-08` | Korzysta z S-01 ze strumienia B, domykając wymaganie dostępu do konta. |
| D | Podgląd i import bankowy | `S-11` → `S-12` | Łączy dostęp z B (S-01) z rejestrem i edycją z A (S-02, S-05). |

## Baseline

Stan kodu na 2026-10-10, automatycznie zbadany i potwierdzony przez użytkownika. Wybrane technologie pochodzą z tech-stack.md; ich deklaracja jest odróżniona od faktycznie istniejących funkcji.

- **Frontend:** partial — zgodnie z tech-stack.md: Vue 3, TypeScript i Vite; starter i polecenia budowania istnieją (`frontend/package.json:6`, `frontend/src/App.vue`). Funkcji produktu i routingu jeszcze nie ma; instalacja zależności pozostaje czynnością uruchomienia pierwszego przekroju.
- **Backend / API:** partial — zgodnie z tech-stack.md: ASP.NET Core Web API; istnieją kontrolery i przykładowe API (`backend/PennyPath.Api/Program.cs:5`, `backend/PennyPath.Api/Controllers/WeatherForecastController.cs`). Obsługa wydatków jeszcze nie istnieje.
- **Data:** absent — zgodnie z tech-stack.md wybrano PostgreSQL, lecz integracja, model wydatków i migracje nie powstały (`backend/PennyPath.Api/PennyPath.Api.csproj:9`; potwierdzenie w `context/changes/bootstrap-verification/verification.md`).
- **Auth:** absent — brak dostawcy, logowania i wydawania lub weryfikacji sesji; samo wywołanie autoryzacji nie tworzy uwierzytelniania (`backend/PennyPath.Api/Program.cs:19`). Deklaracja auth w tech-stack.md opisuje zakres produktu.
- **Deploy / infra:** absent — zgodnie z tech-stack.md wskazano wstępnie Railway i GitHub Actions; konfiguracja hostingu, kontenerów i workflow CI/CD jeszcze nie istnieje. Szczegóły oraz koszty wymagają weryfikacji przy przygotowaniu wdrożenia.
- **Observability:** partial — domyślne logowanie startera istnieje (`backend/PennyPath.Api/appsettings.json:2`); brak instrumentacji zdarzeń produktu, śledzenia błędów i metryk.

Brak lessons.md, infrastructure.md i deploy-plan.md. Shape-notes nie zawiera sekcji Forward: technical-roadmap; zawiera jednak jawne ustalenie, że ręczny wpis i dashboard poprzedzają CSV. Brak istniejących funkcji produktu oznacza, że żaden element tej mapy nie jest już ukończony.

PRD nie nadaje identyfikatorów wymaganiom niefunkcjonalnym; odwołania do nich używają rzeczywistych nazw sekcji.

## Foundations

### F-01: Sprawdzalna zgodność rejestru i podsumowania

- **Outcome:** (foundation) Istnieją minimalne przypadki referencyjne i sposób uruchomienia sprawdzenia zgodności rejestru i podsumowania dla kwot, dat zakupu oraz kategorii i podkategorii.
- **Change ID:** spending-reconciliation-checks
- **PRD refs:** FR-002, FR-004, Business Logic, Non-Functional Requirements
- **Unlocks:** S-02, S-03, S-04, S-05, S-06; ścieżka sprawdzenia zgodności rejestru i podsumowania
- **Prerequisites:** —
- **Parallel with:** S-01, S-08, S-11
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Zgodność sum jest warunkiem dopuszczenia funkcji w PRD. Foundation ustanawia minimalne przypadki referencyjne; S-02 integruje rzeczywisty zapis i agregację, a kolejne przekroje rozszerzają weryfikację przy pierwszym użyciu nowych zachowań.
- **Status:** ready

## Slices

### S-01: Dostęp do własnego konta

- **Outcome:** Użytkownik może zalogować się adresem email i hasłem w przeglądarce oraz uzyskać dostęp do własnego obszaru, z kontrolą dostępu dla niezalogowanych osób.
- **Change ID:** private-account-access
- **PRD refs:** FR-001, US-01, Access Control, Non-Functional Requirements
- **Prerequisites:** —
- **Parallel with:** F-01
- **Blockers:** —
- **Unknowns:**
  - Jak użytkownicy uzyskują konto i co dzieje się przy wejściu bez logowania na chronioną stronę? — Owner: user. Block: yes.
- **Risk:** Pierwszy prywatny przepływ wymaga ustalenia sposobu uzyskania konta. Minimalna ścieżka uruchomienia w przeglądarce i kontrola tożsamości powstają tutaj; S-02 zastosuje tę tożsamość do rzeczywistych wydatków.
- **Status:** blocked

### S-02: Pierwszy ręczny wydatek i aktualne podsumowanie

- **Outcome:** Użytkownik może dodać wydatek z nazwą, kwotą w PLN, datą zakupu i kategorią/podkategorią z zestawu początkowego, zobaczyć go we własnym rejestrze i odświeżonym podsumowaniu bieżącego okresu.
- **Change ID:** first-manual-expense
- **PRD refs:** US-01, FR-001, FR-002, FR-004, FR-006, Business Logic, Non-Functional Requirements
- **Prerequisites:** F-01, S-01
- **Parallel with:** S-08, S-11
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Ten przepływ najwcześniej pokazuje wartość produktu. Zapis, przypisanie do właściciela, początkowe kategorie, wykres kategorii i przejście do podkategorii są integrowane razem.
- **Status:** proposed

### S-03: Porównanie wydatków między okresami

- **Outcome:** Użytkownik może porównać wydatki bieżącego i poprzedniego okresu według kategorii oraz podkategorii, z sumami zgodnymi z rejestrem.
- **Change ID:** compare-spending-periods
- **PRD refs:** FR-002, US-01, Business Logic, Non-Functional Requirements
- **Prerequisites:** S-02
- **Parallel with:** S-05, S-06, S-07, S-08, S-09, S-10, S-11, S-12
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Porównanie korzysta z rzeczywistych wydatków dostarczonych przez S-02; granice okresów i daty zakupu muszą przypisywać każdy wydatek do właściwego zestawienia.
- **Status:** proposed

### S-04: Własny dzień początku okresu

- **Outcome:** Użytkownik może ustawić dzień początku okresu od 1 do 28 i zobaczyć przeliczone zestawienia bieżącego oraz poprzedniego okresu według dat zakupu.
- **Change ID:** configure-spending-period
- **PRD refs:** FR-003, FR-002, Business Logic, Success Criteria
- **Prerequisites:** S-03
- **Parallel with:** S-05, S-06, S-07, S-08, S-09, S-10, S-11, S-12
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Zmiana granic okresu rozwija już działające porównanie; przypadki na granicy miesiąca wymagają zgodności z rejestrem. S-02, S-03 i S-04 razem spełniają pierwsze kryterium sukcesu PRD.
- **Status:** proposed

### S-05: Poprawienie zapisanego wydatku

- **Outcome:** Użytkownik może zmienić nazwę, kwotę, datę zakupu lub ręczne przypisanie kategorii/podkategorii własnego wydatku i zobaczyć zaktualizowany rejestr oraz podsumowanie.
- **Change ID:** edit-expense
- **PRD refs:** FR-004, US-01, Business Logic, Non-Functional Requirements
- **Prerequisites:** S-02
- **Parallel with:** S-03, S-04, S-06, S-08, S-11
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Korekta rozwija pierwszy zapisany wydatek; przeniesienie między kategoriami lub okresami musi usunąć stary wkład i uwzględnić nowy dokładnie raz.
- **Status:** proposed

### S-06: Potwierdzone usunięcie wydatku

- **Outcome:** Użytkownik może usunąć własny wydatek po potwierdzeniu i zobaczyć rejestr oraz podsumowanie bez jego wkładu; anulowanie zachowuje wydatek.
- **Change ID:** delete-expense-with-confirmation
- **PRD refs:** FR-004, Business Logic, Non-Functional Requirements
- **Prerequisites:** S-02
- **Parallel with:** S-03, S-04, S-05, S-08, S-09, S-11, S-12
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Potwierdzenie jest częścią wymagania, a spójne usunięcie wkładu do sum domyka zarządzanie wydatkami. Obsługa podziałów jest rozszerzana wraz z S-07.
- **Status:** proposed

### S-07: Podział mieszanego zakupu

- **Outcome:** Użytkownik może rozdzielić własny zakup między kategorie/podkategorie, zaakceptować podział tylko przy zgodności sum z pierwotną kwotą i zobaczyć każdą alokację w odpowiednim zestawieniu.
- **Change ID:** split-mixed-purchase
- **PRD refs:** FR-005, FR-002, FR-004, Business Logic, Non-Functional Requirements
- **Prerequisites:** S-02, S-05, S-06
- **Parallel with:** S-03, S-04, S-08, S-09, S-11, S-12
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Podział rozszerza działający cykl dodawania, poprawiania i usuwania wydatku; agregacja musi liczyć pierwotny zakup raz i zachować równość alokacji również po korekcie.
- **Status:** proposed

### S-08: Odzyskanie dostępu po zapomnieniu hasła

- **Outcome:** Użytkownik może odzyskać dostęp do własnego konta po zapomnieniu hasła i ponownie korzystać ze swoich zapisanych wydatków.
- **Change ID:** recover-account-password
- **PRD refs:** FR-001, Access Control
- **Prerequisites:** S-01
- **Parallel with:** F-01, S-02, S-03, S-04, S-05, S-06, S-07, S-09, S-10, S-11, S-12
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Odzyskanie hasła domyka dostęp do konta bez zmiany właściciela danych; wybory techniczne i konfigurację dostarczenia wiadomości rozstrzyga plan tego przekroju.
- **Status:** proposed

### S-09: Własne kategorie i podkategorie

- **Outcome:** Użytkownik może rozbudować lub zmienić własny zestaw kategorii i podkategorii oraz używać go przy ręcznym przypisywaniu wydatków i odczycie podsumowania.
- **Change ID:** configure-personal-categories
- **PRD refs:** FR-006, FR-004, FR-002, US-01
- **Prerequisites:** S-02, S-05
- **Parallel with:** S-03, S-04, S-06, S-07, S-08, S-11, S-12
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Konfiguracja rozwija zestaw początkowy z S-02; zmiana organizacji kategorii musi zachować przypisanie wydatków i zgodność podsumowania dla ich właściciela.
- **Status:** proposed

### S-10: Usunięcie używanej kategorii po przeniesieniu wydatków

- **Outcome:** Użytkownik może usunąć własną kategorię lub podkategorię dopiero po przeniesieniu powiązanych wydatków i alokacji, a podsumowanie pokazuje ich nowe przypisanie.
- **Change ID:** reassign-before-category-deletion
- **PRD refs:** FR-006, FR-005, FR-002, Business Logic
- **Prerequisites:** S-07, S-09
- **Parallel with:** S-03, S-04, S-08, S-11, S-12
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Przeniesienie obejmuje również alokacje mieszanych zakupów; dlatego bezpieczne usuwanie kategorii konsumuje już działające podziały oraz konfigurację kategorii.
- **Status:** proposed

### S-11: Podgląd jednego formatu bankowego CSV

- **Outcome:** Użytkownik może wczytać lub przeciągnąć bankowy CSV jednego uzgodnionego układu, obejrzeć rekordy oraz dopasować separator, separator dziesiętny i ustawienia zakończeń linii.
- **Change ID:** preview-bank-csv
- **PRD refs:** FR-007
- **Prerequisites:** S-01
- **Parallel with:** F-01, S-02, S-03, S-04, S-05, S-06, S-07, S-08, S-09, S-10
- **Blockers:** —
- **Unknowns:**
  - Który układ bankowego CSV będzie wspierany i jaki przykładowy plik określa ten układ? — Owner: user. Block: yes.
- **Risk:** Podgląd ujawnia interpretację pliku przed zmianą rejestru; ustalenie jednego rzeczywistego układu ogranicza zakres i odblokowuje walidację importu.
- **Status:** blocked

### S-12: Import z rozstrzygnięciem podejrzanych duplikatów

- **Outcome:** Użytkownik może zaimportować zweryfikowane rekordy CSV po przejrzeniu podejrzanych duplikatów i zobaczyć je w prywatnym rejestrze oraz podsumowaniu; ręczne przypisanie kategorii korzysta z przepływu edycji wydatku.
- **Change ID:** import-reviewed-bank-csv
- **PRD refs:** FR-008, FR-007, FR-004, US-01, Business Logic, Non-Functional Requirements
- **Prerequisites:** S-02, S-05, S-11
- **Parallel with:** S-03, S-04, S-06, S-07, S-08, S-09, S-10
- **Blockers:** —
- **Unknowns:**
  - Który uzgodniony układ CSV definiuje rekordy podlegające walidacji i kontroli duplikatów? — Owner: user. Block: yes.
- **Risk:** Import konsumuje sprawdzony podgląd i istniejący rejestr; podobieństwo daty, opisu i kwoty wskazuje podejrzenie do rozstrzygnięcia przez użytkownika, a zaakceptowane rekordy muszą wpłynąć na sumy dokładnie raz.
- **Status:** blocked

Pole „Parallel with” wymienia elementy bez zależności między nimi w żadnym kierunku. Równoległa realizacja jest możliwa po spełnieniu ich własnych warunków wstępnych i rozstrzygnięciu blokujących pytań.

## Backlog Handoff

| Roadmap ID | Change ID | Suggested issue title | Ready for `/10x-plan` | Notes |
| --- | --- | --- | --- | --- |
| F-01 | spending-reconciliation-checks | Sprawdzalna zgodność rejestru i podsumowania | yes | `/10x-plan spending-reconciliation-checks` — odblokowuje weryfikację S-02 |
| S-01 | private-account-access | Dostęp do własnego konta | no | Rozstrzygnij pytanie 4 o dostęp do konta |
| S-02 | first-manual-expense | Pierwszy ręczny wydatek i aktualne podsumowanie | no | Najpierw ukończ: F-01, S-01 |
| S-03 | compare-spending-periods | Porównanie wydatków między okresami | no | Najpierw ukończ: S-02 |
| S-04 | configure-spending-period | Własny dzień początku okresu | no | Najpierw ukończ: S-03 |
| S-05 | edit-expense | Poprawienie zapisanego wydatku | no | Najpierw ukończ: S-02 |
| S-06 | delete-expense-with-confirmation | Potwierdzone usunięcie wydatku | no | Najpierw ukończ: S-02 |
| S-07 | split-mixed-purchase | Podział mieszanego zakupu | no | Najpierw ukończ: S-02, S-05, S-06 |
| S-08 | recover-account-password | Odzyskanie dostępu po zapomnieniu hasła | no | Najpierw ukończ: S-01 |
| S-09 | configure-personal-categories | Własne kategorie i podkategorie | no | Najpierw ukończ: S-02, S-05 |
| S-10 | reassign-before-category-deletion | Usunięcie używanej kategorii po przeniesieniu wydatków | no | Najpierw ukończ: S-07, S-09 |
| S-11 | preview-bank-csv | Podgląd jednego formatu bankowego CSV | no | Rozstrzygnij pytanie 1 o układ CSV |
| S-12 | import-reviewed-bank-csv | Import z rozstrzygnięciem podejrzanych duplikatów | no | Rozstrzygnij pytanie 1 o układ CSV |

Każdy Change ID jest stabilnym identyfikatorem zmiany. Foldery zmian i szczegóły wykonania powstają dopiero w dalszym planowaniu.

## Open Roadmap Questions

Pytania PRD poniżej zachowano dosłownie; dopisane pola Block wskazują ich wpływ na tę mapę. Pytania o skalę pozostają otwarte i nie stanowią blokady początkowego użycia przez osoby wskazane w wizji.

1. **Which bank CSV layout will the MVP support?** — Owner: user. Resolve before implementing CSV import. Block: S-11, S-12.
2. **What traffic estimate should target_scale.qps capture?** — Owner: user. Not specified in the input. Block: —; nie blokuje początkowych przepływów, pozostaje do uzupełnienia przed decyzjami o skali infrastruktury.
3. **What data-volume estimate should target_scale.data_volume capture?** — Owner: user. Not specified in the input. Block: —; nie blokuje początkowych przepływów, pozostaje do uzupełnienia przed decyzjami o pojemności infrastruktury.
4. **How do users register, and what happens when an unauthenticated user accesses a gated route?** — Owner: user. Resolve before implementing account access. Block: S-01; pośrednio wszystkie S-02–S-12 przez dostęp do konta.

## Parked

- **Kategoryzacja wspomagana AI i odczyt paragonów, zdjęć oraz PDF** — Why parked: PRD §Non-Goals odkłada je na później; MVP korzysta z ręcznej kategoryzacji.
- **Natywne aplikacje iOS i Android** — Why parked: PRD §Non-Goals określa MVP jako produkt przeglądarkowy.
- **Dedykowane budżety i prognozy oszczędności** — Why parked: PRD §Non-Goals pozostawia planowanie oszczędności użytkownikowi na podstawie zestawień.
- **Bezpośrednie połączenia z bankami** — Why parked: PRD §Non-Goals wybiera ręczny wpis i CSV jako sposoby wprowadzania wydatków.
- **Wspólne konta gospodarstwa domowego** — Why parked: PRD §Non-Goals wskazuje prywatne wydatki pojedynczych osób.
- **Wiele walut i przewalutowanie** — Why parked: PRD §Non-Goals ogranicza MVP do PLN.
- **Gwarantowana obsługa offline** — Why parked: PRD §Non-Goals odkłada ją poza bieżące zobowiązania produktu.
- **Dowolne układy bankowych CSV** — Why parked: PRD §Non-Goals ogranicza import do jednego uzgodnionego układu z opisanymi ustawieniami odczytu.

## Milestone History

## Done
