# PennyPath — HTTPS w LAN na Macu M1

Szczegółowe przygotowanie Maca i GitHub opisuje [mac-m1-setup.md](mac-m1-setup.md). Ten dokument jest kontraktem aktualnego wdrożenia S-01. Komendy na rzeczywistym serwerze wykonuje operator; przygotowanie plików nie oznacza wdrożenia ani instalacji CA na urządzeniach.

## Zakres i trwałe zasoby

Jeden obraz `linux/arm64` zawiera Vue i ASP.NET Core. Proces app/migratora działa bez roota. PostgreSQL 18 używa istniejącego `pennypath_postgres_data` pod `/var/lib/postgresql` i nie publikuje portu. Caddy 2.11.9-alpine kończy TLS przez `tls internal`, bez publicznego DNS/ACME i bez publicznego redirectu HTTP. Wszystkie serwisy mają `restart: unless-stopped`.

Przeglądarka używa wspólnego originu `https://<LAN_HOST>:8443` (domyślny port). HTTP app jest publikowane tylko na `127.0.0.1:${APP_PORT:-8080}` do kontroli zdrowia. `LAN_HOST` to stały IP lub nazwa osiągalna na urządzeniach, bez protokołu, portu ani ścieżki. `localhost` w szablonie służy wyłącznie lokalnemu uruchomieniu.

Caddy ustawia `default_sni {$LAN_HOST}` dla klientów łączących się po IP bez SNI. Dzięki temu wybiera certyfikat adresu LAN także za przekierowaniem portu Dockera, gdzie adres docelowy wewnątrz kontenera jest inny. Test Compose sprawdza zarówno nazwę localhost, jak i URL z adresem IP przy włączonej weryfikacji CA.

App należy do sieci bazy i osobnej `edge`: `EDGE_SUBNET=172.30.90.0/24`. App i migrator otrzymują adresy z puli `EDGE_IP_RANGE=172.30.90.128/25`; statyczny adres proxy `PROXY_IP=172.30.90.2` jest poza nią. Dzięki temu app uruchomiony przed Caddy nie zajmuje jego IP. Sprawdź istniejące sieci Dockera przed konfiguracją; przy konflikcie dobierz wszystkie trzy ustawienia razem. Pula dynamiczna musi być częścią EDGE_SUBNET i nie może obejmować PROXY_IP. App przetwarza Forwarded-For/Proto wyłącznie od tego dokładnego IP, przed auth i CSRF. Caddy nadpisuje nagłówki otrzymane od klienta.

Zachowuj nazwę projektu `pennypath` i wolumeny:

- `postgres_data`: baza i konta Identity.
- `app_keys`: `/var/lib/pennypath/keys`, katalog APP_UID z trybem 0700; nazwa aplikacji Data Protection to `PennyPath`.
- `proxy_data` i `proxy_config`: CA, klucze oraz stan Caddy.

Wylogowanie kończy bieżącą sesję; cookie trwa najwyżej 7 dni bez przedłużania. Wymiana zgodnego obrazu nie powinna wymagać ponownego logowania. Development i testy używają osobnych kluczy. Kopie kluczy aplikacji i prywatnego CA przechowuj zaszyfrowane poza Macem wraz z kopią bazy; nie usuwaj wolumenów podczas zwykłego wdrożenia.

## 1. Przygotowanie publikacji i serwera

PR uruchamia `verify` na GitHub ARM64; build obrazu wykonuje xUnit, testy Vue i kontrolę typów, a test Compose sprawdza rzeczywisty PostgreSQL/HTTPS/sesje. Dopiero zielone `verify` odblokowuje `publish`. `main` publikuje tag SHA i digest GHCR, a `DEPLOY_ENABLED=true` włącza wydania na runnerze Maca (`self-hosted`, `macOS`, `ARM64`, `pennypath-deploy`). Pakiet GHCR powinien mieć widoczność publiczną dla anonimowego pull.

Na serwerze sprawdź `uname -m`, OrbStack, `docker info` i `docker compose version`. Włącz OrbStack po logowaniu, dostęp portu HTTPS z LAN i pracę bez uśpienia. Nie zakładamy działania przed zalogowaniem/FileVault. Node i SDK są potrzebne tylko podczas budowania, nie na serwerze.

