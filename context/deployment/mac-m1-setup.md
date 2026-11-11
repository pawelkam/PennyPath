# PennyPath — samodzielna konfiguracja Maca M1 i pipeline’u

Instrukcja przygotowana 2026-10-14 dla plików obecnych w tym repozytorium. Wszystkie instalacje, zmiany ustawień GitHub, komendy na serwerze i wdrożenia wykonujesz sam. Instrukcja prowadzi od pustej konfiguracji do pierwszego wdrożenia i aktualizacji po merge.

## Jak korzystać z instrukcji

Każdy etap ma oznaczenie miejsca wykonania:

| Miejsce | Znaczenie |
| --- | --- |
| **Komputer roboczy** | Komputer z aktualnym workspace PennyPath i zmianami przygotowanymi do publikacji |
| **GitHub** | Przeglądarka, repozytorium `pawelkam/PennyPath` |
| **Mac-serwer** | Osobny Mac M1, zalogowane konto uruchamiające OrbStack i runnera |
| **Drugie urządzenie** | Telefon lub komputer w tej samej domowej sieci |

Wykonuj bloki po kolei i sprawdzaj wynik przed przejściem dalej. Fragmenty oznaczone `<...>` zastępujesz własnymi wartościami. Zmienne terminala nie przechodzą do nowego okna — w razie jego otwarcia ustaw je ponownie.

Zakładamy repozytorium `pawelkam/PennyPath`, gałąź `main`, port aplikacji `8080` i konto użytkownika na Macu. Jeśli zmienisz właściciela/nazwę repozytorium, trzeba także dostosować adres obrazu w workflow, `scripts/deploy.sh` i etykietę źródła w Dockerfile.

## Efekt końcowy

```text
Komputer roboczy → PR do main → sprawdzenia na GitHub ARM64
                              ↓ merge
                         push do main
                              ↓
                   verify → publish do GHCR
                              ↓
                  deploy na runnerze Maca M1
                              ↓
                  OrbStack / Docker Compose
                    app + PostgreSQL 18
                              ↓
                   http://<adres-Maca>:8080
```

Przeglądarka korzysta z jednego adresu dla Vue i API. Obraz aplikacji zawiera obie części; na serwerze nie instalujesz Node ani SDK .NET. PostgreSQL ma własny trwały wolumen i nie publikuje portu w LAN. `/weatherforecast` zwraca przykładowe dane, `/health` potwierdza działanie HTTP. Faza 1 S-01 dodaje konfigurację bazy, magazyn Identity i jednorazowy tryb `--migrate`; logowanie przez UI/API, HTTPS w LAN, wydatki i Cloudflare Tunnel powstają później. `/health` nie sprawdza schematu kont. Zwykły start aplikacji nie migruje bazy, a integracja migratora z automatycznym wydaniem należy do fazy 4 S-01.

### Pliki, z których korzystasz

| Plik | Rola |
| --- | --- |
| [Dockerfile](../../Dockerfile) | Buduje Vue oraz backend .NET i tworzy obraz ARM64 |
| [compose.yaml](../../compose.yaml) | Definiuje aplikację, PostgreSQL, wolumen i sekret |
| [.env.example](../../.env.example) | Szablon konfiguracji serwera |
| [containers.yml](../../.github/workflows/containers.yml) | Pipeline PR, publikacji i wdrożenia |
| [deploy.sh](../../scripts/deploy.sh) | Wdraża digest i przywraca aplikację po błędzie |
| [smoke-test.py](../../scripts/smoke-test.py) | Sprawdza stronę, statyczne zasoby, API i 404 |
| [test-compose.sh](../../scripts/test-compose.sh) | Sprawdza rzeczywiste kontenery w izolacji w CI |

Pliki są przygotowane w workspace. Pipeline zacznie działać dopiero po ich wysłaniu do GitHub.

## 1. Przygotuj konto i system na serwerze

**Miejsce: Mac-serwer.**

Zaloguj się na konto, którego będziesz używać do obsługi aplikacji. To samo konto uruchomi OrbStack i GitHub runnera. Otwórz Terminal i wykonaj:

```sh
whoami
uname -m
sw_vers
df -h "$HOME"
```

**Oczekiwany wynik:** znasz nazwę konta, architektura to `arm64`, znasz wersję macOS i ilość wolnego miejsca. Nazwę konta zapisz — jej katalog domowy będzie przechowywał konfigurację wdrożeń.

Sprawdź narzędzia:

```sh
git --version
python3 --version
curl --version
openssl version
```

