# PennyPath — Docker Compose na Macu M1

**Samodzielna instalacja od początku:** przejdź do [szczegółowej instrukcji krok po kroku](mac-m1-setup.md). Zawiera komendy, ustawienia GitHub, oczekiwane wyniki i diagnostykę. Ten dokument opisuje skrócony kontrakt wdrożenia.

## Zakres

Jeden obraz `linux/arm64` zawiera build Vue i ASP.NET Core. Aplikacja działa bez roota i udostępnia HTTP na porcie 8080. PostgreSQL 18 działa w osobnym kontenerze z trwałym wolumenem pod `/var/lib/postgresql` oraz healthcheckiem; port bazy pozostaje wewnętrzny. Oba serwisy mają `restart: unless-stopped`.

Na tym etapie wdrażany jest starter i syntetyczne dane kontrolne. Połączenie API z PostgreSQL, migracje, logowanie, wydatki i Cloudflare Tunnel należą do kolejnych etapów. `/health` potwierdza działanie procesu HTTP, a nie gotowość przyszłych funkcji bazy.

## 1. Pierwsza publikacja

1. Umieść kod wraz z frontendem, backendem, lockfile npm, Dockerfile, Compose, workflow i skryptami w `main` repozytorium `pawelkam/PennyPath`. Skonfiguruj `main` jako domyślną gałąź. W istniejącym workspace są także wcześniejsze zmiany użytkownika; dobierz pliki do commita świadomie.
2. Pozostaw zmienną repozytorium `DEPLOY_ENABLED` nieustawioną lub ustawioną na `false`. Push do `main` wykona weryfikację i publikację bez próby użycia domowego runnera.
3. Poczekaj na zielone zadania `verify` i `publish`. Obraz otrzyma tag pełnego SHA commita. Digest odczytaj z kroku publikacji lub strony pakietu.
4. W GitHub otwórz pakiet `pennypath` → Package settings → Change visibility → Public. Publiczny obraz umożliwia anonimowe pobranie na Macu. [GHCR tworzy nowy pakiet jako prywatny](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry).

