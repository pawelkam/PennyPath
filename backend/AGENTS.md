# Repository Guidelines

This directory contains PennyPath’s ASP.NET Core backend, targeting .NET 10. These guidelines apply to `backend/`; see @../AGENTS.md for repository-wide instructions.

## Build and Development Commands

Run commands from `backend/`:

- `dotnet restore PennyPath.slnx` — restore the API’s NuGet dependencies.
- `dotnet build PennyPath.slnx --no-restore` — compile the solution after restoring.
- `dotnet run --project PennyPath.Api --launch-profile https` — start the API in Development with HTTPS.

Both launch profiles use port `0`, so the operating system selects ports. Read the listening URLs from startup output; replace the placeholder host in @PennyPath.Api/PennyPath.Api.http before sending requests.

## Project Structure and Adding Endpoints

@PennyPath.slnx contains the single `PennyPath.Api` project. Add any new backend projects to this solution.

Place controllers in `PennyPath.Api/Controllers/`, using @PennyPath.Api/Controllers/WeatherForecastController.cs as the current routing and controller reference. Its `[controller]` route depends on the controller name; review URL changes when renaming controllers.

@PennyPath.Api/Program.cs contains service registration and middleware configuration. The sample response model is @PennyPath.Api/WeatherForecast.cs. The root Dockerfile builds Vue into `wwwroot/` before publishing. No test project exists; `../scripts/smoke-test.py` checks the published app's HTTP behavior.

## Coding Style and Naming

Match the existing C# files: four-space indentation, braces on separate lines, file-scoped namespaces, and PascalCase type and public member names. Name controller files `<Resource>Controller.cs` under the `PennyPath.Api.Controllers` namespace. Keep filenames aligned with their types.

Compiler settings and package versions are defined in @PennyPath.Api/PennyPath.Api.csproj. No EditorConfig or dedicated lint/format configuration is present.

## Testing Guidelines

No testing framework, test naming convention, or coverage threshold is configured. Validate changes with the solution build and endpoint requests in @PennyPath.Api/PennyPath.Api.http. Include new endpoint examples there and record the checks performed in pull requests.

## Commit and Pull Request Guidelines

Existing commits use short descriptions such as “Second lesson”; no enforced prefix convention is evident. Describe backend changes concretely. In pull requests, include affected endpoints, configuration changes, validation commands and results, and related issues when applicable.

## Configuration Notes

Logging settings live in `PennyPath.Api/appsettings*.json`; development URLs and environment settings live in @PennyPath.Api/Properties/launchSettings.json. OpenAPI is exposed only in Development. The LAN container serves HTTP on port 8080 without HTTPS redirection. `/health` returns 200, unknown `/api/*` routes return 404, and browser routes fall back to the Vue page. PostgreSQL integration and authentication are not yet implemented; `UseAuthorization()` alone does not establish authentication.
