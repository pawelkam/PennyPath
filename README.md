# PennyPath

Starter Vue 3 / TypeScript i ASP.NET Core (.NET 10), uruchamiany przez Docker Compose na Macu M1 z OrbStack. Backend obsługuje build Vue, przykładowe `GET /weatherforecast` i `GET /health`. PostgreSQL 18 jest przygotowany do przyszłej integracji; aplikacja nie zapisuje jeszcze danych w bazie.

## Lokalny start z Dockerem (ARM64)

Z katalogu głównego repozytorium:

```sh
cp .env.example .env
openssl rand -hex 32 > postgres-password
chmod 600 .env postgres-password
docker build --platform linux/arm64 -t pennypath:local .
APP_IMAGE=pennypath:local docker compose --env-file .env up -d --wait
python3 scripts/smoke-test.py http://127.0.0.1:8080
```

Interfejs jest dostępny pod `http://localhost:8080`, a z drugiego urządzenia pod `http://<adres-Maca>:8080`. PostgreSQL nie publikuje portu. Projekt Compose nazywa się `pennypath`; dane są w wolumenie `pennypath_postgres_data`. Zatrzymanie przez `docker compose down` zachowuje wolumen; dodanie `--volumes` usuwa dane.

## Build bez Dockera

Wymagania: SDK .NET 10.0.401 i Node 24 co najmniej 24.12.0 (inne wersje dopuszczone przez `frontend/package.json` też działają).

```sh
cd frontend
npm ci
npm run build
cd ..
dotnet restore backend/PennyPath.slnx
dotnet build backend/PennyPath.slnx --no-restore
python3 scripts/test-deploy.py
```

Do testu wspólnej aplikacji skopiuj `frontend/dist/` do `backend/PennyPath.Api/wwwroot/` przed `dotnet publish`. Ten katalog jest generowany i ignorowany przez Git.

## Wdrożenie po merge

Workflow [Containers](.github/workflows/containers.yml) sprawdza PR na runnerze GitHub ARM64. Push do `main` dodatkowo publikuje `ghcr.io/pawelkam/pennypath:<pełny-SHA>` i, gdy `DEPLOY_ENABLED=true`, wdraża digest na runnerze Maca z etykietą `pennypath-deploy`. Ręczne uruchomienie workflow działa dla `main`.

Samodzielne przygotowanie serwera, GitHub i pipeline’u opisuje [szczegółowa instrukcja krok po kroku](context/deployment/mac-m1-setup.md). Skrócony kontrakt jest w [planie wdrożenia](context/deployment/deploy-plan.md). Wyniki dostępnej weryfikacji zapisano w [verification.md](context/deployment/verification.md).