Runner `ubuntu-24.04-arm` buduje natywnie ARM64. Dostępność i rozliczenia zależą od rodzaju repozytorium/konta; potwierdź je w [dokumentacji runnerów GitHub](https://docs.github.com/en/actions/reference/runners/github-hosted-runners). Workflow używa `GITHUB_TOKEN` z prawem `packages: write` tylko w zadaniu publikacji.

## 2. OrbStack i konto serwera

Na docelowym Macu:

1. Sprawdź `uname -m` (oczekiwane `arm64`) i `sw_vers`. Zainstaluj wspierany [OrbStack](https://docs.orbstack.dev/), uruchom go na koncie, które będzie wykonywało wdrożenia, i włącz start przy logowaniu.
2. Sprawdź `docker context use orbstack`, `docker info` i `docker compose version`. To konto musi mieć dostęp do silnika Docker i własnego katalogu `~/pennypath/deploy`.
3. Wyłącz usypianie Maca podczas pracy serwera. Zapewnij stabilny adres w LAN, np. rezerwację DHCP. Zezwól na port 8080 w lokalnym firewallu i sprawdź dostęp z drugiego urządzenia.
4. Zapewnij Git i Python 3 do ręcznej weryfikacji. Node i SDK .NET nie są wymagane na serwerze — build odbywa się w kontenerze na GitHub.

`restart: unless-stopped` działa po uruchomieniu silnika OrbStack. Nie zakładamy działania przed zalogowaniem użytkownika; FileVault, login i start OrbStack stanowią część próby restartu.

## 3. Inicjalizacja trwałego katalogu

Pobierz `main` do `~/pennypath/repo`. Z katalogu repozytorium wykonaj poniższe komendy, zastępując przykładowy digest rzeczywistym wynikiem pierwszej publikacji:

```sh
deploy_dir="$HOME/pennypath/deploy"
mkdir -p "$deploy_dir"
chmod 700 "$deploy_dir"
cp compose.yaml "$deploy_dir/compose.yaml"
cp .env.example "$deploy_dir/.env"
openssl rand -hex 32 > "$deploy_dir/postgres-password"
chmod 600 "$deploy_dir/.env" "$deploy_dir/postgres-password"

release_image='ghcr.io/pawelkam/pennypath@sha256:<64-znakowy-digest>'
APP_IMAGE="$release_image" docker compose --project-name pennypath \
  --env-file "$deploy_dir/.env" -f "$deploy_dir/compose.yaml" \
  up -d --wait --wait-timeout 120 postgres
bash scripts/deploy.sh "$release_image"
python3 scripts/smoke-test.py http://127.0.0.1:8080
```

Nie generuj ponownie hasła przy kolejnych wdrożeniach. PostgreSQL ustawia je przy pierwszej inicjalizacji wolumenu; zmiana pliku nie zmienia hasła istniejącej bazy. Sekret jest montowany tylko do PostgreSQL i nie trafia do obrazu, konfiguracji frontendowej ani repozytorium.

`release.env` zapisuje `APP_IMAGE` i `PREVIOUS_IMAGE`. Skrypt zapisuje je atomowo dopiero po pozytywnym healthchecku. Przy pierwszym wydaniu poprzedni digest jest pusty. Kolejne operacje Compose uruchamiaj w trwałym katalogu:

```sh
cd "$HOME/pennypath/deploy"
docker compose --env-file .env --env-file release.env ps
docker compose --env-file .env --env-file release.env logs --tail 100 app
```

Compose na serwerze jest trwałą konfiguracją, niezależną od checkoutu runnera. Zmiana `compose.yaml` w repozytorium wymaga świadomej synchronizacji tego pliku na serwer i weryfikacji wpływu na PostgreSQL. Automatyczne wydanie aktualizuje wyłącznie serwis `app`.

## 4. Self-hosted runner i włączenie wdrożeń

W repozytorium GitHub przejdź do Settings → Actions → Runners → New self-hosted runner. Wybierz macOS / ARM64 i wykonaj aktualne polecenia instalacji oraz rejestracji w osobnym katalogu, np. `~/actions-runner`. Dodaj etykietę **`pennypath-deploy`**; domyślne etykiety `self-hosted`, `macOS` i `ARM64` pozostają wymagane. [Rejestracja runnera](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners).

Runner ma działać na tym samym koncie co OrbStack. Najpierw uruchom `./run.sh` i sprawdź połączenie. Następnie skonfiguruj usługę `./svc.sh install` i `./svc.sh start`, według [instrukcji GitHub dla macOS](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/configure-the-application). Korzystaj z aktualnej wersji runnera obsługującej akcje Node 24 (minimum 2.327.1). Workflow dodaje `$HOME/.orbstack/bin` do PATH i wybiera kontekst `orbstack`.

Po udanym pierwszym wdrożeniu oraz sprawdzeniu dostępu LAN ustaw Settings → Secrets and variables → Actions → Variables → **`DEPLOY_ENABLED=true`**. Uruchom Actions → Containers → Run workflow → `main` i potwierdź zadanie `deploy`. Późniejszy merge PR do `main` automatycznie publikuje i wdraża obraz. PR wykonuje wyłącznie `verify` na GitHub; nie uruchamia zadań na domowym serwerze.

Cały workflow dla `main` ma wspólną grupę concurrency z `cancel-in-progress: false`. Trwające wydanie nie jest przerywane przez kolejne. GitHub może zastąpić starsze oczekujące uruchomienie nowszym; mechanizm nie jest kolejką gwarantującą wdrożenie każdego pośredniego commita. Skrypt dodatkowo ma lokalną blokadę, która chroni przed jednoczesnym ręcznym wdrożeniem.

## 5. Wdrożenie i rollback

Skrypt akceptuje wyłącznie pełny digest obrazu `ghcr.io/pawelkam/pennypath`. Przed zmianą sprawdza stan zapisany w `release.env`, zdrowie PostgreSQL i dostępność lokalnego obrazu poprzedniego wydania. Pobiera nowy obraz przed odtworzeniem aplikacji. Nieudane pobranie pozostawia działający kontener.

Aktualizacja używa `up -d --no-deps --pull never app`; nie odtwarza PostgreSQL ani jego wolumenu. Przez maksymalnie 120 sekund sprawdza healthcheck kontenera, oczekiwany digest i HTTP 200 z `/health`. Przy niepowodzeniu przywraca bieżący zapisany digest z lokalnego cache i ponownie sprawdza jego zdrowie; workflow kończy się błędem również po udanym rollbacku. Pierwsze nieudane wydanie zatrzymuje niesprawny `app`.

Ręczne cofnięcie do poprzedniego wydania, z checkoutu repozytorium:

```sh
bash scripts/deploy.sh rollback
```

Po udanym rollbacku bieżący i poprzedni digest zamieniają się miejscami. Ponowne wdrożenie tego samego digestu zachowuje poprzednie wydanie. Nie usuwaj obrazu aktualnego wydania z lokalnego cache przed zakończeniem aktualizacji. W przypadku błędu odtwarzania skrypt zgłasza go osobno; sprawdź logi i ręcznie wdróż digest zapisany w `release.env`.

Blokada `.deploy-lock` jest usuwana po zwykłym zakończeniu i sygnałach INT/TERM. Po awarii zasilania lub SIGKILL najpierw potwierdź, że nie działa żadne wdrożenie. Porównaj obraz kontenera z `release.env`, odtwórz zapisany obraz, jeśli aktualizacja została przerwana, a dopiero wtedy usuń pustą blokadę komendą `rmdir`. Przed ręcznym rollbackiem lub utrzymaniem zatrzymaj kolejkę wdrożeń przez `DEPLOY_ENABLED=false` i poczekaj na zakończenie aktywnego zadania.

## 6. Kopie PostgreSQL i odtwarzanie

W trwałym katalogu zrób logiczną kopię (zawiera dane, nie hasło logowania):

```sh
cd "$HOME/pennypath/deploy"
mkdir -p backups
chmod 700 backups
backup_file="backups/pennypath-$(date +%Y%m%d-%H%M%S).dump"
docker compose --env-file .env --env-file release.env exec -T postgres \
  pg_dump -U pennypath -d pennypath -Fc > "$backup_file"
chmod 600 "$backup_file"
```

Przechowuj zaszyfrowaną kopię poza Macem. Sam wolumen i kopia na tym samym dysku nie zabezpieczają przed awarią urządzenia. Po wdrożeniu funkcji produktu automatyzuj kopie z retencją dobraną do akceptowanej utraty danych.

Próbę odtworzenia przeprowadź w osobnej bazie, bez zmiany bazy aplikacji:

```sh
docker compose --env-file .env --env-file release.env exec -T postgres \
  createdb -U pennypath pennypath_restore_check
docker compose --env-file .env --env-file release.env exec -T postgres \
  pg_restore -U pennypath -d pennypath_restore_check --exit-on-error < "$backup_file"
docker compose --env-file .env --env-file release.env exec -T postgres \
  psql -U pennypath -d pennypath_restore_check -c '\dt'
```

Porównaj liczbę rekordów i przyszłe sumy wydatków, potem usuń bazę kontrolną przez `dropdb -U pennypath pennypath_restore_check`. Zmiana major PostgreSQL i przyszłe migracje domenowe wymagają własnej procedury; rollback obrazu aplikacji nie cofa schematu bazy.

## 7. Odbiór na docelowym Macu

- Z drugiego urządzenia w LAN otwórz `http://<adres-Maca>:8080` i `/health`.
- Utwórz PR i potwierdź, że działa tylko GitHub `verify`; po merge sprawdź zgodność `release.env` i `.Config.Image` kontenera z digestem publikacji.
- Uruchom workflow ręcznie dla `main`, sprawdź rollback po co najmniej dwóch różnych wydaniach oraz odtworzenie kopii.
- Zrestartuj Maca, zaloguj konto serwera, potwierdź start OrbStack, runnera, obu kontenerów i dostęp LAN. Zanotuj, jak FileVault wpływa na wznowienie. Kontener zatrzymany ręcznie wymaga ponownego uruchomienia.

Test kontenerowy w CI używa osobnego projektu Compose i lokalnego rejestru. Sprawdza ARM64, użytkownika bez roota, statyczny frontend, SPA, API, zdrowie, 404, trwałość danych po odtworzeniu PostgreSQL, aktualizację aplikacji, błędne pobranie obrazu i automatyczny oraz ręczny rollback. Uruchomienie lokalne na hoście ARM64 z Dockerem: `docker build --platform linux/arm64 -t pennypath:ci .`, następnie `bash scripts/test-compose.sh`. Test wymaga wolnych portów 5000 i 18080 i usuwa wyłącznie swoje kontenery oraz wolumen testowy.