Jeśli brakuje Git/narzędzi Apple, uruchom `xcode-select --install`, przejdź instalator i sprawdź ponownie. Jeśli Python 3 nadal nie działa, zainstaluj go przez Homebrew (`brew install python`, gdy masz już Homebrew) albo instalator z [python.org](https://www.python.org/downloads/macos/). Python służy do testu HTTP; nie uruchamia aplikacji.

W Ustawieniach systemowych ustaw pracę Maca bez automatycznego usypiania podczas zasilania. Ekran może się wyłączać. W zależności od modelu i wersji macOS ustawienie jest w sekcji Energia lub Bateria → Opcje. [Instrukcja Apple](https://support.apple.com/guide/mac-help/set-sleep-and-wake-settings-mchle41a6ccd/mac).

Ten wariant działa po zalogowaniu użytkownika i uruchomieniu OrbStack. Nie wymaga włączania automatycznego logowania ani wyłączania FileVault.

## 2. Zainstaluj i sprawdź OrbStack

**Miejsce: Mac-serwer.**

1. Pobierz OrbStack z [orbstack.dev](https://orbstack.dev/download), zainstaluj i otwórz aplikację. Alternatywnie, z istniejącym Homebrew: `brew install orbstack`.
2. Przejdź pierwsze uruchomienie i pozwól aplikacji przygotować narzędzia CLI.
3. Włącz uruchamianie OrbStack przy logowaniu. Sprawdź także Ustawienia systemowe → Ogólne → Rzeczy otwierane podczas logowania, jeśli ustawienie nie jest widoczne w aplikacji.
4. W ustawieniach Docker OrbStack upewnij się, że **Expose ports to LAN** jest włączone. Dzięki temu opublikowany port aplikacji będzie dostępny z telefonu/komputera w domu. [Instalacja](https://docs.orbstack.dev/quick-start), [dostęp LAN](https://docs.orbstack.dev/docker/network).

Otwórz nowe okno Terminala:

```sh
export PATH="$HOME/.orbstack/bin:/opt/homebrew/bin:$PATH"
docker context ls
docker context use orbstack
docker version
docker compose version
docker info
docker run --rm --platform linux/arm64 hello-world
```

**Oczekiwany wynik:** kontekst `orbstack` jest aktywny, `docker version` pokazuje klienta i serwer, działa Compose, kontener `hello-world` kończy się sukcesem.

Jeśli widzisz `Cannot connect to the Docker daemon`, uruchom OrbStack i sprawdź kontekst. Jeśli `docker: command not found`, sprawdź konfigurację narzędzi CLI w OrbStack i PATH. Nie używaj `sudo docker` — silnik i konfiguracja należą do zalogowanego konta.

## 3. Przygotuj GitHub, zanim wyślesz kod

**Miejsce: GitHub.**

Otwórz [repozytorium PennyPath](https://github.com/pawelkam/PennyPath).

1. W Settings → Actions → General sprawdź, czy Actions są włączone i polityka pozwala korzystać z `actions/checkout` oraz akcji `docker/*` użytych w workflow.
2. W Settings → Secrets and variables → Actions → **Variables** dodaj zmienną repozytorium:

   | Nazwa | Wartość |
   | --- | --- |
   | `DEPLOY_ENABLED` | `false` |

3. Nie dodawaj na tym etapie hasła PostgreSQL ani tokenu GHCR do Secrets. Hasło zostanie plikiem na Macu; zadanie publikacji korzysta z automatycznego `GITHUB_TOKEN` i deklaracji `packages: write` we własnym workflow.
4. Jeśli repozytorium ma już gałęzie, ustaw `main` jako domyślną. Jeśli jest puste, zrób to po pierwszym push.

**Oczekiwany wynik:** `DEPLOY_ENABLED=false` widnieje w Variables. Pierwszy push będzie budował i publikował obraz, ale nie będzie jeszcze wdrażał na Maca.

## 4. Wyślij przygotowany kod

**Miejsce: komputer roboczy, aktualne repozytorium PennyPath.**

```sh
cd /Users/pawelkaminski/Projects/PennyPath
git status --short
git branch --show-current
git remote -v
```

Sprawdź, czy gałąź to `main`, a `origin` wskazuje `pawelkam/PennyPath`. Jeśli pracujesz na innej gałęzi, przekaż tę zmianę do `main` swoim zwykłym sposobem. Nie zmieniaj gałęzi ani nie odrzucaj istniejących zmian bez ich przejrzenia.

W workspace są też wcześniejsze zmiany dotyczące kursu i AGENTS.md. Poniższa lista wybiera aplikację oraz konfigurację wdrożenia; dopasuj ją do tego, co chcesz opublikować:

```sh
git add .dockerignore .env.example .gitignore .github Dockerfile compose.yaml global.json README.md
git add frontend backend scripts context/deployment
git add context/foundation/tech-stack.md context/foundation/infrastructure.md
git diff --cached --stat
git diff --cached --check
git diff --cached
```

**Sprawdź przed commitem:** w staged są frontend, backend, `frontend/package-lock.json`, Dockerfile, Compose, workflow i skrypty. Nie ma prawdziwego `.env`, pliku hasła, `node_modules`, `bin`, `obj` ani wygenerowanego `wwwroot`.

```sh
git commit -m "Prepare ARM64 Compose deployment and GitHub pipeline"
git push -u origin main
```

Jeśli Git wymaga konfiguracji autora, ustaw `git config user.name "<Twoje imię>"` i `git config user.email "<Twój email do commitów>"`, potem ponów commit. Jeśli HTTPS pyta o hasło do GitHub, użyj skonfigurowanej autoryzacji Git/SSH lub GitHub CLI; hasło konta nie służy do uwierzytelniania push.

Jeśli push zgłasza `non-fast-forward`, sprawdź istniejące commity na GitHub i uzgodnij historię przed ponowną próbą. Pierwsza publikacja nie wymaga `--force`.

## 5. Sprawdź pierwszy pipeline

**Miejsce: GitHub → Actions → Containers.**

Otwórz uruchomienie dla wysłanego commita `main`. Zadania powinny wyglądać tak:

| Zadanie | Oczekiwany wynik przy pierwszym push |
| --- | --- |
| `verify` | Zielone: testy skryptów, build Vue/.NET ARM64 i testy kontenerów |
| `publish` | Zielone: obraz opublikowany do GHCR |
| `deploy` | `Skipped`, ponieważ `DEPLOY_ENABLED=false` |

`verify` działa na `ubuntu-24.04-arm`, nie na domowym Macu. Sprawdza m.in. trwałość PostgreSQL, nieudane pobranie obrazu i rollback w osobnym projekcie Compose. Dostępność i rozliczenie runnera sprawdzisz w [dokumentacji GitHub](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).

Jeśli zadanie jest czerwone, otwórz pierwszy krok z błędem, popraw przyczynę i powtórz pipeline. Nie przechodź do wdrożenia, dopóki `verify` i `publish` nie będą zielone. Zapisz pełny SHA tego commita — tag obrazu jest równy temu SHA.

Workflow już zawiera wymagany podział pracy i uprawnienia. Nie musisz tworzyć drugiego workflow ani dodatkowego pipeline’u w OrbStack.

## 6. Ustaw obraz GHCR jako publiczny

**Miejsce: GitHub.**

Otwórz profil `pawelkam` → Packages → `pennypath`, następnie Package settings → Change visibility → **Public**. Pakiet może być również widoczny w sekcji Packages przy repozytorium.

Widoczność pakietu jest osobnym ustawieniem od widoczności repozytorium. Nowy pakiet jest domyślnie prywatny; w tym wariancie publiczny obraz będzie pobierany anonimowo przez Maca. [Dokumentacja GHCR](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry).

**Oczekiwany wynik:** pakiet `ghcr.io/pawelkam/pennypath` jest publiczny i zawiera wersję oznaczoną pełnym SHA pierwszego commita.

## 7. Pobierz repozytorium na serwer

**Miejsce: Mac-serwer, konto z kroku 1.**

```sh
mkdir -p "$HOME/Serwer/penny-path/"
git clone --branch main https://github.com/pawelkam/PennyPath.git "$HOME/Serwer/penny-path/repo"
cd "$HOME/Serwer/penny-path/repo"
git rev-parse HEAD
```

Jeśli repozytorium jest prywatne, wykonaj clone przez skonfigurowany dostęp SSH, np. `git@github.com:pawelkam/PennyPath.git`, albo przez uwierzytelniony GitHub CLI. Dostęp do kodu i anonimowe pobranie publicznego obrazu to dwie niezależne sprawy.

Jeśli katalog `repo` już istnieje i jest Twoim checkoutem, zamiast ponownego clone użyj `git status`, sprawdź gałąź i pobierz aktualizacje przez `git pull --ff-only origin main` na `main`.

**Oczekiwany wynik:** istnieją `compose.yaml`, `.env.example`, `scripts/deploy.sh` i workflow. SHA checkoutu odpowiada zielonej publikacji z kroku 5. Nie przechodź dalej, jeśli najnowszy commit nie ma jeszcze opublikowanego obrazu.

Docelowe katalogi:

```text
~/pennypath/repo/           ręczny checkout kodu i skryptów
~/pennypath/deploy/         trwała konfiguracja, hasło, digests i kopie
~/actions-runner/           instalacja runnera oraz jego roboczy checkout
```

Workflow sam pobiera właściwy commit do katalogu roboczego runnera. Nie musisz aktualizować ręcznego checkoutu na serwerze po każdym merge; aktualizuj go, gdy chcesz korzystać z nowszej wersji skryptów ręcznie.

## 8. Utwórz trwałą konfigurację i hasło PostgreSQL

**Miejsce: Mac-serwer. Ten etap wykonujesz raz.**

```sh
cd "$HOME/Serwer/penny-path/repo"
deploy_dir="$HOME/Serwer/penny-path/deploy"
mkdir -p "$deploy_dir"
chmod 700 "$deploy_dir"
```

Sprawdź, czy katalog nie zawiera już `.env`, `postgres-password`, `secrets/postgres-password` albo `release.env`. Jeśli są, masz istniejącą konfigurację — przejdź do aktualizacji sekretu opisanej niżej, zamiast wykonywać inicjalizację ponownie.

Poniższy blok odmawia nadpisania istniejących plików:

```sh
if [ -e "$deploy_dir/.env" ] || [ -e "$deploy_dir/postgres-password" ] || [ -e "$deploy_dir/secrets/postgres-password" ] || [ -e "$deploy_dir/compose.yaml" ]; then
  printf 'Konfiguracja już istnieje. Sprawdź ją przed dalszymi krokami.\n'
else
  mkdir -p "$deploy_dir/secrets"
  chmod 700 "$deploy_dir/secrets"
  cp compose.yaml "$deploy_dir/compose.yaml"
  cp .env.example "$deploy_dir/.env"
  (umask 077; openssl rand -hex 32 > "$deploy_dir/secrets/postgres-password")
  chmod 600 "$deploy_dir/.env"
  chmod 444 "$deploy_dir/secrets/postgres-password"
fi
```

Sprawdź pliki i konfigurację bez wyświetlania hasła:

```sh
ls -ld "$deploy_dir/secrets"
ls -l "$deploy_dir/compose.yaml" "$deploy_dir/.env" "$deploy_dir/secrets/postgres-password"
cat "$deploy_dir/.env"
test -s "$deploy_dir/secrets/postgres-password" && printf 'Plik hasła jest niepusty.\n'
```

W `.env` powinno być:

```dotenv
POSTGRES_PASSWORD_FILE=./secrets/postgres-password
APP_PORT=8080
```

Komentarze z szablonu mogą zostać. `APP_IMAGE` będzie ustawiane przez skrypt i po udanym wdrożeniu zapisane w `release.env`.

Hasło pozostaje na serwerze. Jeden plik jest montowany tylko do odczytu do PostgreSQL, app i jednorazowego migratora. Plik ma `0444`, aby użytkownik app bez roota mógł go odczytać; katalog `secrets` musi wcześniej otrzymać `0700`, by inni użytkownicy hosta nie mogli dotrzeć do pliku. `.env` pozostaje `0600`. Nie polegaj na `uid/gid/mode` sekretu Compose o źródle plikowym. Po pierwszej inicjalizacji wolumenu nie generuj hasła ponownie — zmiana pliku nie zmienia hasła istniejącej bazy.

### Aktualizacja sekretu istniejącej instalacji

Jeżeli `.env` wskazuje obecnie `./postgres-password`, zachowaj jego zawartość i przenieś plik do prywatnego katalogu. Ten blok nie tworzy nowego hasła i nie nadpisuje istniejącego celu:

```sh
mkdir -p "$deploy_dir/secrets"
chmod 700 "$deploy_dir/secrets"
if [ -f "$deploy_dir/postgres-password" ] && [ ! -e "$deploy_dir/secrets/postgres-password" ]; then
  mv "$deploy_dir/postgres-password" "$deploy_dir/secrets/postgres-password"
  chmod 444 "$deploy_dir/secrets/postgres-password"
else
  printf 'Sprawdź istniejącą ścieżkę i plik docelowy; nie generuj nowego hasła.\n'
fi
```

Ustaw `POSTGRES_PASSWORD_FILE=./secrets/postgres-password` w dotychczasowym `.env`, zachowując inne ustawienia i tryb `0600`. Jeśli dotychczasowa ścieżka jest niestandardowa, najpierw ustal rzeczywisty plik źródłowy. Zsynchronizuj aktualny Compose zgodnie z procedurą utrzymania z sekcji 19, zachowując projekt i wolumen PostgreSQL. Nie uruchamiaj inicjalizacji bazy od nowa. Kontrolę odczytu w kontenerze wykonasz po pobraniu obrazu w sekcji 10.

## 9. Pobierz dokładny obraz pierwszego wydania

**Miejsce: Mac-serwer.**

```sh
cd "$HOME/Serwer/penny-path/repo"
release_sha=$(git rev-parse HEAD)
docker pull --platform linux/arm64 "ghcr.io/pawelkam/pennypath:$release_sha"
release_image=$(docker image inspect --format '{{index .RepoDigests 0}}' "ghcr.io/pawelkam/pennypath:$release_sha")
printf '%s\n' "$release_image"
docker image inspect --format '{{.Architecture}}' "$release_image"
```

**Oczekiwany wynik:** obraz pobiera się bez `docker login`, `release_image` ma postać `ghcr.io/pawelkam/pennypath@sha256:` i 64 znaki digestu, a architektura to `arm64`.

Skrypt wdrożeniowy akceptuje digest, a nie tag. Dzięki temu wdraża konkretny obraz. Jeśli otworzysz nowy Terminal, odtwórz zmienne z tego kroku lub ustaw `release_image` na zapisany pełny adres digestu.

Jeśli pull zwraca `denied`/`unauthorized`, wróć do ustawień widoczności pakietu. Jeśli obraz nie istnieje, sprawdź pełny SHA oraz wynik `publish`.

## 10. Uruchom PostgreSQL i pierwszą aplikację

**Miejsce: Mac-serwer.**

```sh
deploy_dir="$HOME/Serwer/penny-path/deploy"
APP_IMAGE="$release_image" docker compose --project-name pennypath \
  --env-file "$deploy_dir/.env" -f "$deploy_dir/compose.yaml" \
  config --quiet

APP_IMAGE="$release_image" docker compose --project-name pennypath \
  --env-file "$deploy_dir/.env" -f "$deploy_dir/compose.yaml" \
  up -d --wait --wait-timeout 120 postgres
```

**Oczekiwany wynik:** konfiguracja jest poprawna i PostgreSQL osiąga stan healthy. `APP_IMAGE` jest tutaj wymagane do odczytania całego Compose, nawet gdy uruchamiasz tylko bazę.

Sprawdź odczyt sekretu z UID obrazu app; oczekiwany kod to 0 bez wyświetlania hasła:

```sh
APP_IMAGE="$release_image" docker compose --project-name pennypath \
  --env-file "$deploy_dir/.env" -f "$deploy_dir/compose.yaml" \
  run --rm --no-deps --entrypoint sh app -c \
  'test "$(id -u)" != 0 && test -r /run/secrets/postgres_password && cat /run/secrets/postgres_password >/dev/null'
```

Po ręcznym przeglądzie migracji uruchom ją osobno z nowego obrazu. Dla istniejącej bazy najpierw wykonaj i sprawdź kopię z sekcji 17:

```sh
APP_IMAGE="$release_image" docker compose --project-name pennypath \
  --env-file "$deploy_dir/.env" -f "$deploy_dir/compose.yaml" \
  run --rm --no-deps app --migrate
```

Oczekiwany kod to 0 i potwierdzenie zastosowania migracji; ponowienie jest idempotentne. Proces nie uruchamia serwera HTTP. Przy błędzie nie kontynuuj wydania — sprawdź konfigurację i dostępność bazy. Automatyczny `deploy.sh` otrzyma ten krok dopiero w fazie 4 S-01.

Po sukcesie migracji uruchom pierwsze wydanie aplikacji:

```sh
cd "$HOME/Serwer/penny-path/repo"
bash scripts/deploy.sh "$release_image"
python3 scripts/smoke-test.py http://127.0.0.1:8080
```

**Oczekiwany wynik:** komunikat `Deployed ...` i pozytywny wynik testu Vue, zasobów, SPA, health, API oraz 404. Skrypt utworzył `~/pennypath/deploy/release.env`.

Sprawdź stan:

```sh
cd "$HOME/Serwer/penny-path/deploy"
docker compose --env-file .env --env-file release.env ps
cat release.env
docker volume inspect pennypath_postgres_data
```

Oba serwisy powinny być healthy. `APP_IMAGE` w `release.env` zawiera wdrożony digest; `PREVIOUS_IMAGE` jest pusty przy pierwszym wydaniu. Wolumen `pennypath_postgres_data` zawiera bazę. Trwały katalog `deploy` przechowuje konfigurację, nie samą zawartość wolumenu PostgreSQL.

## 11. Sprawdź dostęp z domowej sieci

**Miejsce: Mac-serwer, następnie drugie urządzenie.**

Na Macu sprawdź:

```sh
curl -i http://127.0.0.1:8080/health
networksetup -listallhardwareports
```

W wyniku `networksetup` znajdź aktywny Ethernet albo Wi-Fi i odpowiadające mu `Device`, np. `en0`. Dla tego urządzenia odczytaj adres (zastąp `en0`, jeśli interfejs jest inny):

```sh
ipconfig getifaddr en0
```

Adres Maca sprawdzisz także w Ustawieniach systemowych → Sieć → aktywne połączenie → Szczegóły. W routerze ustaw rezerwację DHCP dla tego Maca, żeby adres nie zmieniał się po restarcie.

Na telefonie lub drugim komputerze w tej samej sieci otwórz:

```text
http://<adres-Maca>:8080
http://<adres-Maca>:8080/health
```

**Oczekiwany wynik:** strona Vue i JSON zdrowia są dostępne. Używaj `http://`, bez HTTPS. Telefon musi korzystać z domowego Wi-Fi, a nie transmisji komórkowej lub izolowanej sieci gościnnej.

Jeśli działa localhost, ale nie LAN, sprawdź **Expose ports to LAN** w OrbStack, zgodę na ruch przychodzący dla OrbStack w firewallu macOS, poprawny adres i izolację urządzeń w routerze. Nie trzeba ustawiać przekierowania portów na routerze do Internetu.

## 12. Zarejestruj self-hosted runnera

**Miejsce: GitHub i Mac-serwer. `DEPLOY_ENABLED` nadal ma wartość `false`.**

Na GitHub: Settings → Actions → Runners → New self-hosted runner. Wybierz **macOS** i **ARM64**. GitHub pokaże aktualne komendy pobrania, sprawdzenia archiwum i rejestracji — korzystaj z nich, aby nie instalować starej wersji. [Instrukcja rejestracji](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners).

Na Macu przygotuj katalog:

```sh
mkdir -p "$HOME/Serwer/penny-path/actions-runner"
cd "$HOME/Serwer/penny-path/actions-runner"
```

Wykonaj z ekranu GitHub komendy pobrania, weryfikacji i rozpakowania. Jeśli pokazują tworzenie katalogu `actions-runner`, nie twórz zagnieżdżonego katalogu drugi raz — jesteś już we właściwym miejscu.

Uruchom dostarczone przez GitHub `./config.sh --url ... --token ...`. W interaktywnych pytaniach ustaw:

| Pole | Wartość |
| --- | --- |
| Runner group, jeśli pojawi się pytanie | Domyślna dostępna grupa |
| Runner name | `pennypath-m1` |
| Additional labels | `pennypath-deploy` |
| Work folder | `_work` |

Zachowaj domyślne etykiety `self-hosted`, `macOS`, `ARM64`. Workflow wymaga **wszystkich czterech** etykiet. Nie wybieraj opcji wyłączającej domyślne etykiety. Token rejestracyjny z ekranu GitHub wygasa po godzinie; jeśli wygasł, wygeneruj nowy, odświeżając procedurę. Nie zapisuj go w repozytorium.

Uruchom runnera na próbę:

```sh
./run.sh
```

**Oczekiwany wynik:** `Connected to GitHub`, `Listening for Jobs`, a w Settings → Actions → Runners stan `Idle`.

Naciśnij **Ctrl+C**, aby zakończyć próbę. Na tym samym koncie, w zalogowanej sesji macOS, zainstaluj usługę:

```sh
cd "$HOME/Serwer/penny-path/actions-runner"
./svc.sh install
./svc.sh start
./svc.sh status
```

Użyj procedury [GitHub dla macOS](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/configure-the-application?platform=mac); nie uruchamiaj równocześnie `run.sh` i usługi. **Oczekiwany wynik:** usługa działa, runner wraca do `Idle` i pozostaje dostępny po zamknięciu Terminala.

## 13. Sprawdź środowisko używane przez workflow

**Miejsce: Mac-serwer.**

Workflow wybiera kontekst `orbstack` i dodaje katalog narzędzi do PATH. Przed włączeniem wdrożeń sprawdź ten wariant bez polegania na konfiguracji Twojej powłoki:

```sh
/bin/bash --noprofile --norc -c '
  set -e
  export PATH="$HOME/.orbstack/bin:/opt/homebrew/bin:$PATH"
  export DOCKER_CONTEXT=orbstack
  docker info >/dev/null
  docker compose version
  test -r "$HOME/Serwer/penny-path/deploy/compose.yaml"
  test -r "$HOME/Serwer/penny-path/deploy/.env"
  test -r "$HOME/Serwer/penny-path/deploy/release.env"
  printf "Dostęp do Docker i konfiguracji wdrożenia działa.\n"
'
```

**Oczekiwany wynik:** pozytywny komunikat. Jeśli nie działa, sprawdź konto runnera, OrbStack, PATH i lokalizację plików. `HOME` usługi musi wskazywać konto, na którym utworzyłeś `~/pennypath/deploy`.

## 14. Włącz i uruchom automatyczne wdrożenie

**Miejsce: GitHub.**

1. Settings → Secrets and variables → Actions → Variables → edytuj `DEPLOY_ENABLED` na dokładnie **`true`** (małe litery).
2. Actions → Containers → Run workflow → wybierz **`main`** → Run workflow.
3. Poczekaj na zielone `verify`, `publish` i **`deploy`**.

Jeśli nie widać Run workflow, sprawdź, czy workflow z `workflow_dispatch` znajduje się na domyślnej gałęzi repozytorium i domyślna gałąź to `main`.

Na Macu sprawdź:

```sh
cd "$HOME/Serwer/penny-path/deploy"
docker compose --env-file .env --env-file release.env ps
cat release.env
app_container=$(docker compose --env-file .env --env-file release.env ps -q app)
docker inspect --format '{{.Config.Image}}' "$app_container"
docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$app_container"
curl -fsS http://127.0.0.1:8080/health
```

**Oczekiwany wynik:** obraz kontenera zgadza się z `APP_IMAGE` w `release.env`, etykieta revision odpowiada SHA uruchomienia workflow, aplikacja jest zdrowa.

Od tej chwili push do `main`, w tym merge PR, uruchamia cały przepływ. Automatyzacja serializuje wydania dla `main` i nie przerywa aktywnego wdrożenia. GitHub może zastąpić starsze oczekujące uruchomienie nowszym — nie traktuj tego mechanizmu jako gwarancji wdrożenia każdego pośredniego commita.

## 15. Sprawdź rzeczywisty PR i merge

**Miejsce: komputer roboczy i GitHub.**

Na komputerze roboczym, po zsynchronizowaniu `main`, przygotuj małą zmianę README:

```sh
git switch main
git pull --ff-only origin main
git switch -c test/deployment-pipeline
```

Edytuj README, np. dodaj informację o wykonanym teście pipeline’u. Następnie:

```sh
git add README.md
git commit -m "Verify deployment pipeline through a pull request"
git push -u origin test/deployment-pipeline
```

Na GitHub utwórz PR do `main`:

1. Poczekaj na `verify`. PR ma tylko sprawdzenia; `publish` i `deploy` są pomijane.
2. Sprawdź, że uruchomiony obraz Maca nie zmienił się w czasie PR.
3. Zrób merge.
4. Otwórz nowe uruchomienie Containers dla `main` i poczekaj na trzy zielone zadania.
5. Powtórz komendy inspekcji z kroku 14 i porównaj revision z SHA nowego wydania.

**Oczekiwany wynik:** PR nie wdraża, merge aktualizuje aplikację, a `release.env` zapisuje poprzedni digest. Etykieta revision pozwala sprawdzić zmianę wydania nawet wtedy, gdy edytowałeś tylko dokumentację.

Opcjonalnie ustaw ochronę `main`, wymagając PR i pozytywnego checku `verify`. Wybierz nazwę checku z faktycznie wykonanego PR; dostępność reguł zależy od rodzaju repozytorium i planu GitHub.

## 16. Przećwicz ręczny rollback

**Miejsce: GitHub i Mac-serwer. Potrzebujesz dwóch różnych wdrożonych digestów.**

Najpierw ustaw `DEPLOY_ENABLED=false` i poczekaj na zakończenie już uruchomionych zadań `deploy`. Ta zmienna nie zatrzymuje wydania, które już trwa.

Na Macu:

```sh
cd "$HOME/Serwer/penny-path/deploy"
cat release.env
cd "$HOME/Serwer/penny-path/repo"
bash scripts/deploy.sh rollback
python3 scripts/smoke-test.py http://127.0.0.1:8080
```

**Oczekiwany wynik:** aplikacja działa na wcześniejszym digestcie, `APP_IMAGE` i `PREVIOUS_IMAGE` zamieniły się miejscami, baza pozostaje uruchomiona.

Drugie `bash scripts/deploy.sh rollback` przywróci wydanie, z którego właśnie się cofnąłeś. Po ćwiczeniu wróć do oczekiwanego wydania, sprawdź zdrowie i ustaw `DEPLOY_ENABLED=true`.

Automatyczny rollback po niesprawnym obrazie jest sprawdzany przez test kontenerowy w CI. Na pierwszym wydaniu nie ma czego przywracać. Rollback dotyczy aplikacji; przyszłe migracje schematu bazy wymagają własnej zgodności z poprzednią wersją.

## 17. Zrób kopię PostgreSQL i sprawdź odtworzenie

**Miejsce: Mac-serwer.**

```sh
cd "$HOME/Serwer/penny-path/deploy"
umask 077
mkdir -p backups
chmod 700 backups
backup_file="backups/pennypath-$(date +%Y%m%d-%H%M%S).dump"
```

Utwórz kopię i oznacz ją jako kompletną dopiero po udanym eksporcie:

```sh
if docker compose --env-file .env --env-file release.env exec -T postgres \
  pg_dump -U pennypath -d pennypath -Fc > "$backup_file.tmp"; then
  mv "$backup_file.tmp" "$backup_file"
  chmod 600 "$backup_file"
  printf 'Kopia zapisana: %s\n' "$backup_file"
else
  printf 'Eksport nie powiódł się. Plik .tmp nie jest poprawną kopią.\n'
fi
```

Sprawdź zawartość kompletnej kopii:

```sh
docker compose --env-file .env --env-file release.env exec -T postgres \
  pg_restore --list < "$backup_file"
```

**Oczekiwany wynik:** narzędzie odczytuje archiwum bez błędu. Na obecnym starterze baza nie ma jeszcze tabel domenowych, więc lista będzie mała. [PostgreSQL — pg_dump](https://www.postgresql.org/docs/18/app-pgdump.html).

Odtwórz kopię do osobnej bazy kontrolnej:

```sh
docker compose --env-file .env --env-file release.env exec -T postgres \
  createdb -U pennypath pennypath_restore_check
docker compose --env-file .env --env-file release.env exec -T postgres \
  pg_restore -U pennypath -d pennypath_restore_check --exit-on-error < "$backup_file"
docker compose --env-file .env --env-file release.env exec -T postgres \
  psql -U pennypath -d pennypath_restore_check -c '\dt'
```

**Oczekiwany wynik:** przywracanie kończy się bez błędów. Brak tabel na starterze jest poprawny. Gdy powstaną wydatki, dodatkowo porównuj liczbę rekordów i sumy. Po zakończeniu próby usuń wyłącznie bazę kontrolną:

```sh
docker compose --env-file .env --env-file release.env exec -T postgres \
  dropdb -U pennypath pennypath_restore_check
```

Przenieś zaszyfrowaną kopię poza Maca, np. na drugi komputer albo do wybranego magazynu kopii. Zachowaj osobno konfigurację potrzebną do odtworzenia serwera. Sam wolumen OrbStack oraz katalog `backups` na tym samym dysku nie zastępują kopii poza urządzeniem.

### Opcjonalnie: codzienny eksport

Na serwerze utwórz poniższy skrypt. Nie trafia do repozytorium:

```sh
cat > "$HOME/Serwer/penny-path/deploy/backup.sh" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
umask 077
export PATH="$HOME/.orbstack/bin:/opt/homebrew/bin:/usr/bin:/bin:$PATH"
export DOCKER_CONTEXT=orbstack
cd "$HOME/Serwer/penny-path/deploy"
mkdir -p backups
backup_file="backups/pennypath-$(date +%Y%m%d-%H%M%S).dump"
docker compose --env-file .env --env-file release.env exec -T postgres \
  pg_dump -U pennypath -d pennypath -Fc > "$backup_file.tmp"
mv "$backup_file.tmp" "$backup_file"
printf 'Backup OK: %s\n' "$backup_file"
SH
chmod 700 "$HOME/Serwer/penny-path/deploy/backup.sh"
bash "$HOME/Serwer/penny-path/deploy/backup.sh"
```

Wykonaj `crontab -e` na tym samym koncie i dodaj wpis, zastępując `<konto>` nazwą z kroku 1:

```cron
0 3 * * * /bin/bash /Users/<konto>/pennypath/deploy/backup.sh >> /Users/<konto>/pennypath/deploy/backups/backup.log 2>&1
```

Wpis robi eksport o 03:00, kiedy Mac działa i OrbStack jest uruchomiony. Sprawdź `crontab -l` oraz po pierwszym wykonaniu `backups/backup.log` i nowy plik `.dump`. Ten przykład nie przenosi kopii poza Maca, nie usuwa starszych kopii i nie wysyła powiadomień; dobierz retencję i transfer do swojego miejsca przechowywania. Jeśli macOS ogranicza cron, sprawdź zgody systemowe i log zadania.

## 18. Sprawdź restart Maca

**Miejsce: GitHub, Mac-serwer i drugie urządzenie.**

1. Na czas próby ustaw `DEPLOY_ENABLED=false` i poczekaj na zakończenie aktywnego wydania.
2. Zrestartuj Maca.
3. Odblokuj FileVault, jeśli jest używany, i zaloguj konto serwera.
4. Sprawdź, czy OrbStack uruchomił się automatycznie.
5. Sprawdź `docker info`, stan Compose, `/health`, usługę runnera i jego status na GitHub.
6. Otwórz aplikację z drugiego urządzenia.
7. Przywróć `DEPLOY_ENABLED=true`, gdy wszystko działa.

Komendy kontrolne:

```sh
cd "$HOME/Serwer/penny-path/deploy"
docker compose --env-file .env --env-file release.env ps
curl -fsS http://127.0.0.1:8080/health
cd "$HOME/Serwer/penny-path/actions-runner"
./svc.sh status
```

**Oczekiwany wynik:** oba kontenery są healthy, runner jest Idle, adres LAN działa. Kontenery zatrzymane ręcznie przez `stop` albo usunięte przez `down` wymagają ponownego uruchomienia. Nie oczekuj dostępności aplikacji przed zalogowaniem użytkownika w tym wariancie.

## 19. Codzienna obsługa

**Miejsce: Mac-serwer.** Zawsze zaczynaj od:

```sh
cd "$HOME/Serwer/penny-path/deploy"
```

Stan, ostatnie logi i obserwacja logów aplikacji:

```sh
docker compose --env-file .env --env-file release.env ps
docker compose --env-file .env --env-file release.env logs --tail 100 app postgres
docker compose --env-file .env --env-file release.env logs -f --tail 50 app
```

Ctrl+C kończy obserwację logów, nie zatrzymuje kontenerów.

Ponowne uruchomienie samej aplikacji:

```sh
docker compose --env-file .env --env-file release.env restart app
```

Uruchomienie usług po świadomym zatrzymaniu:

```sh
docker compose --env-file .env --env-file release.env up -d --wait --wait-timeout 120
```

Przed planowanym zatrzymaniem usług lub ręcznym wdrożeniem wyłącz `DEPLOY_ENABLED` i zaczekaj na zakończenie aktywnego zadania. `stop`/`down` zachowują wolumen, ale `down --volumes` go usuwa. Nie używaj czyszczenia wolumenów ani resetu Docker w OrbStack jako sposobu naprawy wdrożenia.

Automatyczne wydania aktualizują tylko obraz aplikacji. Nie aktualizują PostgreSQL, OrbStack, macOS ani trwałego `compose.yaml`. Jeśli zmienisz Compose w repozytorium, porównaj go z kopią w `deploy`, zachowaj poprzednią konfigurację i zsynchronizuj świadomie. Zmiana major PostgreSQL jest osobnym zadaniem z kopią i procedurą migracji.

## 20. Rozwiązywanie typowych problemów

| Objaw | Co sprawdzić |
| --- | --- |
| `verify` nie startuje lub runner GitHub ARM64 jest niedostępny | Actions są włączone; runner `ubuntu-24.04-arm` jest dostępny dla Twojego repozytorium/konta; limit minut nie został wyczerpany |
| `publish` zgłasza brak uprawnień | W logu używany jest `GITHUB_TOKEN`; workflow ma `packages: write`; pakiet należy do `pawelkam` i jest połączony z PennyPath; polityka organizacji nie blokuje publikacji |
| `deploy` ma stan `Skipped` | Uruchomienie dotyczy `main`, nie PR; `publish` się udało; repozytoryjna Variable `DEPLOY_ENABLED` to dokładnie `true` |
| `deploy` czeka na runnera | Runner ma stan Idle/Online i komplet etykiet: `self-hosted`, `macOS`, `ARM64`, `pennypath-deploy` |
| `docker info` działa w Terminalu, ale nie w workflow | Konto/HOME usługi runnera, PATH i kontekst `orbstack`; powtórz krok 13 |
| Skrypt zgłasza brak konfiguracji | Pliki są w `~/pennypath/deploy` konta runnera, a nie wyłącznie w checkoutcie repozytorium |
| Baza nie staje się healthy | Logi `postgres`, niepusty plik hasła i prawa odczytu; nie inicjalizuj ponownie istniejącego wolumenu |
| Port 8080 jest zajęty | `lsof -nP -iTCP:8080 -sTCP:LISTEN`; ustal, jaki proces używa portu. Jeśli świadomie zmienisz `APP_PORT`, dostosuj adres testów i LAN |
| Działa localhost, nie działa LAN | Expose ports to LAN, firewall dla OrbStack, aktywny adres interfejsu, wspólna sieć i izolacja klientów |
| Obraz nie pobiera się anonimowo | Widoczność pakietu GHCR to Public; pełny SHA został opublikowany |
| Healthcheck wdrożenia nie przechodzi | Logi aplikacji; oczekiwany digest; lokalny `/health`; pipeline powinien zgłosić błąd i odtworzyć ostatnie działające wydanie |
| `No previous release is recorded` | Masz tylko jedno wydanie; wykonaj drugie udane wdrożenie innego digestu |
| `Another deployment holds .../.deploy-lock` | Sprawdź aktywne Actions i procesy wdrożenia; poczekaj na ich zakończenie. Po awarii wykonaj procedurę poniżej |
| `Running app differs from release.env` | Ktoś zmienił obraz ręcznie albo wdrożenie zostało przerwane; uzgodnij kontener ze stanem zapisanym w `release.env` |

### Przywrócenie zapisanego stanu po przerwanym wdrożeniu

Najpierw wyłącz `DEPLOY_ENABLED`, poczekaj na zakończenie aktywnych zadań i potwierdź, że na Macu nie działa `deploy.sh`. Odczytaj `release.env` oraz obraz działającego kontenera. Jeśli istnieje poprawny zapis poprzednio działającego wydania, możesz odtworzyć samą aplikację z tego zapisu:

```sh
cd "$HOME/Serwer/penny-path/deploy"
unset APP_IMAGE
cat release.env
docker compose --env-file .env --env-file release.env up -d --no-deps --pull never app
```

Poczekaj na healthy, sprawdź `/health` oraz zgodność obrazu z `APP_IMAGE` zapisanym w pliku. Dopiero gdy potwierdzisz, że nie trwa wdrożenie i blokada jest pozostałością awarii, usuń pusty katalog blokady:

```sh
rmdir "$HOME/Serwer/penny-path/deploy/.deploy-lock"
```

Nie edytuj digestów tylko po to, żeby ominąć kontrolę stanu. Jeśli nie ma jeszcze `release.env` (pierwsze wydanie się nie udało), przejrzyj logi zatrzymanej aplikacji i napraw przyczynę przed kolejną inicjalizacją.

## Lista odbioru

- [ ] OrbStack i Docker Compose działają na Macu ARM64.
- [ ] `main` zawiera aplikację, lockfile, Dockerfile, Compose, skrypty i workflow.
- [ ] Pierwsze `verify` i `publish` są zielone.
- [ ] Pakiet GHCR jest publiczny i pobiera się anonimowo.
- [ ] Konfiguracja i hasło są w trwałym katalogu na serwerze.
- [ ] Pierwsze ręczne wdrożenie i test HTTP przeszły.
- [ ] Strona działa z drugiego urządzenia w LAN.
- [ ] Runner działa jako usługa na tym samym koncie co OrbStack.
- [ ] Ręczne uruchomienie workflow na `main` wdraża obraz.
- [ ] PR wykonuje tylko sprawdzenia, merge aktualizuje aplikację.
- [ ] Ręczny rollback został przećwiczony.
- [ ] Kopia PostgreSQL daje się odtworzyć, a kompletna kopia jest poza Macem.
- [ ] Po restarcie i zalogowaniu kontenery, runner i dostęp LAN wracają.

Wyniki swojej instalacji możesz dopisać do [verification.md](verification.md), oddzielając je od lokalnych testów wykonanych wcześniej. Docelowy serwer jest skonfigurowany dopiero wtedy, gdy powyższe sprawdzenia faktycznie przejdą.


## 21. Dodaj HTTPS do już działającego wdrożenia

**Miejsce: Mac-serwer, GitHub i urządzenia w LAN.** Ta sekcja rozszerza zakończoną instalację z poprzednich punktów. Zacznij tutaj, jeśli aplikacja, PostgreSQL, OrbStack i runner już działają. Wcześniejsza instrukcja opisuje wykonany etap HTTP; poniżej przechodzisz na `https://<adres-Maca>:8443` w tym samym projekcie Compose, z istniejącą bazą, hasłem i historią wydań.

### 21.0. Przekaż lokalne zmiany na Maca do testów przed commitem

Jeżeli zmiany HTTPS nie są jeszcze w GitHub, `git pull` na serwerze ich nie pobierze. Do weryfikacji przed commitem użyj paczki aktualnych źródeł przygotowanej w workspace: `PennyPath-https-20261111.tar.gz`. Zawiera bieżące wersje plików projektu, w tym nowe pliki testów i Caddyfile; pomija `.git`, lokalne sekrety oraz wygenerowane zależności/buildy.

Przenieś paczkę na Maca przez AirDrop do Pobranych. Rozpakuj ją w osobnym katalogu testowym, poza dotychczasowym checkoutem i katalogiem wdrożenia:

```sh
checks_dir=$(mktemp -d "$HOME/Serwer/penny-path/https-checks.XXXXXX")
tar -xzf "$HOME/Downloads/PennyPath-https-20261111.tar.gz" -C "$checks_dir"
cd "$checks_dir/PennyPath-https"
docker build --platform linux/arm64 -t pennypath:ci .
```

Po udanym buildzie, w tym samym katalogu, wykonaj:

```sh
bash scripts/test-compose.sh
```

Test Compose sam tworzy izolowaną bazę, proxy i syntetyczne konta. Nie wymaga pliku `.env` serwera i nie instaluje CA w systemie. Korzysta z testowych portów 5000/18080/18443 i podsieci `172.30.91.0/24`; wykryta kolizja kończy test bez zmieniania istniejących sieci. Wyniki obu komend zamykają kryteria 4.5–4.6. Właściwą aktualizację działającego wdrożenia wykonaj według dalszych punktów po zatwierdzeniu kodu i publikacji obrazu.

### 21.1. Wstrzymaj automatyczne wydania i zachowaj obecną konfigurację

W GitHub ustaw `DEPLOY_ENABLED=false` i poczekaj na zakończenie aktywnego zadania `deploy`. Runner i działające kontenery mogą pozostać uruchomione.

Na Macu ustaw ścieżki do **istniejącej** instalacji. Domyślny katalog obecnego `deploy.sh` to `$HOME/Serwer/penny-path/deploy`; jeśli używasz innego, wpisz go w `deploy_dir`. Nie przenoś wdrożenia do nowego katalogu na potrzeby HTTPS.

```sh
repo_dir="$HOME/Serwer/penny-path/repo"
deploy_dir="${DEPLOY_DIR:-$HOME/Serwer/penny-path/deploy}"
test -f "$deploy_dir/.env"
test -f "$deploy_dir/release.env"
test -f "$deploy_dir/compose.yaml"
unset APP_IMAGE

pp_compose() {
  docker compose --project-name pennypath \
    --env-file "$deploy_dir/.env" --env-file "$deploy_dir/release.env" \
    -f "$deploy_dir/compose.yaml" "$@"
}

pp_compose ps
postgres_before=$(pp_compose ps -q postgres)
test -n "$postgres_before"
umask 077
https_backup="$deploy_dir/backups/before-https-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$https_backup"
chmod 700 "$https_backup"
cp "$deploy_dir/.env" "$deploy_dir/release.env" "$deploy_dir/compose.yaml" "$https_backup/"
if [ -f "$deploy_dir/deploy/Caddyfile" ]; then
  cp "$deploy_dir/deploy/Caddyfile" "$https_backup/Caddyfile"
fi
```

`pennypath` musi być nazwą dotychczasowego projektu. Jeśli wcześniej użyłeś innej, zachowaj ją w funkcji oraz przekaż tę samą przez `COMPOSE_PROJECT_NAME` do `deploy.sh`. Wykonaj i sprawdź kopię/odtworzenie PostgreSQL według sekcji 17, przed zmianą konfiguracji. Zachowaj obecny sekret bazy; HTTPS nie wymaga nowego hasła ani ponownej inicjalizacji PostgreSQL.

### 21.2. Dopisz ustawienia HTTPS do istniejącego .env

Ustal stały adres IP Maca (np. rezerwacja DHCP) albo nazwę rozwiązywaną na urządzeniach LAN. Poniższy IP jest przykładem — zastąp go własnym.

Otwórz **dotychczasowy** `$deploy_dir/.env`. Dopisz poniższe pola lub uaktualnij je, jeżeli już istnieją; każde pole powinno występować tylko raz:

```dotenv
LAN_HOST=192.168.1.50
HTTPS_PORT=8443
EDGE_SUBNET=172.30.90.0/24
PROXY_IP=172.30.90.2
```

Zachowaj `POSTGRES_PASSWORD_FILE`, `APP_PORT` i inne ustawienia. Nie zastępuj tego pliku przez `.env.example`. LAN_HOST to sam IP lub hostname, bez `https://`, portu i ścieżki. Przed użyciem podsieci sprawdź `docker network ls` i pule w `docker network inspect <nazwa>`; jeśli `172.30.90.0/24` koliduje z istniejącą siecią, dobierz wolną podsieć i zmień **razem** EDGE_SUBNET/PROXY_IP. Nie usuwaj istniejących sieci.

```sh
chmod 600 "$deploy_dir/.env"
```

Sekret app/migratora musi być czytelny przez użytkownika kontenera bez roota: plik 0444 w prywatnym katalogu hosta 0700. Jeśli ten układ jest już przygotowany, zachowaj go. Jeśli nadal masz stary `./postgres-password`, wykonaj wyłącznie „Aktualizacja sekretu istniejącej instalacji” z sekcji 8, zachowując zawartość hasła i aktualizując ścieżkę w obecnym `.env`.

### 21.3. Zsynchronizuj Compose i Caddyfile

W istniejącym checkoutcie przygotuj wersję zmiany HTTPS, której obraz przeszedł `verify` i został opublikowany do GHCR. Jeżeli aktualizujesz checkout z `main`, najpierw sprawdź lokalne zmiany przez `git status`, potem użyj `git pull --ff-only origin main` na `main`. Wystarcza istniejący klon repozytorium i dotychczasowy runner.

Po kopii z kroku 21.1 i przeglądzie różnic skopiuj dwa pliki:

```sh
mkdir -p "$deploy_dir/deploy"
cp "$repo_dir/compose.yaml" "$deploy_dir/compose.yaml"
cp "$repo_dir/deploy/Caddyfile" "$deploy_dir/deploy/Caddyfile"
pp_compose config --quiet
```

Jeśli masz własne zmiany w Compose, przenieś je do nowej konfiguracji przed zastąpieniem pliku. Zachowaj istniejący serwis PostgreSQL, nazwę projektu, nazwę wolumenu bazy i źródło sekretu. Ten krok tylko synchronizuje pliki; nie odtwarza bazy.

Nowa konfiguracja dodaje proxy Caddy, osobną sieć edge oraz wolumeny `app_keys`, `proxy_data` i `proxy_config`. Aplikacja ufa Forwarded-For/Proto wyłącznie od PROXY_IP. Jej port HTTP pozostaje na `127.0.0.1:${APP_PORT:-8080}` do kontroli zdrowia. Dostęp użytkowników przechodzi na HTTPS_PORT; w OrbStack pozostaw Expose ports to LAN i zezwól w firewallu na TCP 8443 (lub wybrany HTTPS_PORT).

### 21.4. Zaktualizuj aplikację obsługującą proxy i trwałe klucze

Nowy obraz musi zawierać obsługę zaufanego proxy i magazynu kluczy — samo uruchomienie Caddy przed starszym backendem nie wystarczy. Użyj digestu opublikowanego wydania HTTPS, zastępując poniższą wartość rzeczywistym adresem:

```sh
release_image='ghcr.io/pawelkam/pennypath@sha256:<64-znakowy-digest-wydania-HTTPS>'
DEPLOY_DIR="$deploy_dir" bash "$repo_dir/scripts/deploy.sh" "$release_image"
```

Oczekiwany wynik: `Deployed ...`; dotychczasowy kontener PostgreSQL pozostaje uruchomiony. Aktualny skrypt sam uruchamia idempotentne `--migrate` z nowego obrazu **przed** wymianą app. Błąd migracji zachowuje poprzedni app i release.env; przerwij procedurę i sprawdź konfigurację, zamiast inicjalizować bazę ponownie. Zwykły start API nie migruje.

Sprawdź zachowanie bazy, odczyt sekretu i prawa katalogu kluczy:

```sh
test "$(pp_compose ps -q postgres)" = "$postgres_before"
pp_compose exec -T app sh -c \
  'test "$(id -u)" != 0 && test -r /run/secrets/postgres_password && cat /run/secrets/postgres_password >/dev/null && test -w /var/lib/pennypath/keys && test "$(stat -c %a /var/lib/pennypath/keys)" = 700 && test "$(stat -c %u /var/lib/pennypath/keys)" = "$(id -u)"'
```

Oczekiwany kod to 0, bez wypisania hasła. Wolumen app_keys przechowuje klucze Data Protection aplikacji `PennyPath`. Po tym przejściu zachowuj go podczas kolejnych wydań; odtwarzanie zgodnego app nie powinno wymagać ponownego logowania. Jeśli wcześniej sesje używały nietrwałych kluczy, pierwsze przejście na ten magazyn może wymagać ponownego logowania.

### 21.5. Uruchom proxy i wyeksportuj certyfikat CA

```sh
pp_compose up -d --no-deps proxy
pp_compose ps
pp_compose cp proxy:/data/caddy/pki/authorities/local/root.crt "$deploy_dir/pennypath-root.crt"
openssl x509 -in "$deploy_dir/pennypath-root.crt" -noout -fingerprint -sha256
```

Oczekiwane: app i PostgreSQL healthy, proxy uruchomione i plik root.crt dostępny po starcie Caddy. Jeżeli certyfikat nie powstał, sprawdź `pp_compose logs --tail 50 proxy` i ponów eksport po uruchomieniu proxy. Caddy używa `tls internal`, bez publicznego DNS/ACME i bez przekierowania publicznego HTTP.

Zachowuj wolumeny proxy_data i proxy_config z CA oraz app_keys z kluczami sesji. Kopie tych magazynów przechowuj zaszyfrowane jak inne sekrety serwera. Automatyczny deploy aktualizuje app; proxy i Caddyfile utrzymujesz tym osobnym krokiem.

### 21.6. Zaufaj CA na urządzeniach i sprawdź HTTPS

Skopiuj na urządzenia LAN **wyłącznie** `$deploy_dir/pennypath-root.crt`. Porównaj fingerprint z wynikiem na Macu-serwerze i dodaj certyfikat CA do zaufanych w systemie/przeglądarce. Na macOS użyj Dostępu do pęku kluczy; na iOS po instalacji profilu włącz pełne zaufanie w Ustawienia → Ogólne → To urządzenie → Ustawienia zaufania certyfikatów. Prywatny `root.key` i cały magazyn `/data` pozostają na serwerze. Odbiór wymaga połączenia bez ostrzeżeń certyfikatu i bez wyłączania weryfikacji TLS.

Na Macu ustaw adres odpowiadający LAN_HOST / HTTPS_PORT z `.env` i uruchom kontrole:

```sh
https_url='https://192.168.1.50:8443'
curl --fail --cacert "$deploy_dir/pennypath-root.crt" "$https_url/health"
python3 "$repo_dir/scripts/smoke-test.py" "$https_url" --ca-cert "$deploy_dir/pennypath-root.crt"
python3 "$repo_dir/scripts/test-auth.py" "$https_url" --ca-cert "$deploy_dir/pennypath-root.crt"
```

Test auth tworzy syntetyczne konta. W dwóch osobnych kontekstach przeglądarki otwórz `$https_url/app`: sprawdź rejestrację A/B, osobne logowanie, powrót do chronionej strony, własny email, sesję po ponownym otwarciu przeglądarki i wylogowanie. Rejestracja nie wymaga potwierdzenia emaila; sesja trwa maksymalnie 7 dni bez przedłużania. Sprawdź też klawiaturę, mobile/desktop i brak prywatnego widoku przed potwierdzeniem sesji. Odzyskanie hasła pozostaje w S-08, wydatki w S-02.

Jeśli HTTPS działa tylko na serwerze, sprawdź zgodność LAN_HOST z adresem certyfikatu, trust CA, HTTPS_PORT/firewall, Expose ports to LAN i izolację urządzeń w Wi-Fi. Po zmianie LAN_HOST odtwórz samo proxy: `pp_compose up -d --no-deps --force-recreate proxy`, zachowując CA.

### 21.7. Wznów dotychczasowy pipeline

Po udanym odbiorze przywróć `DEPLOY_ENABLED=true`. Kolejne wydania używają istniejącego runnera i projektu, migrują z nowego obrazu i wymieniają wyłącznie app. Ręczny `deploy.sh rollback` pomija migrator starszego obrazu i zachowuje bazę, klucze oraz CA; schemat nie jest cofany. Dla niestandardowego deploy_dir zapewnij ten sam DEPLOY_DIR w środowisku kroku deploy workflow.

Wynik tej aktualizacji: dotychczasowa aplikacja jest dostępna w LAN przez HTTPS, lokalny HTTP służy kontroli zdrowia, a baza/sekret i historia wdrożeń są zachowane. Nie powtarzasz instalacji OrbStack, rejestracji runnera, tworzenia bazy ani pierwszej konfiguracji GitHub z punktów 1–20.
