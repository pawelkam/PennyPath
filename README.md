# PennyPath

Vue 3 / TypeScript i ASP.NET Core (.NET 10), uruchamiane w jednym obrazie ARM64 na Macu M1 z OrbStack. Konta są przechowywane przez Identity / EF Core w PostgreSQL 18. Caddy udostępnia frontend i API pod jednym adresem HTTPS w LAN.

Użytkownik samodzielnie rejestruje konto emailem i hasłem (12–128 znaków), a następnie osobno się loguje. Potwierdzenie emaila nie jest wymagane. Sesja trwa najwyżej 7 dni od logowania, bez przedłużania aktywnością, i działa po restarcie przeglądarki oraz wymianie zgodnego kontenera. Wylogowanie kończy bieżącą sesję. Odzyskiwanie hasła jest osobną zmianą S-08; rejestr wydatków powstaje w S-02.

## Lokalny start z Dockerem (ARM64)

Poniższe przygotowanie jest przeznaczone dla **nowej instalacji**. Dla istniejącej bazy zachowaj projekt Compose, wolumen i dotychczasowe hasło; skorzystaj z [procedury aktualizacji](context/deployment/deploy-plan.md).

```sh
cp .env.example .env
chmod 600 .env
mkdir -p secrets
chmod 700 secrets
(umask 077; openssl rand -hex 32 > secrets/postgres-password)
chmod 444 secrets/postgres-password
docker build --platform linux/arm64 -t pennypath:local .
export APP_IMAGE=pennypath:local
docker compose --env-file .env up -d --wait --wait-timeout 120 postgres
docker compose --env-file .env run --rm --no-deps app --migrate
docker compose --env-file .env up -d --wait --wait-timeout 120 app proxy
docker compose --env-file .env cp proxy:/data/caddy/pki/authorities/local/root.crt /tmp/pennypath-root.crt
python3 scripts/smoke-test.py https://localhost:8443 --ca-cert /tmp/pennypath-root.crt
python3 scripts/test-auth.py https://localhost:8443 --ca-cert /tmp/pennypath-root.crt
```

Otwórz `https://localhost:8443` po zaufaniu wyeksportowanemu root CA w przeglądarce/systemie. Dla drugiego urządzenia ustaw w `.env` `LAN_HOST` na stały adres IP lub nazwę serwera, odtwórz `proxy` i zaufaj temu samemu CA na urządzeniu. Nie omijaj ostrzeżeń certyfikatu. Eksportuj wyłącznie `root.crt`; klucze prywatne CA pozostają na serwerze.

`HTTPS_PORT` domyślnie wynosi 8443; `APP_PORT=8080` publikuje HTTP tylko na `127.0.0.1` do kontroli zdrowia. PostgreSQL nie publikuje portu. Sieć `EDGE_SUBNET=172.30.90.0/24` ma osobną pulę dynamiczną `EDGE_IP_RANGE=172.30.90.128/25` dla app/migratora; stały `PROXY_IP=172.30.90.2` pozostaje poza tą pulą, aby Docker nie przydzielił go wcześniej aplikacji. Przy konflikcie sieci zmieniaj wszystkie trzy ustawienia razem. Aplikacja ufa nagłówkom Forwarded-For/Proto wyłącznie od tego IP. Wolumeny `postgres_data`, `app_keys`, `proxy_data`, `proxy_config` zachowują bazę, klucze sesji i CA. Zwykły `down` je zachowuje; `down --volumes` usuwa dane.

## Build i testy bez Dockera

Wymagania: SDK .NET 10.0.401 i Node 24 co najmniej 24.12.0 (pozostałe wersje zgodne z `frontend/package.json` też są dopuszczone), Python 3.9+.

```sh
npm --prefix frontend ci
npm --prefix frontend run test:unit -- --run
npm --prefix frontend run build
dotnet restore backend/PennyPath.slnx
dotnet tool restore
dotnet build backend/PennyPath.slnx --no-restore
dotnet test backend/PennyPath.slnx
mkdir -p artifacts
dotnet tool run dotnet-ef migrations script --idempotent --project backend/PennyPath.Api --output artifacts/private-account-access.sql
python3 scripts/test-deploy.py
bash -n scripts/deploy.sh
bash -n scripts/test-compose.sh
git diff --check
```

Testy xUnit używają relacyjnego SQLite wyłącznie do testów. Prawdziwy PostgreSQL, mounty sekretów, HTTPS i trwałość sesji wymagają `docker build --platform linux/arm64 -t pennypath:ci .` i `bash scripts/test-compose.sh`. Test Compose używa syntetycznych kont, własnego projektu, portów 5000/18080/18443 i osobnej podsieci `172.30.91.0/24`; przy kolizji podsieci odmawia startu bez zmiany istniejących sieci.

Do uruchomienia lokalnego API ustaw `Database__Host`, `Database__Port`, `Database__Name`, `Database__Username` oraz `Database__PasswordFile` wskazujący plik hasła dostępnej bazy PostgreSQL. Nie przekazuj hasła w CLI ani frontendowych `VITE_*`. Po przygotowaniu bazy migrator lokalny to `ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/PennyPath.Api --no-launch-profile -- --migrate`; zwykły start API nie migruje schematu.

Wariant wspólnego originu: zbuduj Vue, skopiuj `frontend/dist/` do `backend/PennyPath.Api/wwwroot/`, zaufaj certyfikatowi deweloperskiemu przez `dotnet dev-certs https --trust`, uruchom `dotnet run --project backend/PennyPath.Api --launch-profile https` i otwórz HTTPS wypisany w logu. Osobny serwer Vite i proxy HTTPS opisuje [frontend/README.md](frontend/README.md).

Stabilny `userId` pochodzi z `IdentityUser.Id` / claimu `ClaimTypes.NameIdentifier`, nigdy z emaila ani ID przesłanego przez klienta. S-02 użyje go jako właściciela wydatków. Cookie auth jest Secure/HttpOnly; CSRF jest wymagany także dla anonimowego logowania i rejestracji.

## Wdrożenie po merge

Workflow [Containers](.github/workflows/containers.yml) sprawdza PR na GitHub ARM64. Testy backendu i Vue są częścią budowania finalnego obrazu; HTTPS/PostgreSQL/sesje oraz ścieżki błędów wdrożenia działają w `verify` i blokują `publish`. Push do `main` publikuje obraz GHCR i przy `DEPLOY_ENABLED=true` wdraża digest na runnerze Maca z etykietą `pennypath-deploy`.

Domyślny trwały katalog to `$HOME/Serwer/penny-path/deploy`; override: `DEPLOY_DIR`. `deploy.sh` pobiera obraz, uruchamia jego `--migrate` i dopiero po sukcesie wymienia app. Błąd migracji zachowuje działający kontener i `release.env`. Rollback przywraca obraz bez uruchamiania starszego migratora i bez cofania schematu.

Przed pierwszym przejściem na konfigurację HTTPS operator wyłącza `DEPLOY_ENABLED`, czeka na aktywne wydania, sprawdza kopię i odtworzenie bazy oraz synchronizuje Compose i Caddyfile do trwałego katalogu. Automatyczne wydania zmieniają tylko app; utrzymanie proxy/CA jest osobnym krokiem. Szczegóły: [plan wdrożenia](context/deployment/deploy-plan.md), [instrukcja Maca M1](context/deployment/mac-m1-setup.md). Dotychczasowe wyniki środowiskowe są w [verification.md](context/deployment/verification.md); nie dowodzą odbioru tej zmiany.
