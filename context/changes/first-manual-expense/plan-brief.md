# Pierwszy ręczny wydatek i aktualne podsumowanie — Plan Brief

> Full plan: [plan.md](plan.md)

## What & Why

S-02 daje pierwszy działający przepływ wydatków: ręczny wpis, prywatny rejestr i zgodne z nim podsumowanie.
Wykres kategorii i podkategorie pokazują, na co trafiają pieniądze; przyszłe daty pozwalają uwzględniać znane wydatki z wyprzedzeniem.

## Starting Point

Gotowe są konta, sesje, Vue Router, PostgreSQL, HTTPS w LAN, migrator i kontrole wdrożenia.
F-01 dostarcza sześć przypadków referencyjnych. Nie ma jeszcze wydatków, katalogu kategorii, ustawień strefy ani agregacji.

## Desired End State

Po pierwszym logowaniu użytkownik wybiera strefę z propozycją przeglądarki; zapis obowiązuje na koncie i można go edytować.
Dodaje dodatni wydatek w PLN z datą, kategorią i opcjonalną podkategorią, widzi własny rejestr oraz miesięczny wykres z podkategoriami.
Domyślnie lista pokazuje bieżący miesiąc; przełącznik udostępnia całą historię. Podsumowanie zawsze pozostaje miesięczne.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Okres | Miesiąc kalendarzowy [pierwszy dzień, pierwszy dzień następnego miesiąca) | Prosty początek; porównanie i inny dzień startu są w S-03/S-04 |
| Strefa | Propozycja przeglądarki, jawny pierwszy wybór, zapis na koncie, późniejsza edycja | Ten sam okres na wszystkich urządzeniach |
| Kategorie | Jedzenie, Transport, Dom, Zdrowie, Rozrywka, Pozostałe; osobny zestaw konta | Zatwierdzony start bez czekania na zarządzanie kategoriami |
| Podkategoria | Opcjonalna; null widoczny jako „Bez podkategorii”, odrębnie od „inne” | Użytkownik może poprzestać na kategorii |
| Kwoty | Dodatnie, od 0,01 PLN; bez zera, zwrotów i przychodów | Jednoznaczny rejestr samych wydatków |
| Precyzja | Formularz blokuje ponad dwa miejsca; API obcina w dół przed zapisem | 12.349→12.34, 12.999→12.99, 0.009→0.00 i odrzucenie |
| Daty przyszłe | Dozwolone, liczone w miesiącu daty zakupu | Znane przyszłe zakupy uczestniczą w planowaniu wydatków |
| Historia | Bieżący miesiąc domyślnie, przełącznik wszystkich wpisów | Czytelny start i dostęp do starszych/przyszłych zakupów |

## Scope

**In scope:** ustawienie/edycja strefy, osobisty katalog sześciu kategorii, zapis, rejestr, dokładna agregacja, wykres i podkategorie, testy oraz addytywna migracja.
**Out of scope:** przychody i saldo, zwroty/ujemne kwoty, budżety, status opłacenia, porównanie okresów, inny dzień startu, edycja/usuwanie/podziały zakupów, konfiguracja kategorii i CSV.

## Architecture / Approach

Obecne API i kontekst EF rozszerzamy o prywatne dane, bez nowej usługi. Pierwszy zapis strefy atomowo inicjalizuje katalog także istniejącym kontom.
Serwer wyznacza miesiąc ze strefy i zegara, zapisuje DateOnly oraz dokładne kwoty, zwraca rejestr i pełną miesięczną agregację.
Vue współdzieli sesję/CSRF, przechowuje dane w pamięci i rysuje SVG z dostępną legendą; nie oblicza sum finansowych.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Model danych i migracja | Wydatki, kategorie, strefa, ograniczenia i testy modelu | Zachowanie kont i właściwe FK właściciela |
| 2. API i zgodność podsumowań | Ustawienia, zapis, rejestr i sześć przypadków przez rzeczywistą agregację | Obcięcie kwot, null i granice okresu |
| 3. Przepływ w Vue | Pierwszy wybór/edycja strefy, formularz, historia, wykres i podkategorie | Spóźnione dane innego konta oraz błędy po zapisie |
| 4. Weryfikacja na PostgreSQL i HTTPS | Gotowy obraz, upgrade, trwałość i odbiór dwóch kont | Różnice providera i rzeczywista migracja |

**Prerequisites:** ukończone F-01/S-01; SDK .NET i narzędzie EF przypięte w repo, Node zgodny z engines, Python; odbiór UI wymaga sprawnego API, faza 4 hosta ARM64 z Dockerem/Compose lub CI.
**Estimated effort:** orientacyjnie 5–8 sesji / 16–28 godzin wraz z weryfikacją; bez oczekiwania na host i odbiór.

## Open Risks & Assumptions

- SQLite nie dowodzi poprawności PostgreSQL; testy prawdziwej bazy i obrazu pozostają obowiązkową bramką fazy 4.
- Kolumna numeric sama zaokrągla; API musi wcześniej obciąć kwotę. Techniczny limit zapisu: 999999999999,99 PLN, suma pozostaje dokładna.
- Zmiana strefy zmienia bieżący miesiąc, nie zapisane daty. Suma obejmuje również przyszłe wpisy datowane w tym miesiącu.
- Powtórne ustawienie strefy nie dubluje kategorii; logout/401 czyści dane i unieważnia spóźnione odpowiedzi.
- Po potwierdzonym zapisie błąd odświeżenia ponawia tylko odczyt. Nieznany wynik POST wymaga sprawdzenia rejestru przed nowym zapisem.

## Success Criteria (Summary)

- Dwa konta mają niezależne ustawienia, katalogi i wydatki; wybór strefy zachowuje się na drugim urządzeniu.
- 0,10 + 0,20 + 0,01 daje dokładnie 0,31 PLN, null jest osobną grupą, a przyszłe daty trafiają do właściwego miesiąca.
- Rejestr, wykres i podkategorie zgadzają się dla tego samego okresu; dane przetrwają aktualizację i odbiór PostgreSQL/HTTPS.

## References

- [Pełne kontrakty, testy i Progress](plan.md), [roadmapa S-02](../../foundation/roadmap.md), [PRD](../../foundation/prd.md).
- `tests/fixtures/spending-reconciliation/{README.md,cases.json}`; [poprzednia zmiana S-01](../private-account-access/plan.md).
