<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Dostęp do własnego konta

- **Plan**: [plan.md](../plan.md)
- **Mode**: Deep
- **Date**: 2026-11-05
- **Verdict**: SOUND — po poprawkach, z zaakceptowanym ryzykiem F3
- **Initial verdict**: REVISE
- **Findings**: 0 critical, 3 warnings, 0 observations; po triage: 2 FIXED, 1 ACCEPTED, 0 PENDING
- **Review baseline**: HEAD `04344cc`; plan i brief z roboczego drzewa. Przegląd tylko do odczytu kodu; bez uruchamiania testów produktu i bez dostępu do konfiguracji działającego serwera. Docker nie jest dostępny lokalnie.

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | PASS |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | PASS |
| Plan Completeness | WARNING |

## Grounding

Grounding: 10/10 paths ✓, 3/3 symbols ✓, brief↔plan ✓.

Sprawdzone istniejące ścieżki: `backend/PennyPath.Api/PennyPath.Api.csproj`, `backend/PennyPath.Api/Program.cs`, `compose.yaml`, `.env.example`, `.dockerignore`, `scripts/deploy.sh`, `scripts/test-compose.sh`, `Dockerfile`, `frontend/package.json`, `frontend/src/App.vue`. Nowe ścieżki w planie są jawnie przeznaczone do utworzenia. Zweryfikowane istniejące punkty konfiguracji: dane PostgreSQL, `ASPNETCORE_HTTP_PORTS` i zależność workflow `publish.needs: verify`.

Sekcja Progress ma cztery fazy i 26 kryteriów zgodnych 1:1 z blokami faz; nie znaleziono checkboxów poza Progress. Brief odpowiada planowi. Weryfikacja z jednym subagentem objęła pakowanie, migracje, konsumentów skryptu wdrożeniowego i izolację testów Compose.

## Findings

