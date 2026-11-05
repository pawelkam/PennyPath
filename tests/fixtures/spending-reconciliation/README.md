# Przypadki referencyjne zgodności wydatków

`cases.json` zawiera wyłącznie dane syntetyczne dla F-01. Oczekiwane wyniki są zapisane jawnie na podstawie zatwierdzonego planu; skrypt kontrolny ich nie generuje. Poprawny wynik oznacza spójność wzorca referencyjnego. Integracja rzeczywistego rejestru i podsumowania aplikacji należy do S-02.

## Format version 1

Dokument ma `schema_version: 1`, `currency: "PLN"` i niepustą listę `cases`. Każdy przypadek zawiera unikalne `id`, opis `description`, `period`, listę `expenses` i `expected`.

| Pole | Znaczenie |
| --- | --- |
| `period.start_inclusive` | Pierwsza włączona data zakupu, ISO `YYYY-MM-DD` |
| `period.end_exclusive` | Pierwsza wyłączona data zakupu; późniejsza od początku |
| `expenses[].id` | Niepusty identyfikator wydatku, unikalny w przypadku |
| `expenses[].amount` | Dodatnia kwota PLN jako tekst z kropką i dwoma miejscami dziesiętnymi |
| `expenses[].purchase_date` | Data decydująca o przynależności do okresu |
| `expenses[].recorded_date` | Data wpisu, poprawna data ISO; nie wpływa na wybór okresu |
| `expenses[].category_id` | Niepusty identyfikator kategorii |
| `expenses[].subcategory_id` | Niepusty identyfikator podkategorii w obrębie danej kategorii |
| `expected.expense_ids` | Identyfikatory uwzględnionych wydatków bez powtórzeń; kolejność nie ma znaczenia |
| `expected.total` | Suma wszystkich uwzględnionych kwot |
| `expected.categories` | Mapa identyfikator kategorii → suma |
| `expected.subcategories` | Mapa identyfikator kategorii → mapa identyfikator podkategorii → suma |

Kwoty i sumy mają postać np. `"0.10"`, a nie liczby JSON. Sumowanie jest dziesiętne, dokładne i nie używa tolerancji. Sumy mogą być zerowe. Pusty wynik ma `expense_ids: []`, `total: "0.00"` i dwie puste mapy. Ta konwencja dotyczy danych referencyjnych, nie wyglądu przyszłego wykresu.

Każdy wydatek spełniający `start_inclusive <= purchase_date < end_exclusive` wnosi kwotę dokładnie raz do sumy ogólnej, swojej kategorii oraz podkategorii. Klucze map i rekordy mogą mieć dowolną kolejność. Podkategorie `food/other` i `transport/other` są odrębnymi grupami.

## Ręczne wyprowadzenie oczekiwań

| Przypadek | Uwzględnione wpisy | Wyliczenie i oczekiwane grupy |
| --- | --- | --- |
| `empty-register` | Brak | Suma `0.00`, mapy puste |
| `exact-cents` | `c1`, `c2`, `c3` | `0.10 + 0.20 + 0.01 = 0.31`; food `0.31`, groceries `0.30`, drinks `0.01` |
| `category-hierarchy` | `h1`, `h2`, `h3` | `15.00 + 20.00 + 5.00 = 40.00`; food `20.00` (other `15.00`, staples `5.00`), transport `20.00` (other `20.00`) |
| `purchase-date-boundaries` | Tylko `b1`, `b2` | `10.00 + 20.00 = 30.00`, całość food/groceries; `b0` zakupiono przed styczniem, `b3` już w lutym; daty wpisu nie zmieniają wyboru |
| `calendar-year-boundary` | Tylko `y1`, `y2` | `1.25 + 2.75 = 4.00`, całość food/groceries; listopad i 1 stycznia poza okresem grudniowym |
| `distinct-register-rows` | `r1`, `r2` | `0.50 + 0.50 = 1.00`, całość food/drinks; podobieństwo daty i kwoty nie usuwa odrębnego wpisu |

W każdym przypadku suma kategorii równa się sumie ogólnej, a suma podkategorii danej kategorii równa się sumie tej kategorii. Kontrola porównuje również dokładny skład wpisów i map; poprawna suma ogólna nie ukrywa błędnego przypisania.

## Uruchomienie kontroli

Z katalogu głównego repozytorium, Python 3.9+ bez dodatkowych pakietów:

```sh
python3 scripts/check-spending-reconciliation.py
python3 scripts/test-spending-reconciliation.py -v
```

Kontrola może też otrzymać ścieżkę do kopii JSON jako argument. Bez argumentu rozwiązuje plik względem lokalizacji skryptu, więc działa również przy innym katalogu roboczym. Wynik wskazuje ID każdego przypadku i ewentualne różnice oczekiwanej oraz obliczonej wartości.

Kody wyjścia kontroli: `0` — wszystkie przypadki zgodne; `1` — rozbieżność; `2` — błąd odczytu lub formatu danych. Błędy odczytu/formatu są wypisywane na stderr. Kontrola nie zmienia pliku wejściowego i nie uruchamia aplikacji, bazy ani usług sieciowych.

## Granice zestawu

Przykłady otrzymują jawne granice okresów i pełne przypisania. Nie ustalają walidacji formularza produktu, zasad zwrotów, kwot ujemnych, zaokrąglania ani prezentacji nieprzypisanych kategorii. Nie obliczają bieżącego okresu z zegara lub ustawień. Podziały zakupów, operacje na wydatkach i import CSV zostaną objęte dodatkowymi przykładami w odpowiednich przekrojach roadmapy.
