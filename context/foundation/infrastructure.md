---
project: PennyPath
researched_at: 2026-10-13
recommended_platform: "Domowy Mac M1 + OrbStack + Docker Compose (LAN)"
runner_up: Railway
context_type: mvp
tech_stack:
  language: "C# / TypeScript"
  framework: "ASP.NET Core Web API / Vue 3.5 / Vite 8.2"
  runtime: ".NET 10 w linux/arm64; SDK 10.0.401 i Node 24 >=24.12.0 podczas budowania"
---

## Recommendation

**Uruchom starter na domowym Macu M1 przez OrbStack i Docker Compose, dostępny w LAN pod `http://<adres-Maca>:8080`.**

Użytkownik wybrał własny serwer po porównaniu platform chmurowych, a następnie uzgodnił pierwszy etap przez HTTP w domowej sieci. Cloudflare Tunnel oraz publiczny dostęp są późniejszym etapem. Własny sprzęt eliminuje abonament za serwer aplikacji i bazę, umieszcza PostgreSQL obok API i wymaga samodzielnego utrzymania usług, kopii oraz aktualizacji. Pierwszą alternatywą jest Railway, jeśli przerwy domowego hostingu lub obsługa serwera okażą się nieakceptowalne.

Wariant operacyjny to jeden wieloetapowy obraz aplikacji `linux/arm64` (Node 24, SDK .NET 10.0.401, ASP.NET Core 10), uruchamiany bez roota, oraz PostgreSQL 18 z trwałym wolumenem pod `/var/lib/postgresql` i bez publikacji portu. Backend obsługuje statyczny build Vue, przykładowe API, `/health` i fallback SPA; nieznane `/api/*` zwracają 404. Hasło bazy pozostaje plikiem na serwerze. Integracja API z bazą, migracje, wydatki i uwierzytelnianie nie zostały jeszcze zaimplementowane. [OrbStack](https://docs.orbstack.dev/docker/), [oficjalny obraz PostgreSQL](https://hub.docker.com/_/postgres).

GitHub Actions sprawdza PR na `ubuntu-24.04-arm`. Push do `main` publikuje `ghcr.io/pawelkam/pennypath:<pełny-SHA>` i wdraża dokładny digest przez self-hosted runner z etykietą `pennypath-deploy`, gdy `DEPLOY_ENABLED=true`. Wydania są szeregowane; skrypt przechowuje bieżący i poprzedni digest w `~/pennypath/deploy` i przywraca aplikację po nieudanym healthchecku. Pierwsza publikacja wymaga ręcznego ustawienia pakietu GHCR jako publicznego. Instrukcja: [deploy-plan.md](../deployment/deploy-plan.md), stan weryfikacji: [verification.md](../deployment/verification.md).

Ten dokument rozstrzyga wcześniejszą wstępną propozycję Railway w `context/foundation/tech-stack.md`. Wybór domowego serwera jest jawną decyzją użytkownika, a nie zwycięstwem w punktacji preferującej zarządzane platformy.

Warunki pierwszego etapu: macOS wspierany przez OrbStack, start OrbStack po logowaniu, rejestracja runnera, test LAN i próba restartu Maca. Nie zakładamy działania przed zalogowaniem. Przed późniejszym publicznym udostępnieniem potrzebne będą własna domena, Cloudflare Tunnel, sprawdzone odtworzenie kopii poza Macem i kontrola dostępu wymagana przez PRD. Aktualna wersja macOS, RAM serwera, domena i miejsce przechowywania kopii nie zostały potwierdzone.

## Platform Comparison

### Kontekst i sposób oceny

Źródła projektu: `context/foundation/tech-stack.md`, `context/foundation/prd.md`, `frontend/package.json`, `backend/PennyPath.Api/PennyPath.Api.csproj`. Początkowo do 10 użytkowników; PRD nie ustala ruchu, wielkości danych ani poziomu dostępności. Lokalny warsztat ma SDK 10.0.401 i Node 26.10.0; nie jest to potwierdzenie konfiguracji domowego serwera.

Odpowiedzi z wywiadu:
- Zwykłe żądania i odpowiedzi; brak wymagania stale aktywnych workerów lub trwałych połączeń.
- Najniższy realny miesięczny koszt ma pierwszeństwo przed wygodą obsługi.
- Praktyczna znajomość Cloudflare Workers / Pages.
- Jeden region wystarczy.
- Aplikacja i baza u jednego dostawcy preferowane.

Późniejsze ustalenie: użytkownik posiada Mac M1 i wybiera go do hostingu; pierwszym etapem jest Docker Compose w LAN, a publikacja przez tunel zostaje odłożona. Baza na tym samym Macu zapewnia współlokalizację, ale jej utrzymanie należy do użytkownika. Poniższa punktacja zachowuje pierwotne badanie platform i wariantu docelowego z tunelem.

Pass = 2, Partial = 1, Fail = 0. Wagi pięciu kryteriów: CLI/API 3, zarządzana infrastruktura 3, dokumentacja czytelna dla agentów 2, stabilne wdrażanie/rollback 3, integracja MCP/CI 1. Maksimum podstawowe to 24. Jest to porównawcza heurystyka, nie pomiar wydajności ani gwarancja niezawodności.

| Platforma | CLI-first | Managed/Serverless | Dokumentacja dla agentów | Stabilne API wdrożeniowe | MCP / Integracja | Suma ważona |
| --- | --- | --- | --- | --- | --- | --- |
| Railway | Pass | Pass | Pass | Pass | Pass | 24/24 |
| Render | Pass | Pass | Pass | Pass | Pass | 24/24 |
| Cloudflare Workers + Containers + Neon | Pass | Pass | Pass | Partial | Pass | 21/24 |
| Fly.io + Managed Postgres | Pass | Pass | Pass | Pass | Partial | 23/24 |
| Vercel Services + Container Runtime | Pass | Pass | Pass | Partial | Partial | 20/24 |
| Netlify | Partial | Pass | Pass | Partial | Pass | 18/24 |
| Domowy Mac M1 + Cloudflare Tunnel | Pass | Fail | Pass | Partial | Partial | 14/24 |

Miękkie preferencje dodają do 8 punktów za realny koszt całego zestawu, do 4 za współlokalizację i 1 za znajomość Cloudflare. Jeden region nie daje dodatkowych punktów za globalny edge. Punkty kosztowe uwzględniają ograniczenia darmowych planów i niepewność: Railway 7, Render 4, Cloudflare 6, Fly z managed DB 0, Vercel beta 3, własny sprzęt 8. Własny sprzęt uzyskuje 8 przy założeniu, że już istnieje; prąd, domena, backup i obsługa pozostają kosztami.

| Platforma | Koszt 0–8 | Współlokalizacja 0–4 | Znajomość 0–1 | Wynik po preferencjach |
| --- | --- | --- | --- | --- |
| Railway | 7 | 4 | 0 | 35 |
| Render | 4 | 4 | 0 | 32 |
| Cloudflare Workers + Containers + Neon | 6 | 0 | 1 | 28 |
| Fly.io + Managed Postgres | 0 | 4 | 0 | 27 |
| Vercel Services + Container Runtime | 3 | 0 | 0 | 23 |
| Netlify | — | — | 0 | odrzucony |
| Domowy Mac M1 + Cloudflare Tunnel | 8 | 4 | 1 | 27 |

Twardy filtr dotyczy rzeczywistej możliwości uruchomienia obecnego backendu. Netlify odpada jako hosting całego stosu. Cloudflare przechodzi przez Containers GA oraz zewnętrzny PostgreSQL; same Workers/Pages nie uruchamiają ASP.NET Core. Vercel przechodzi warunkowo przez Container Runtime i Services beta. Rozwiązania beta nie są przedstawione jako stabilny odpowiednik zwykłego hostingu kontenerów.

### Ustalenia per platforma

**Domowy Mac M1.** Cały stos działa jako kontenery `linux/arm64` w OrbStack: .NET, PostgreSQL i statyczny frontend. CLI Docker Compose oraz GitHub Actions zapewniają aktualizację aplikacji i rollback digestu. Utrzymanie OS, DB, kopii i zasilania pozostaje lokalne — Managed dostaje Fail. Start wymaga działającego OrbStack na koncie użytkownika; zachowanie po restarcie wymaga sprawdzenia na faktycznym serwerze. Cloudflare Tunnel jest przewidziany dopiero po etapie LAN. [OrbStack](https://docs.orbstack.dev/docker/), [GitHub self-hosted runners](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners).

**Railway.** Obsługuje .NET przez Docker; aktualny poradnik pokazuje starsze obrazy, więc dla net10.0 potrzebne są obrazy Microsoft 10.0. Usługi i PostgreSQL można umieścić w Europie. Railway utrzymuje szablony bazy oraz provisioning, storage, sieć i poprawki bezpieczeństwa; użytkownik włącza backup/PITR i odpowiada za istotne ustawienia oraz aktualizacje major. Podstawowe operacje mają CLI/API, dokumentacja ma Markdown/llms.txt, a oficjalny MCP jest dostępny. Bezpośrednia komenda CLI rollback nie została potwierdzona, ale oficjalne API dokumentuje `deploymentRollback`; panel zapewnia praktyczną ścieżkę cofnięcia. Podstawowe funkcje nie mają na sprawdzonych stronach etykiety beta; brak etykiety nie jest osobnym komunikatem GA. Focused PR Environments są beta. [ASP.NET](https://docs.railway.com/guides/aspnet-core), [bazy](https://docs.railway.com/databases), [API rollback](https://docs.railway.com/integrations/api/api-cookbook), [MCP](https://docs.railway.com/ai/mcp-server), [Focused PR beta](https://docs.railway.com/environments).

**Render.** .NET działa przez Docker, Vue jako static site, a API i zarządzany PostgreSQL można umieścić we Frankfurcie. CLI, REST API rollbacku i oficjalny MCP obejmują operacje; dokumentacja ma Markdown/llms.txt. Podstawowa obsługa jest dobrze dopasowana do MVP, lecz płatny zestaw ma wyższy próg niż mały model Railway. Darmowe API usypia, a darmowy PostgreSQL wygasa po 30 dniach, więc trwały rejestr wymaga płatnej bazy. Płatny PG zapewnia PITR; retencja zależy od planu. Pełne środowiska podglądu PR wymagają płatnego planu i osobnych zasobów. Dokumentacyjny MCP jest experimental; ocena Pass integracji opiera się na właściwym MCP operacyjnym i API. [Obsługa runtime](https://render.com/docs/faq), [CLI](https://render.com/docs/cli), [API rollback](https://api-docs.render.com/reference/rollback-deploy), [MCP](https://render.com/docs/mcp-server), [dokumentacja agentowa](https://render.com/docs/llm-support).

**Cloudflare Workers + Containers + Neon.** Containers są GA od 2026-04-13 i uruchamiają Linux images, w tym .NET 10. Statyczny frontend oraz CLI Wrangler, dokumentacja Markdown/llms.txt i MCP są mocnymi stronami. PostgreSQL wymaga zewnętrznego dostawcy; D1 jest inną bazą, a Hyperdrive łączy z istniejącym PostgreSQL. Dysk kontenera jest nietrwały. Wdrożenie Workera i obrazu kontenera oraz cofnięcie całego zestawu wymagają weryfikacji kompatybilności wersji, więc stabilne wdrażanie oceniono Partial. Containers nie zapewniają automatycznych preview URLs; staging wymaga osobnego środowiska. Dla nowych projektów Cloudflare zaleca Workers zamiast Pages; Pages pozostaje dostępne. Domyślny Node obrazu Pages jest niższy niż wymagany przez projekt i wymaga jawnej zmiany. [GA Containers](https://developers.cloudflare.com/changelog/post/2026-04-13-containers-sandbox-ga/), [FAQ](https://developers.cloudflare.com/containers/faq/), [wdrażanie](https://developers.cloudflare.com/containers/guides/deploy/), [build Pages](https://developers.cloudflare.com/pages/configuration/build-image/).

**Fly.io.** Linux images i CLI dobrze obsługują .NET, trwałe procesy i WebSockety. Managed Postgres jest dostępny w europejskich regionach i daje backupy oraz pooling, ale minimalny koszt jest wysoki dla tego MVP. Tańszy PostgreSQL na własnej Machine wymaga samodzielnej obsługi i nie jest równoważny managed DB. `fly deploy`, `fly logs` oraz ponowny deploy poprzedniego obrazu zapewniają obsługę z terminala; dokumentacja ma llms.txt. Oficjalne funkcje MCP odnalezione podczas badania dotyczą Sprites i nie dowodzą pełnej obsługi Machines/MPG, stąd Partial. Nowe konta mają ograniczony trial, nie bezterminowy darmowy zestaw. Aktualny cennik zmienił się 2026-10-01; starsza strona cen w dokumentacji może pokazywać inne wartości. [Cennik](https://fly.io/pricing/), [zmiana cen](https://fly.io/pricing-update/), [Managed Postgres](https://fly.io/mpg/), [CLI deploy](https://fly.io/docs/flyctl/deploy/), [llms.txt](https://fly.io/llms.txt).

**Vercel.** Nowe Services i Container Runtime obsługują net10.0 poprzez OCI, lecz oba są beta na wszystkich planach. Dlatego platformy nie odrzucono jako niekompatybilnej, a stabilność wdrażania otrzymała Partial. Vue/Vite pasuje do hostingu statycznego. Kontener działa jako funkcja, bez trwałego lokalnego dysku; partnerzy Marketplace zapewniają PostgreSQL, a wspólna faktura nie oznacza jednego operatora infrastruktury. CLI wspiera deploy, rollback i logi, dokumentacja udostępnia Markdown/OpenAPI; obecne MCP nie ma etykiety beta, lecz nie potwierdzono osobnego komunikatu GA. Hobby jest dla projektów osobistych i niekomercyjnych; Pro zaczyna się od 20 USD. WebSockety są Public Beta — ta funkcja nie jest wymagana przez PennyPath. [Dokładny poradnik .NET 10](https://vercel.com/kb/guide/dot-net-asp-net-on-vercel-with-docker), [Container beta](https://vercel.com/docs/functions/runtimes/container), [MCP](https://vercel.com/docs/agent-resources/vercel-mcp), [Marketplace](https://vercel.com/docs/marketplace-storage).

**Netlify.** Frontend Vue pasuje, ale funkcje JS/TS/Go i Deno nie uruchamiają obecnego ASP.NET Core CLR. Brak oficjalnego ogólnego hostingu kontenerów oznacza odrzucenie całej platformy dla obecnego stosu. Netlify Database jest już GA od 2026-04-28 i zapewnia zarządzany PostgreSQL; nie rozwiązuje to backendu. Dokumentacja Markdown/llms.txt oraz oficjalny MCP są dostępne, natomiast nie potwierdzono komendy CLI rollback, więc operacyjność i stabilna pętla wdrożeń ocenione są ostrożniej. Nowe plany rozliczają kredyty również za buildy, transfer i DB. Dokumentacja DB nadal zawiera minioną datę ulgi storage, więc obecnego kosztu storage nie traktujemy jako potwierdzonego. [Runtime funkcji](https://docs.netlify.com/build/functions/configuration/), [Database GA](https://www.netlify.com/changelog/2026-04-28-netlify-database/), [ceny kredytowe](https://docs.netlify.com/manage/accounts-and-billing/billing/billing-for-credit-based-plans/credit-based-pricing-plans/), [DB billing](https://docs.netlify.com/build/data-and-storage/netlify-database/billing-and-usage/).

### Model kosztów

10–100 tys. małych żądań miesięcznie to wspólny scenariusz porównania, nie prognoza PRD. Nie wyznacza sam zużycia RAM, CPU, aktywnych godzin, transferu ani wielkości bazy. Kwoty USD są przed podatkami i dodatkowymi usługami; oszacowania są jawnie warunkowe.

| Wariant | Cały zestaw w przyjętym scenariuszu | Założenia i ograniczenia |
| --- | --- | --- |
| Domowy M1 + Tunnel | 0 USD abonamentu hostingowego i tunelu; prąd, domena oraz kopie osobno | Sprzęt już posiadany. Dodatkowe 10 W przez 30 dni daje 7,2 kWh; pomnóż rzeczywisty pobór przez własną cenę energii. Koszt czasu utrzymania nie został wyceniony. |
| Railway Hobby | około 5,60–6,05 USD; wariant większej pamięci 11,20–11,65 USD | API obsługuje także build Vue; API+PG średnio 0,5 GB RAM i 0,02 vCPU, wolumen 1 GB, transfer 1–10 GB. Drugi wariant: 1 GB RAM, 0,05 vCPU. Backup/PITR dodatkowo. |
| Render płatny | około 13,30–14,05 USD | API 512 MB za 7 USD, PG 256 MB za 6 USD, 1 GB storage za 0,30 USD, static site i Hobby workspace 0 USD. Transfer 1–10 GB przy 5 GB wliczonych i 0,15 USD/GB nadwyżki. |
| Cloudflare Containers + Neon | około 5,05 USD przy krótkiej aktywności; około 12,04 USD przy ciągłej aktywności, plus przekroczenia i ewentualna płatna DB | Kontener 1 GiB RAM/4 GB dysku: 30 vs 730 h aktywności, CPU w limicie w pierwszym wariancie. Neon Free tylko przy zachowaniu 100 CU-h i 1 GB storage. Dochodzą możliwe Workers/DO/CPU oraz DB. |
| Fly.io + managed PG | około 45,21–45,39 USD | Machine 512 MB za 4,39 USD, managed PG minimum 38 USD + 10 GB storage × 0,28 USD, europejski egress 1–10 GB. Parametry i cenę regionu potwierdź przed zakupem. |
| Vercel + partner PG | Hobby od 0 USD dla kwalifikującego się osobistego projektu; Pro od 20 USD, plus DB i compute/transfer | Beta kontenerów i brak pomiaru CPU/RAM nie pozwalają obiecać pełnego rachunku. Limity żądań same nie określają kosztu backendu. |
| Netlify | Sam frontend może mieścić się w Free; pełny stos wymaga zewnętrznego API | 10–100 tys. żądań to 2–20 kredytów; dochodzą buildy, transfer i baza. Koszt całego zestawu nie został wyliczony z powodu runtime filter i niepotwierdzonej ceny storage DB. |

Źródła stawek: [Railway](https://docs.railway.com/pricing/plans), [Render](https://render.com/pricing), [nowe plany Render](https://render.com/docs/new-workspace-plans), [Cloudflare Containers](https://developers.cloudflare.com/containers/platform/pricing/), [Neon](https://neon.com/pricing), [Fly](https://fly.io/pricing/), [Vercel Hobby](https://vercel.com/docs/plans/hobby), [Vercel Pro](https://vercel.com/docs/plans/pro-plan), [Netlify](https://docs.netlify.com/manage/accounts-and-billing/billing/billing-for-credit-based-plans/credit-based-pricing-plans/).

### Shortlisted Platforms

Pierwotna lista po punktacji: Railway, Render, Cloudflare Containers + Neon. Po ujawnieniu posiadanego serwera użytkownik wybrał self-hosting. Poniżej wybrany wariant i dwie chmurowe alternatywy; surowa punktacja nadal uczciwie pokazuje większy narzut operacyjny własnego sprzętu.

#### 1. Domowy Mac M1 + OrbStack / Compose (Recommended)

Jawny wybór użytkownika odpowiada priorytetowi minimalnego kosztu i pozwala wykorzystać istniejący sprzęt. Pierwsza aplikacja jest dostępna przez HTTP w LAN. W późniejszym etapie trwały Named Tunnel może publikować ją przez HTTPS z połączeniem wychodzącym, bez potrzeby publicznego IP lub przekierowania portów. Konto i domena na Cloudflare będą warunkiem standardowej ścieżki publicznej. [Tunnel](https://developers.cloudflare.com/tunnel/), [konfiguracja](https://developers.cloudflare.com/tunnel/get-started/).

#### 2. Railway

Najwyższy wynik w pierwotnym porównaniu platform zarządzanych. Zachowuje API i DB u jednego dostawcy z niskim progiem kosztu. Odpowiedni następny wariant, gdy potrzebna będzie większa niezależność od domowego zasilania i WAN.

#### 3. Render

Alternatywa z jasno wycenionym płatnym API oraz zarządzanym PG. Koszt minimalny jest wyższy, ale zakres usług bazy jest wygodniejszy operacyjnie; wybór ma sens, jeśli przewidywalność i obsługa DB przeważą nad najniższym rachunkiem.

## Anti-Bias Cross-Check: Domowy Mac M1 + Cloudflare Tunnel

Trzy perspektywy wykonano ponownie dla nowego wyboru użytkownika. Wnioski są analizą hipotetycznych awarii opartą na architekturze i źródłach, a nie opisem zdarzeń, które już wystąpiły.

### Devil's Advocate — Weaknesses

1. Wyłączenie Maca, uśpienie, awaria routera lub WAN zatrzymują API i DB; Cloudflare nie uruchamia za użytkownika lokalnej aplikacji.
2. Brak miesięcznego abonamentu przenosi aktualizacje, restart, monitoring, poprawność kopii i odtwarzanie na właściciela.
3. Kopia na tym samym dysku nie zabezpiecza przed utratą nośnika; potrzebna jest kopia poza Macem i sprawdzone odtworzenie.
4. Usługa uruchamiana po logowaniu może nie wrócić po restarcie. Obsługa FileVault i dostęp przed odblokowaniem zależą od macOS oraz konfiguracji sieci.
5. Tunel publikuje webowy adres; logowanie, odzyskanie hasła i izolacja danych pozostają funkcjami aplikacji. Dodatkowy Cloudflare Access nie zastępuje FR-001.

Źródła operacyjne: [Apple — zasilanie i uśpienie](https://support.apple.com/en-gb/guide/mac-help/mchle41a6ccd/mac), [usługa cloudflared](https://developers.cloudflare.com/tunnel/features/locally-managed-tunnels/as-a-service/macos/), [FileVault i SSH na Apple Silicon](https://support.apple.com/en-lamr/guide/security-pdf/sec8447f5049/web), [Access dla własnych aplikacji](https://developers.cloudflare.com/cloudflare-one/access-controls/applications/http-apps/self-hosted-public-app/).

### Pre-Mortem — How This Could Fail

Po sześciu miesiącach domowe wdrożenie PennyPath przestaje być użyteczne. Początkowo wszystko działało, bo właściciel był zalogowany na Macu i ręcznie uruchamiał procesy. Po aktualizacji systemu serwer wrócił do ekranu odblokowania, a aplikacja i tunel nie wystartowały. Nikt wcześniej nie sprawdził pełnego restartu bez sesji użytkownika ani sposobu odzyskania dostępu do zaszyfrowanego dysku. Użytkownicy widzieli błąd połączenia i nie mogli zapisać wydatków. Właściciel naprawił uruchamianie, ale później przerwa w zasilaniu ujawniła drugi problem: baza oraz wszystkie kopie zapasowe leżały na tym samym dysku. Uszkodzenie nośnika pozbawiło aplikację zarówno danych, jak i możliwości odtworzenia. Zachowany eksport okazał się stary, ponieważ zadanie tworzenia kopii kończyło się błędem bez powiadomienia. Próba cofnięcia ostatniego wydania przywróciła kod, lecz pozostawiła nowszy schemat bazy. Podgląd testowy wcześniej korzystał z tych samych danych i dodatkowo zmienił historyczne przypisania kategorii. Po wymianie katalogu aplikacji stracono klucze ochrony sesji, jeśli wybrano logowanie korzystające z tej funkcji. Zerowy abonament hostingowy ukrywał rzeczywisty obowiązek utrzymania: kontrolę usług, aktualizacje, oddzielne dane testowe, kopie poza serwerem i sprawdzone odtwarzanie. To te zaniedbania, a nie moc procesora M1, podważyły zaufanie użytkowników.

### Unknown Unknowns

- **Wersja systemu ma znaczenie.** Serwer musi spełniać wymagania OrbStack; runtime .NET działa w obrazie Linux, a nie bezpośrednio w macOS. Mac M1 wymaga obrazu `linux/arm64`. [OrbStack](https://docs.orbstack.dev/), [obrazy .NET](https://github.com/dotnet/dotnet-docker).
- **„Autostart” oznacza tutaj start po logowaniu.** Kontenery z `restart: unless-stopped` wznawiają pracę, gdy OrbStack uruchomi silnik. Runner działa na tym samym koncie. FileVault i osiągalność przed zalogowaniem wymagają osobnego sprawdzenia i nie są gwarantowane w pierwszym etapie. [OrbStack](https://docs.orbstack.dev/), [Apple FileVault](https://support.apple.com/en-lamr/guide/security-pdf/sec8447f5049/web).
- **Publiczny HTTPS wymaga poprawnej obsługi proxy.** Przekazany schemat wpływa na przekierowania, cookies i linki odzyskania hasła; klucze Data Protection, jeśli używane, muszą przetrwać wymianę katalogu wydania. [Proxy .NET 10](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0), [Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).
- **Konfiguracja Vite jest częścią artefaktu przeglądarki.** Zmienne `VITE_*` są publiczne i wpisywane podczas budowania. Hasła bazy i token tunelu pozostają po stronie serwera; wspólny origin ułatwia względne adresowanie API. Projekt wymaga Node `^22.18.0 || >=24.12.0`, co jest bardziej restrykcyjne niż ogólne minimum Vite. [Vite env](https://vite.dev/guide/env-and-mode), [build statyczny](https://vite.dev/guide/static-deploy.html).
- **Quick Tunnel nie jest trwałym hostingiem.** W późniejszym publicznym etapie potrzebny będzie Named Tunnel i stała domena. Free ma limit request body 100 MB, ale trzeba ustalić mniejszy limit CSV i przetestować cały import przez tunel; nie przenosimy automatycznie ogólnych timeoutów proxy na konfigurację Tunnel. [Quick Tunnel](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/), [limit request body](https://developers.cloudflare.com/cache/concepts/default-cache-behavior/), [origin parameters](https://developers.cloudflare.com/tunnel/reference/origin-parameters/).

## Operational Story

To kontrakt planowanego działania, a nie opis już skonfigurowanych usług.

- **Preview deploys**: PR ma build i testy na runnerze GitHub. Test używa izolowanego projektu Compose, syntetycznej bazy i lokalnego rejestru obrazów. Automatyczne publiczne podglądy PR pozostają poza zakresem.
- **Secrets**: token tunelu, poświadczenia PostgreSQL i klucze ochrony danych są przechowywane poza repozytorium i katalogami wydań, z odczytem ograniczonym do kont usług i administratora Maca. Rotacja tworzy nowy sekret, aktualizuje usługę, sprawdza połączenie i usuwa poprzedni dostęp. Każdy posiadacz tokenu tunelu może uruchomić connector; token nie trafia do frontendowego buildu. [Tokeny tunelu](https://developers.cloudflare.com/tunnel/reference/tunnel-tokens/).
- **Rollback**: `scripts/deploy.sh` zapisuje bieżący i poprzedni digest w `~/pennypath/deploy/release.env`. Aktualizuje tylko `app`, sprawdza zdrowie przez maksymalnie 120 sekund i przywraca poprzednio działający obraz po niepowodzeniu. Ręczna komenda to `bash scripts/deploy.sh rollback`. Migracje DB oraz sekrety mają własną zgodność i nie cofają się razem z kodem.
- **Approval**: właściciel zatwierdza pierwszą publikację produkcyjną, zmianę kosztów, usunięcie bazy oraz istotne zmiany sekretów i sieci. Zatwierdzony wcześniej przepływ wdrażania po merge do main może działać zgodnie z tą autoryzacją. Agent może wykonywać przyznane mu budowanie, sprawdzanie i odczyt logów; ten research nie uruchamia wdrożenia.
- **Logs**: w trwałym katalogu serwera użyj `docker compose --env-file .env --env-file release.env logs --tail 100 app postgres`. Logi workflow są w GitHub Actions. Logowanie pomija sekrety i treść finansowych rekordów. Logi przyszłego cloudflared zostaną dodane przy konfiguracji tunelu.
- **Backup / restore**: zaplanuj logiczny eksport PostgreSQL i zaszyfrowaną kopię poza Macem; sprawdź odtworzenie do osobnej bazy oraz zgodność rejestru i podsumowania. Zachowaj też role/uprawnienia i konfigurację potrzebną do odzyskania, ponieważ pojedynczy `pg_dump` obejmuje jedną bazę, a nie cały klaster. [pg_dump](https://www.postgresql.org/docs/current/app-pgdump.html).

## Risk Register

L/M/H = niskie/średnie/wysokie prawdopodobieństwo lub wpływ. Oceny są jakościowe dla MVP. Status funkcji i ograniczenia sprawdzono 2026-10-13.

| Risk | Source | Likelihood | Impact | Mitigation |
| --- | --- | --- | --- | --- |
| Uśpienie, brak zasilania lub domowego Internetu wyłącza usługę | Devil's advocate / Pre-mortem | M | H | Ustaw pracę bez uśpienia i sprawdź wznowienie po utracie zasilania; wykonaj zewnętrzny healthcheck; zaakceptuj możliwe przerwy MVP. |
| Usługi startują dopiero po zalogowaniu albo pozostają za FileVault | Devil's advocate / Pre-mortem | M | H | Zaakceptowane ograniczenie pierwszego etapu: uruchom OrbStack i runner po zalogowaniu; przetestuj pełny restart i sposób odblokowania dysku. |
| Kopia bazy ginie razem z dyskiem Maca lub jest bezużyteczna | Devil's advocate / Pre-mortem | M | H | Twórz logiczne kopie PostgreSQL, przechowuj zaszyfrowaną kopię poza Macem i okresowo odtwarzaj do osobnej bazy z kontrolą sum. |
| Rollback kodu nie pasuje do schematu bazy | Devil's advocate / Pre-mortem | M | H | Zachowuj bieżący i poprzedni digest obrazu; sprawdzaj zgodność przyszłych migracji z poprzednim kodem i ustal ścieżkę odtwarzania danych przed migracją. |
| Podgląd lub publiczny adres umożliwia dostęp do rzeczywistych cudzych wydatków | Devil's advocate / Pre-mortem | M | H | Stosuj oddzielną testową bazę i dane syntetyczne; weryfikuj dwa konta aplikacji; Access traktuj jako dodatkową bramkę, obok uprawnień aplikacji. |
| Serwer ma niewspieraną wersję macOS lub niewłaściwą architekturę artefaktu | Unknown unknowns / Research finding | M | M | Sprawdź sw_vers i uname -m; porównaj z wymaganiami OrbStack; publikuj linux/arm64 i weryfikuj architekturę obrazu w CI. |
| OrbStack lub runner nie wznawia pracy po restarcie | Unknown unknowns / Research finding | M | M | Włącz start OrbStack przy logowaniu i usługę runnera na tym samym koncie; wykonaj rzeczywisty test restartu. |
| HTTPS przez tunel powoduje błędne przekierowania lub cookies | Unknown unknowns | M | M | Ustaw obsługę przekazanego schematu i zaufanego proxy zgodnie z .NET 10; sprawdź logowanie, wylogowanie i link odzyskania hasła przez publiczny HTTPS. |
| Sekret lub opis wydatku trafia do bundla albo logów | Unknown unknowns | M | H | Trzymaj token tunelu, hasła DB i klucze po stronie serwera z ograniczonym odczytem; sprawdzaj bundle Vite i maskowanie danych w logach. |
| Utrata kluczy ochrony danych unieważnia sesje po restarcie lub rollbacku | Unknown unknowns / Pre-mortem | M | M | Jeśli logowanie używa ASP.NET Core Data Protection, przechowuj chronione klucze poza wymienianym katalogiem wydania i przetestuj restart. |
| Import CSV przekracza limit żądania lub zajmuje proces zbyt długo | Research finding | L | M | Ustal aplikacyjny limit wielkości CSV i zmierz import przez prawdziwy tunel; uwzględnij limit request body Cloudflare Free 100 MB i rzeczywiste timeouty. |
| Koszt lub zakres utrzymania przewyższa korzyść z własnego sprzętu | Devil's advocate / Research finding | M | M | Zmierz dodatkowy pobór prądu, koszt domeny i kopii oraz nakład obsługi; przy nieakceptowalnych przerwach wróć do Railway albo Render. |

Ryzyka alternatyw, jeśli nastąpi zmiana decyzji:
- Railway Focused PR Environments: **beta**, sprawdzone 2026-10-13; użyj standardowego podglądu lub przetestuj tę opcję osobno.
- Render documentation MCP: **experimental**, sprawdzone 2026-10-13; polegaj na operacyjnym MCP, CLI i API.
- Vercel Services / Container Runtime: **beta**, WebSockety: **Public Beta**, sprawdzone 2026-10-13; powtórz ocenę przed wyborem zamiast traktować je jako stabilną podstawę.
- Netlify DB storage: aktualna stawka niepotwierdzona po minionej dacie ulgi; nie buduj budżetu na założeniu darmowego storage.
- Fly Managed Postgres: regiony i zakres funkcji zależą od aktualnej oferty; cena po zmianie z 2026-10-01 oraz opisy rozwijanych funkcji wymagają ponownej kontroli przy wyborze.

### Otwarte ustalenia przed wdrożeniem

- Wersja macOS, dostępne RAM/dysk, autostart i zachowanie FileVault na faktycznym serwerze.
- Domena i strefa DNS Cloudflare dla trwałego publicznego hosta.
- Miejsce kopii poza Macem oraz akceptowana utrata danych i przerwa na odtwarzanie.
- Pytanie PRD o uzyskanie konta i dostęp bez logowania; wybór hostingu go nie rozstrzyga.
- Pomiar zużycia zasobów i importu CSV; PRD nadal nie podaje ruchu ani wielkości danych.

## Getting Started

Kod kontenerów, workflow i skrypty są w repozytorium. Instalacja na osobnym Macu, publikacja GHCR i rejestracja runnera nie zostały wykonane w tym workspace.

1. Opublikuj `main` z `DEPLOY_ENABLED=false`, poczekaj na build i ustaw pakiet GHCR jako publiczny.
2. Przygotuj OrbStack i konto serwera; zweryfikuj ARM64, brak uśpienia oraz dostęp Docker Compose.
3. Zainicjalizuj `~/pennypath/deploy` z konfiguracją i plikiem hasła. Uruchom PostgreSQL, następnie wdroż pierwszy digest skryptem.
4. Zarejestruj runner `pennypath-deploy` na koncie używającym OrbStack i włącz `DEPLOY_ENABLED=true` po sprawdzeniu LAN.
5. Sprawdź merge, rollback, kopię i odtworzenie bazy oraz restart Maca po zalogowaniu.

Pełne komendy znajdują się w [deploy-plan.md](../deployment/deploy-plan.md).

## Out of Scope

Poza tym etapem pozostają:
- Cloudflare Tunnel, publiczny HTTPS i automatyczne podglądy PR;
- integracja API z bazą, migracje domenowe, logowanie i wydatki;
- architektury wieloregionowej, HA i pełnego planu DR;
- instalacji na Macu, zmian routera/DNS ani publicznego wdrożenia.

Podstawowe kopie i próba odtworzenia są częścią wymagań operacyjnych tego MVP.