### F1 — Brak kontraktu uprawnień do sekretu dla aplikacji

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 1 — Dostarczenie konfiguracji i pakowanie; plan.md:93,109
- **Detail**: Plan montuje istniejący plik `postgres_password` do `app`, lecz nie określa przygotowania jego uprawnień. Obraz działa jako `USER $APP_UID` (`Dockerfile:26`), a test oraz instrukcja operatora tworzą plik z trybem 0600 (`scripts/test-compose.sh:29`, `context/deployment/deploy-plan.md:42`). Gdy UID właściciela hostowego pliku różni się od UID aplikacji, proces app/migratora nie odczyta hasła. Dla sekretów Compose opartych na pliku ustawienia uid/gid/mode nie remapują uprawnień bind mountu — [dokumentacja Compose](https://docs.docker.com/reference/compose-file/services/#secrets). Dotychczasowy odczyt przez kontener PostgreSQL nie dowodzi dostępu użytkownika app.
- **Fix A ⭐ Recommended**: Zachować jeden plik hasła, umieścić go w prywatnym katalogu hosta 0700 i nadać plikowi 0444; udostępnić go app wyłącznie jako sekret tylko do odczytu. Opisać przygotowanie w fazie 1 oraz dostosować fixture Compose i późniejszą instrukcję wdrożeniową; sprawdzić odczyt z faktycznym UID app bez wypisywania zawartości.
  - Strength: Bez kopiowania hasła i zależności od mapowania UID; zachowuje istniejące hasło PostgreSQL.
  - Tradeoff: Poufność na hoście zależy od prywatnego katalogu 0700; trybu 0444 nie wolno stosować do pliku w katalogu dostępnym innym użytkownikom.
  - Confidence: HIGH — uprawnienia do samego pliku umożliwiają odczyt dowolnemu UID kontenera, a prywatny katalog ogranicza dostęp na hoście.
  - Blind spot: Nie zweryfikowano rzeczywistych uprawnień katalogów serwera ani zachowania mountu w docelowym Docker Desktop; wymaga kontroli na hoście z Dockerem.
- **Fix B**: Utworzyć osobny plik z tym samym hasłem dla app, z właścicielem odpowiadającym rzeczywistemu UID aplikacji i trybem 0400; zachować obecny plik PostgreSQL 0600. Opisać przygotowanie oraz kontrolę odczytu w kontenerze.
  - Strength: Zachowuje ograniczenie odczytu pliku app do jego właściciela i nie zmienia uprawnień dotychczasowego sekretu PostgreSQL.
  - Tradeoff: Dwie kopie hasła wymagają spójnego utrzymania; ustawienie właściciela na Linuksie może wymagać uprawnień administratora, a mapowanie UID trzeba potwierdzić na Macu.
  - Confidence: MED — działa przy poprawnym mapowaniu UID, lecz plan nie ma jeszcze procedury jego przygotowania.
  - Blind spot: Mapowanie UID na rzeczywistym hoście Docker Desktop nie zostało sprawdzone.
- **Decision**: FIXED — Fix A, zatwierdzony przez użytkownika. Plan fazy 1 obejmuje plik 0444 w prywatnym katalogu 0700, zachowanie hasła i aktualizację fixture/instrukcji; kryteria 4.6/4.9 mają doprecyzowanie kontroli odczytu bez roota. Brief zsynchronizowany. To naprawa planu; odczyt w rzeczywistym kontenerze pozostaje do weryfikacji podczas implementacji.

### F2 — Test i wdrożenie użyją tej samej podsieci

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 4 — Compose i rzeczywiste testy; plan.md:260,284
- **Detail**: Sieć edge ma domyślną podsieć `172.30.90.0/24` i adres proxy `172.30.90.2`. Kontrakt testu zmienia nazwę projektu, hostname i porty, lecz nie podsieć. Gdy na tym samym daemonie istnieje wdrożenie z domyślną siecią, Docker odrzuci utworzenie nakładającej się sieci testowej — [dokumentacja Docker network create](https://docs.docker.com/reference/cli/docker/network/create/). Sam odrębny COMPOSE_PROJECT_NAME izoluje nazwy zasobów, nie pule IPAM. Na świeżym daemonie CI problem nie wystąpi. Równoległe uruchamianie kilku testów nie jest nowym wymaganiem: obecny skrypt ma już stałe porty registry i app.
- **Fix**: W teście jawnie ustawić odrębną parę EDGE_SUBNET/PROXY_IP i przed startem sprawdzić konflikt; przy konflikcie zakończyć test czytelnym błędem, bez zmiany lub usuwania sieci wdrożenia.
- **Decision**: FIXED — użytkownik zatwierdził poprawkę. Kontrakt testu w fazie 4 jawnie ustawia EDGE_SUBNET=172.30.91.0/24 i PROXY_IP=172.30.91.2 oraz wymaga kontroli nakładania podsieci przed jej utworzeniem. Konflikt kończy test czytelnym błędem bez ingerencji w istniejące sieci. Doprecyzowano kryterium 4.6 i brief; rzeczywista kontrola Docker pozostaje do wykonania podczas implementacji.

### F3 — Brak granicy wdrażania pośrednich faz

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 1–3 oraz Phase 4 — instrukcja operatora; plan.md:93,109,270–272
- **Detail**: Wymagana konfiguracja bazy pojawia się w fazie 1, endpointy korzystające ze schematu w fazie 2, natomiast migrator wdrożenia, synchronizacja konfiguracji serwera i zatrzymanie DEPLOY_ENABLED dopiero w fazie 4. Istniejący workflow wdraża kwalifikujące się pushe do main przy DEPLOY_ENABLED=true (`.github/workflows/containers.yml:75,90`), a skrypt korzysta z trwałego Compose serwera i obecnie nie uruchamia migracji (`scripts/deploy.sh:36,51–52,136–140`). Przy osobnych merge'ach faz stara konfiguracja lub brak schematu może spowodować błąd startu albo niedziałające auth przy nadal zielonym /health. To ryzyko warunkowe: nie sprawdzano wartości DEPLOY_ENABLED na serwerze, a plan nie wymaga osobnego wdrożenia każdej fazy.
- **Fix**: Ustalić przed pierwszym merge/pushem S-01 do main granicę wdrożenia: przy aktywnym automatycznym deployu operator wyłącza DEPLOY_ENABLED i czeka na zakończenie trwających wdrożeń. Przywrócenie dopiero po przygotowaniu konfiguracji i migracji w fazie 4; verify/publish mogą działać wcześniej. To instrukcja planu, bez wykonywania zmian na serwerze podczas przeglądu.
- **Decision**: ACCEPTED — użytkownik akceptuje ryzyko do uwzględnienia podczas implementacji. Nie zastosowano proponowanej poprawki granicy wdrożenia. Ryzyko zapisano w Migration Notes planu i Open Risks & Assumptions briefu; nie zmieniano ustawień serwera ani workflow.

## Triage

Triage zakończone 2026-11-05.

- **Fixed**: F1 (Fix A), F2 — 2.
- **Accepted**: F3 — 1.
- **Skipped / Dismissed / Pending**: 0.
- **Verdict after triage**: REVISE → SOUND, z zaakceptowanym ostrzeżeniem F3 dotyczącym pośrednich wdrożeń. Plan Completeness pozostaje WARNING; akceptacja ryzyka nie oznacza jego naprawienia.
- Zachowano numery, tytuły i stan 26 kryteriów Progress. Status zmiany pozostaje plan_reviewed. Modyfikacje dotyczą dokumentów planowania; nie uruchamiano testów produktu, kontenerów ani wdrożeń.
