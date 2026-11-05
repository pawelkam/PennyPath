# Sprawdzalna zgodność rejestru i podsumowania — Plan Brief

> Full plan: [plan.md](plan.md)

## What & Why

F-01 ustanawia sprawdzalny wzorzec zgodności rejestru i podsumowania dla kwot PLN, dat zakupu oraz kategorii i podkategorii.
Użytkownik wybrał wariant referencyjny. Po ukończeniu fazy 1 w `75de4f3` przeniósł dawną fazę 2 poza MVP (2026-11-05).
Wspólne przykłady pomogą weryfikować funkcję aplikacji w S-02 w ramach MVP.

## Starting Point

Istnieją starter API .NET 10, Vue oraz CI z testami wdrożeniowymi Python i Compose. Brak funkcji wydatków i testów agregacji.
Wykorzystujemy obecny wzorzec skryptów Python; nie trzeba dodawać projektu .NET, bibliotek ani zmieniać obrazu aplikacji.

## Desired End State

Jedno polecenie sprawdza sześć przypadków JSON i wskazuje rozbieżności kwot, dat i przypisań.
Kontrola i jej testy działają lokalnie. S-02 może wykorzystać te same dane i oczekiwania w testach .NET.
Podłączenie do CI i rozszerzona dokumentacja przekazania zostały odłożone po MVP.
Zielony wynik F-01 potwierdza spójność wzorca; rzeczywista funkcja aplikacji wymaga osobnych testów integracyjnych w S-02.

## Key Decisions Made

| Decision | Choice | Why |
| --- | --- | --- |
| Zakres | Wariant referencyjny, potwierdzony przez użytkownika | Rzeczywisty zapis i agregacja są integrowane w S-02 |
| Zakres MVP | Ukończona faza 1; dawna faza 2 poza MVP | Decyzja użytkownika z 2026-11-05 ogranicza bieżącą zmianę do lokalnego wzorca |
| Format | JSON version 1, jawne oczekiwane IDs i sumy | Te same przykłady mogą służyć Pythonowi i .NET |
| Narzędzie | Python 3.9+, standardowe `unittest` i `Decimal` | Pasuje do obecnych skryptów; nie wymaga instalowania pakietów |
| Kwoty | Tekst z dwoma miejscami dziesiętnymi, np. `"0.10"` | Dokładne porównania bez `float` i tolerancji |
| Daty | Jawny okres z włączonym początkiem i wyłączonym końcem, według daty zakupu | Wzorzec nie zależy od zegara ani daty wpisu |
| Hierarchia | Sumy kategorii i podkategorii w obrębie kategorii | Poprawna suma ogólna może ukrywać błędne przypisanie |

## Scope

**In scope:** sześć stałych przypadków, dokumentacja formatu, kontrola CLI, testy wykrywania rozbieżności i błędnego wejścia.

**Out of scope:** kod produkcyjny agregacji, API/UI/baza/auth, obliczanie okresów z konfiguracji, edycja/usuwanie/podziały zakupów, CSV i wdrożenie.
**Po MVP:** obie komendy w `verify`, rozszerzenie głównego README, przykład błędu i szczegółowa instrukcja przekazania danych do testów .NET; zapisane w Parked roadmapy.
Przykłady obejmują dodatnie kwoty i pełne przypisania; nie ustalają zasad zwrotów, zaokrąglania ani nieprzypisanych kategorii.

## Architecture / Approach

`tests/fixtures/spending-reconciliation/cases.json` zawiera rejestry i niezależnie ustalone wyniki.
`scripts/check-spending-reconciliation.py [fixture-path]` porównuje wybrane IDs i wszystkie sumy; domyślnie używa repozytoryjnego pliku.
Kody wyjścia: `0` zgodność, `1` rozbieżność, `2` błąd odczytu/formatu. `scripts/test-spending-reconciliation.py` sprawdza także uszkodzone kopie danych.
Przypadki: pusty rejestr; grosze `0.10 + 0.20 + 0.01 = 0.31`; hierarchia z `other` w dwóch kategoriach; granice stycznia i różna data wpisu; przełom roku; dwa osobne podobne wpisy liczone jako `1.00`.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Przypadki referencyjne i wykonywalna kontrola | JSON, opis danych, skrypt i testy | Pozorna zgodność sum ogólnych; sprawdzamy też IDs i oba poziomy hierarchii |

**Status:** faza 1 ukończona i odebrana, commit `75de4f3`; aktywny zakres F-01 na MVP zamknięty.
**Prerequisites:** Python 3.9+ i checkout repozytorium; bez wymogu Dockera dla nowych kontroli.
**Estimated effort:** aktywna faza już ukończona; prace po MVP wymagają osobnej zmiany i oszacowania.

## Open Risks & Assumptions

- Oczekiwania muszą być zapisane niezależnie i potwierdzone ręcznie, a nie wygenerowane kontrolowanym skryptem.
- Zestaw jest syntetyczny; kolejne przekroje rozszerzą go przy pierwszym użyciu nowych zachowań.
- Lokalny Python ma wersję 3.9.6. `actionlint` nie jest dostępny; plan nie wymaga go jako lokalnej bramki.
- Historyczny Baseline roadmapy jest starszy od wdrożenia; plan opiera się na istniejących plikach CI.
- Kontrolę referencyjną trzeba uruchamiać lokalnie do czasu podłączenia jej do CI po MVP. S-02 nadal weryfikuje rzeczywistą funkcję aplikacji w MVP.

## Success Criteria (Summary)

- Sześć przypadków przechodzi; testy wykrywają błędy kwoty, daty i hierarchii, także przy niezmienionej sumie ogólnej.
- Obie komendy działają lokalnie; podłączenie do CI pozostaje w backlogu po MVP.
- Użytkownik potwierdził wyliczenia; cztery pozycje fazy 1 w `Progress` są ukończone i wskazują commit `75de4f3`.

## References

- [Roadmapa F-01](../../foundation/roadmap.md), [PRD](../../foundation/prd.md), [pełny plan](plan.md).
- [Python Decimal](https://docs.python.org/3/library/decimal.html), [Python unittest](https://docs.python.org/3/library/unittest.html).