Domyślne katalogi: `$HOME/Serwer/penny-path/repo`, `$HOME/Serwer/penny-path/deploy`, `$HOME/Serwer/penny-path/actions-runner`. Skrypt respektuje `DEPLOY_DIR`; dla istniejącej lokalizacji ustaw go jawnie przy każdym ręcznym uruchomieniu i w środowisku kroku deploy workflow. Używaj tego samego projektu i pliku Compose podczas utrzymania, aby nie utworzyć nowej bazy przypadkiem.

## 2. Pierwsza konfiguracja albo aktualizacja istniejącej

**Istniejąca instalacja:** ustaw `DEPLOY_ENABLED=false`, poczekaj na zakończenie aktywnych wydań, wykonaj i sprawdź kopię/odtworzenie z sekcji 5. Zachowaj poprzedni Compose, `.env`, release record, sekret i wolumen PostgreSQL. Dopiero potem zsynchronizuj `compose.yaml` i `deploy/Caddyfile` do trwałego katalogu. Nie uruchamiaj ponownego generowania hasła.

**Nowa instalacja**, z checkoutu repozytorium:

```sh
deploy_dir="${DEPLOY_DIR:-$HOME/Serwer/penny-path/deploy}"
mkdir -p "$deploy_dir"
chmod 700 "$deploy_dir"
if [ -e "$deploy_dir/.env" ] || [ -e "$deploy_dir/release.env" ] || [ -e "$deploy_dir/compose.yaml" ] || [ -e "$deploy_dir/postgres-password" ] || [ -e "$deploy_dir/secrets/postgres-password" ]; then
  printf 'Istniejąca konfiguracja: zachowaj bazę i hasło, wykonaj procedurę aktualizacji.\n'
else
  mkdir -p "$deploy_dir/secrets" "$deploy_dir/deploy"
  chmod 700 "$deploy_dir/secrets"
  cp compose.yaml "$deploy_dir/compose.yaml"
  cp deploy/Caddyfile "$deploy_dir/deploy/Caddyfile"
  cp .env.example "$deploy_dir/.env"
  (umask 077; openssl rand -hex 32 > "$deploy_dir/secrets/postgres-password")
  chmod 600 "$deploy_dir/.env"
  chmod 444 "$deploy_dir/secrets/postgres-password"
fi
```

Dla starego `./postgres-password` utwórz `secrets` z 0700, przenieś ten sam plik do `secrets/postgres-password` bez nadpisania istniejącego celu, ustaw plik 0444 i zmień wyłącznie ścieżkę `POSTGRES_PASSWORD_FILE` w `.env`. Nie regeneruj hasła: zmiana pliku nie zmienia hasła istniejącego PostgreSQL. Katalog 0700 musi być ustawiony **przed** nadaniem plikowi 0444. `.env` pozostaje 0600. Nie polegaj na remapowaniu `uid/gid/mode` sekretu Compose o źródle plikowym.

Zsynchronizuj konfigurację istniejącej instalacji po kopii (z checkoutu, z poprawnie ustawionym `deploy_dir`):

```sh
mkdir -p "$deploy_dir/deploy"
cp compose.yaml "$deploy_dir/compose.yaml"
cp deploy/Caddyfile "$deploy_dir/deploy/Caddyfile"
```

Edytuj zachowany `.env`: `POSTGRES_PASSWORD_FILE`, `LAN_HOST`, `HTTPS_PORT` (8443), `APP_PORT` (8080, tylko loopback), `EDGE_SUBNET`, `EDGE_IP_RANGE`, `PROXY_IP`. `APP_IMAGE` dostarcza skrypt. Nie przechowuj sekretów w `VITE_*`, CLI, repozytorium lub obrazie. Jeśli sieć edge została już utworzona bez puli dynamicznej, zastosuj procedurę jej wymiany z sekcji 21.8 instrukcji Maca M1; sama edycja `.env` nie zmienia istniejącego IPAM.

## 3. Pierwsze wydanie HTTPS i zaufanie CA

Z checkoutu, po pobraniu zatwierdzonego obrazu:

