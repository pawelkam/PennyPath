<!-- BEGIN @przeprogramowani/10x-cli -->

## Zestaw narzędzi AI 10xDevs — Moduł 1, Lekcja 3

Przygotuj szkielet projektu dla stosu wybranego w Lekcji 2, z wykorzystaniem **łańcucha bootstrapowania**:

```
(/10x-init  →  /10x-shape  →  /10x-prd)  →  /10x-tech-stack-selector  →  /10x-bootstrapper
```

Łańcuch PRD pochodzi z Lekcji 1, a selektor stosu technologicznego z Lekcji 2 — oba zostały ponownie dołączone do tej lekcji, aby umożliwić poprawienie PRD lub zmianę stosu w trakcie pracy. `/10x-bootstrapper` jest głównym tematem lekcji. Łańcuch kończy się tutaj w wersji v1; przyszła Lekcja 4 skonfiguruje kontekst agenta (plik konfiguracji AI projektu (AGENTS.md), `AGENTS.md`).

### Router zadań — Od czego zacząć

| Umiejętność | Użyj jej, gdy |
| --- | --- |
| **Bootstrap (główny temat lekcji)** | |
| `/10x-bootstrapper` | Masz przekazanie w `context/foundation/tech-stack.md` (zapisane przez `/10x-tech-stack-selector`) i jesteś gotowy przygotować szkielet projektu w bieżącym katalogu. Umiejętność odczytuje przekazanie, wyszukuje wybraną kartę w rejestrze starterów, uruchamia jej CLI poprzez jedną z trzech strategii cwd (przygotowanie szkieletu w katalogu tymczasowym, a następnie przeniesienie plików poziom wyżej; przygotowanie szkieletu bezpośrednio w bieżącym katalogu; sklonowanie repozytorium startera bez zachowywania jego historii git), zawsze zachowuje `context/`, odstawia inne kolizje jako sąsiednie pliki `.scaffold`, wykonuje lekką kontrolę aktualności przed przygotowaniem szkieletu i dokładniejszy audyt po jego przygotowaniu oraz zapisuje dziennik weryfikacji do `context/changes/bootstrap-verification/verification.md`. Użyj PO `/10x-tech-stack-selector`. |
| **W razie potrzeby uruchom ponownie kroki nadrzędne** | |
| `/10x-init` / `/10x-shape` / `/10x-prd` / `/10x-tech-stack-selector` | Dołączone, aby umożliwić poprawienie PRD lub zmianę stosu w trakcie pracy. Jeśli `/10x-bootstrapper` zgłosi odmowę z powodu rozbieżności rejestru albo zmienisz zdanie co do startera, uruchom ponownie `/10x-tech-stack-selector`, aby ponownie wygenerować `tech-stack.md`, i wywołaj go ponownie. |

### Jak łańcuch przekazuje dane

- `/10x-tech-stack-selector` (Lekcja 2) zapisuje `context/foundation/tech-stack.md` z frontmatterem o 4 kluczach (`starter_id`, `package_manager`, `project_name`, `hints`) oraz jedn akapitową treścią `## Why this stack`.
- `/10x-bootstrapper` odczytuje ten plik W CAŁOŚCI (bez powrotu do historii rozmowy). Jeśli go nie ma, umiejętność odmawia, podając jednoliniowe przekierowanie do `/10x-tech-stack-selector`, i zatrzymuje się — bez wbudowanego mini-przekazania, bez trybu samodzielnego w v1.
- Wybrany `starter_id` jest wyszukiwany w `/skills/10x-tech-stack-selector/references/starter-registry.yaml`. Umiejętność korzysta z tego rejestru; nie jest jego właścicielem. Walidator CI (`scripts/validate-starter-registry-sync.mjs`) zapobiega odwoływaniu się przez bootstrapper do `starter_id`, którego nie ma w rejestrze.
- Umiejętność zapisuje `context/changes/bootstrap-verification/verification.md` jako dziennik ścieżki audytu dla uruchomienia. Schemat znajduje się w `/skills/10x-bootstrapper/references/verification-log-schema.md`.

### Co bootstrapper obejmuje (a czego NIE obejmuje)

- **Objęte (v1)**: przygotowanie szkieletu przez `cmd_template` wybranej karty (delegowanie do CLI, a nie generowanie plików wbudowane w umiejętność), trzy strategie cwd uruchamiane z `bootstrapper-config.yaml` (`subdir-then-move`, `native-cwd`, `git-clone`), ścisła polityka konfliktów tworząca sąsiednie pliki `.scaffold` + zawsze zachowująca `context/`, dwa etapy weryfikacji (lekka kontrola aktualności przed przygotowaniem szkieletu + dogłębny audyt uwzględniający język po jego przygotowaniu), podsumowanie audytu według poziomów ważności, pełny dziennik weryfikacji na dysku.
- **NIEOBJĘTE w v1 (celowo)**: generowanie `AGENTS.md` / pliku konfiguracji AI projektu (AGENTS.md) (odroczone do przyszłej Lekcji 4 — „Architektura pamięci”); nakładki rozmieszczenia elementów certyfikacyjnych dla poszczególnych starterów (należą do przyszłej umiejętności kontekstu agenta, nie tutaj); pliki przepływów pracy CI; awaryjne użycie AI jako pomostu dla stosów spoza rejestru (odroczone do v2 — w v1 selektor stosu technologicznego w trybie łańcuchowym już ogranicza wybór do rejestru, więc taki przypadek nie może wystąpić); tryb samodzielny, w którym użytkownik podaje stos wprost bez przekazania (odroczony do v2); działania kompensacyjne dla `bootstrapper_confidence: best-effort` lub `quality_override: true` (sygnalizowane w rozmowie, ale bez automatycznych działań następczych — to również zadanie przyszłej umiejętności architektury pamięci).