```sh
release_image='ghcr.io/pawelkam/pennypath@sha256:<64-znakowy-digest>'
docker pull --platform linux/arm64 "$release_image"
APP_IMAGE="$release_image" docker compose --project-name pennypath \
  --env-file "$deploy_dir/.env" -f "$deploy_dir/compose.yaml" config --quiet
APP_IMAGE="$release_image" docker compose --project-name pennypath \
  --env-file "$deploy_dir/.env" -f "$deploy_dir/compose.yaml" up -d --wait --wait-timeout 120 postgres
APP_IMAGE="$release_image" docker compose --project-name pennypath \
  --env-file "$deploy_dir/.env" -f "$deploy_dir/compose.yaml" run --rm --no-deps --entrypoint sh app -c \
  'test "$(id -u)" != 0 && test -r /run/secrets/postgres_password && cat /run/secrets/postgres_password >/dev/null && test -w /var/lib/pennypath/keys && test "$(stat -c %a /var/lib/pennypath/keys)" = 700 && test "$(stat -c %u /var/lib/pennypath/keys)" = "$(id -u)"'
DEPLOY_DIR="$deploy_dir" bash scripts/deploy.sh "$release_image"
```

Kontrola odczytu zwraca 0 bez zawartości sekretu. Deploy sam uruchamia `--migrate` z nowego obrazu, przed wymianą app. Migrator nie nasłuchuje HTTP, kończy się kodem 0/nonzero i można go bezpiecznie ponowić. `/health` jest kontrolą procesu, nie dowodem gotowości schematu. Zwykły start API nie migruje.

Następnie uruchom lub świadomie odtwórz proxy z trwałego katalogu:

```sh
cd "$deploy_dir"
unset APP_IMAGE
docker compose --project-name pennypath --env-file .env --env-file release.env up -d --no-deps proxy
docker compose --project-name pennypath --env-file .env --env-file release.env cp \
  proxy:/data/caddy/pki/authorities/local/root.crt ./pennypath-root.crt
```

Wyeksportuj **wyłącznie certyfikat root.crt**. Zweryfikuj jego fingerprint na serwerze i urządzeniu (np. `openssl x509 -in pennypath-root.crt -noout -fingerprint -sha256`). Zaufaj certyfikatowi lokalnego CA w systemie/przeglądarce na każdym urządzeniu LAN. Na macOS użyj Dostępu do pęku kluczy; na iOS po instalacji profilu włącz pełne zaufanie w Ustawienia → Ogólne → To urządzenie → Ustawienia zaufania certyfikatów. Nie kopiuj `root.key` ani całego magazynu `/data`, nie wyłączaj weryfikacji TLS i nie akceptuj ostrzeżeń jako odbioru. Po zmianie LAN_HOST odtwórz proxy; CA pozostaje to samo.

Z checkoutu, dla rzeczywistego LAN_HOST/HTTPS_PORT:

```sh
python3 scripts/smoke-test.py https://<LAN_HOST>:8443 --ca-cert "$deploy_dir/pennypath-root.crt"
python3 scripts/test-auth.py https://<LAN_HOST>:8443 --ca-cert "$deploy_dir/pennypath-root.crt"
```

Podstaw wartości za placeholdery. Test auth tworzy konta syntetyczne. Ręcznie zaufaj CA na dwóch urządzeniach/kontekstach i sprawdź rejestrację A/B, osobne logowanie, własny email, powrót do chronionej strony, restart przeglądarki z sesją i logout. Hasła 12–128 znaków, bez wymuszania klas znaków; email bez potwierdzenia. Recovery jest S-08, rzeczywiste wydatki S-02. Po odbiorze ponownie włącz `DEPLOY_ENABLED=true`.

## 4. Transakcja wdrożenia i rollback

`deploy.sh` akceptuje niezmienny digest ze wskazanego repozytorium i stosuje blokadę `.deploy-lock`. Sprawdza zgodność kontenera z `release.env`, zdrowie PostgreSQL i cache bieżącego obrazu. Pobiera nowy obraz, uruchamia `compose run --rm --no-deps --pull never app --migrate`, potem wymienia tylko app przez `up -d --no-deps --pull never app`. Błąd pull lub migracji zachowuje dotychczasowy kontener oraz release record; migracja może pozostawić zastosowane zmiany schematu i nie jest automatycznie cofana.

Po wymianie app skrypt sprawdza przez maksymalnie 120 sekund healthcheck, digest i lokalny HTTP 200. Dopiero sukces atomowo zapisuje APP_IMAGE / PREVIOUS_IMAGE. Błąd healthchecku przywraca poprzednio działający obraz bez cofania schematu; brak poprzedniego wydania oznacza zatrzymanie niesprawnego app. Nie usuwaj bieżącego obrazu z cache przed aktualizacją.