### Polityka konfliktów

Gdy umiejętność przenosi pliki z tymczasowego katalogu szkieletu do bieżącego katalogu roboczego, stosuje ścisłą macierz:

- **`context/**`** — wszystko, co szkielet próbował zapisać w `context/`, jest **odrzucane**. Twoje `context/` jest źródłem prawdy dla łańcucha bootstrapowania (PRD, przekazanie stosu technologicznego, plany, ramy) i nigdy nie jest nadpisywane.
- **`.gitignore`** — scalanie przez dopisanie: istniejące wiersze pozostają w kolejności, następnie wiersze szkieletu są odduplikowywane względem Twojego zestawu i dopisywane z komentarzem-separatorem. Semantyka ignorowania Git jest addytywna, więc łączenie jest bezpieczne.
- **`package.json`, `README.md`, plik konfiguracji AI projektu (AGENTS.md), `AGENTS.md`, `*.md` na poziomie katalogu głównego** — istniejący plik użytkownika ma pierwszeństwo; kopia ze szkieletu trafia jako sąsiedni plik `<filename>.scaffold`. Możesz użyć `diff README.md README.md.scaffold`, aby zobaczyć, co dostarczył starter w porównaniu z tym, co już miałeś.
- **Wszystko inne** — jest przenoszone bez komunikatu, jeśli nie ma konfliktu, lub odstawiane jako `<filename>.scaffold`, jeśli konflikt występuje. Macierz nigdy nie usuwa plików użytkownika.

W przypadku strategii `git-clone` (10x-astro-starter i podobne): sklonowany `.git/` jest usuwany przed przeniesieniem poziom wyżej, aby historia nadrzędnego startera nie przeniknęła do Twojego repozytorium. Następnie inicjalizujesz własną historię (`git init`).

### Dziennik weryfikacji

Każde uruchomienie zapisuje `context/changes/bootstrap-verification/verification.md`. Sekcje:

- **`## Hand-off`** — dosłowna kopia frontmatteru tech-stack.md oraz treści `## Why this stack`.
- **`## Pre-scaffold verification`** — tabela ustaleń dotyczących aktualności (wersja pakietu npm + `time.modified` dla starterów JS; GitHub `pushed_at` dla każdego startera z GitHub `docs_url`).
- **`## Scaffold log`** — rozwiązane wywołanie CLI, kod wyjścia, przeniesione pliki, konflikty przedstawione jako sąsiednie pliki `.scaffold`, obsługa `.gitignore`.
- **`## Post-scaffold audit`** — pełne wyniki audytu dla każdego języka (`npm audit --json` dla JS, `pip-audit` dla Python, `cargo audit` dla Rust itd.). Podział według ważności: CRITICAL i HIGH są prezentowane wprost na czacie, MODERATE i LOW tylko w dzienniku. Podział na zależności bezpośrednie i przechodnie, jeśli narzędzie go obsługuje.
- **`## Hints recorded but not acted on`** — każda wskazówka z przekazania, którą bootstrapper odczytał, ale nie wykorzystał w v1. Kompletność ścieżki audytu dla przyszłej umiejętności architektury pamięci.
- **`## Next steps`** — tekst wskazujący dalsze kroki. v1 podaje „your project is scaffolded and verified — happy hacking” i wskazuje przyszłą umiejętność z Lekcji 4 jako następne ogniwo łańcucha.

Folder (`context/changes/bootstrap-verification/`) celowo nie zawiera `change.md`. Uruchomienia bootstrapowania są artefaktami jednorazowymi, a nie śledzonymi zmianami przepływu pracy — folder zawiera dziennik i nic więcej. Ponowne uruchomienia stosują zabezpieczenie ostrzegające i wymagające potwierdzenia przed nadpisaniem; furtką awaryjną jest `verification-v2.md` (i kolejne).

### Ścieżki fundamentów używane przez tę lekcję

- `context/foundation/tech-stack.md` — dane wejściowe (z Lekcji 2)
- `context/changes/bootstrap-verification/verification.md` — dane wyjściowe (dziennik ścieżki audytu)
- `context/foundation/lessons.md` — powtarzające się zasady i pułapki
- `docs/reference/contract-surfaces.md` — rejestr nazw mających kluczowe znaczenie

### Uniwersalny język

Dostarczona umiejętność nie zawiera odniesień do 10xDevs / kohort / certyfikacji. Audyt po przygotowaniu szkieletu jest uruchamiany według `language_family` względem niewielkiej tabeli wyszukiwania; kohorty, których stos należy do `java`, `php`, `dart` lub kombinacji wielu języków, zobaczą w dzienniku wiersz „no built-in audit tool for this ecosystem” oraz rekomendowane narzędzie zewnętrzne, a nie fałszywy zapis „0 findings”.

Umiejętności nie mogą zapisywać do `context/archive/`. Zarchiwizowane zmiany są niezmienne; jeśli rozwiązana ścieżka docelowa zaczyna się od `context/archive/`, przerwij z komunikatem: „This change is archived. Open a new change with `/10x-new` instead.”

<!-- END @przeprogramowani/10x-cli -->