`bash scripts/deploy.sh rollback` zamienia zapisane wydania i **pomija migrator starszego obrazu**. Odpowiedzialność za zgodność starszego kodu z nowszym schematem pozostaje jawna; rollback nie usuwa kont, kluczy ani CA. S-01 dodaje tabele bez usuwania wcześniejszych danych. Stan syntetycznej sesji można zapisać `test-auth.py ... --state /private/temp/session.json`, a po zmianie obrazu sprawdzić `--resume` zamiast ponownego loginu. Artefakt 0600 zawiera cookie i ID, bez hasła; trzymaj go ulotnie poza repo i usuń po teście.

Automatyczny deploy aktualizuje app, bez restartu proxy/PostgreSQL. Compose, Caddyfile, obraz Caddy i CA utrzymuje operator. Przed takimi zmianami wyłącz deploy i poczekaj na trwające zadania. Po awarii zasilania/SIGKILL uzgodnij kontener z release.env, upewnij się, że nie trwa deploy, i dopiero wtedy usuń pustą pozostałą blokadę przez `rmdir`.

## 5. Kopia i próba odtworzenia

W trwałym katalogu, z wyłączonym automatycznym deployem:

```sh
cd "$deploy_dir"
umask 077
mkdir -p backups
chmod 700 backups
backup_file="backups/pennypath-$(date +%Y%m%d-%H%M%S).dump"
if docker compose --env-file .env --env-file release.env exec -T postgres \
  pg_dump -U pennypath -d pennypath -Fc > "$backup_file.tmp"; then
  mv "$backup_file.tmp" "$backup_file"
else
  printf 'Eksport nieudany; plik .tmp nie jest kompletną kopią.\n'
  exit 1
fi
docker compose --env-file .env --env-file release.env exec -T postgres \
  createdb -U pennypath pennypath_restore_check
docker compose --env-file .env --env-file release.env exec -T postgres \
  pg_restore -U pennypath -d pennypath_restore_check --exit-on-error < "$backup_file"
docker compose --env-file .env --env-file release.env exec -T postgres \
  psql -U pennypath -d pennypath_restore_check -c '\dt'
```

Sprawdź tabele Identity i liczbę kont w bazie kontrolnej, później także sumy wydatków. Po udanej kontroli usuń wyłącznie bazę `pennypath_restore_check`. Kopię, zachowany sekret i konfigurację oraz klucze/CA przechowuj zaszyfrowane poza Macem. Zmiana major PostgreSQL wymaga osobnego planu; nie usuwaj istniejącego wolumenu w celu naprawy dostępu.

## 6. Weryfikacja i odbiór

```sh
docker build --platform linux/arm64 -t pennypath:ci .
bash scripts/test-compose.sh
```

Test wymaga wolnych portów 5000/18080/18443 i podsieci `172.30.91.0/24` (`EDGE_IP_RANGE=172.30.91.128/25`, `PROXY_IP=172.30.91.2`). Wykrywa overlap przed tworzeniem zasobów i nigdy nie zmienia istniejących sieci. Sprawdza rzeczywisty adres app przed startem proxy, aby wykryć zajęcie statycznego IP przez pulę dynamiczną. Używa własnego projektu/bazy/registry, pliku sekretu 0444 w katalogu 0700 i ulotnej sesji 0600. Sprawdza prawdziwy odczyt sekretu przez non-root app/migrator, prywatny katalog kluczy, migrację i idempotencję, HTTPS z root CA bez obejść, A/B, CSRF, to samo ID po restartach/update/rollback, trwałość bazy i CA, failed pull, failed migration oraz rollback po healthchecku. Sprzątanie usuwa tylko własny projekt i registry; artefakty testu zostają w prywatnym katalogu tymczasowym do diagnostyki.

Brak Dockera pozostawia te kontrole oczekujące, również prawa mountów. Lokalna kompilacja lub SQLite nie zastępują wyników PostgreSQL/HTTPS/obrazu. Ręczny odbiór obejmuje ponadto mobile/desktop, klawiaturę i brak prywatnego renderowania przed potwierdzeniem sesji (oczekujące 3.6), dwa urządzenia/konteksty i poprawny trust CA (4.8), zachowaną bazę/sekret, kolejność migracji i trwałe klucze/CA (4.9). Sprawdź też restart Maca po zalogowaniu, start OrbStack/runnera i dostęp HTTPS z LAN.
