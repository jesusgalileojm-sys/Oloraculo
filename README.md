# Oloráculo

Oloráculo is a .NET 9 Blazor Server application that predicts the 2026 FIFA World Cup. It layers several statistical predictors into a "model ladder", picks the strongest usable model for each fixture, and runs a Monte Carlo simulation of the full tournament to estimate every team's chances of advancing and lifting the trophy.

## Features

- Layered model ladder, evaluated in priority order and selected by `Oloraculo.Web/Predictors/FinalPredictionSelector.cs`:
  - Null baseline (uniform outcome)
  - FIFA ranking model
  - Elo model
  - Recent form model
  - Poisson goal model (scoreline distribution)
  - Goal + recent context model (player availability, lineups, odds awareness)
- Highest usable rung selection: the Final Oracle uses the most informative model that is not degraded, and explains which higher rungs it skipped (if any) and why.
- Monte Carlo tournament simulation (`Oloraculo.Web/Services/Simulation/SimulationService.cs`) producing per-team group, qualification, and championship probabilities. Repeatable when a seed is configured.
- Snapshot, evaluation, and performance tracking so predictions can be scored against actual results over time.
- Uses API-Football to retrieve fixtures, injuries, lineups, and odds, if available (`Oloraculo.Web/Services/ApiFootballService.cs`), and degrades into static-file only if missing.
- Blazor pages (see `Oloraculo.Web/Components/Layout/NavMenu.razor`): Overview, Oracle Lab, Matches, Tournament, Performance, and Data.

## Tech stack

- .NET 9
- Blazor Server (interactive server components)
- Entity Framework Core 9 with SQLite
- CsvHelper for seed-data import
- xUnit for tests

## Project structure

```
Oloraculo.sln
Oloraculo.Web/                 Blazor Server app
  Components/                  Razor pages, layout, and shared UI
  DAL/                         OloraculoDbContext (EF Core)
  Data/                        CSV seed data
  Helpers/                     CSV parsing, team-name normalization, crypto
  Models/                      Domain, CSV, and API-Football models
  Predictors/                  Model ladder and final selector
  Probability/                 Poisson scoreline and outcome math
  Services/                    Import, prediction, evaluation, snapshot
    Simulation/               Tournament Monte Carlo engine
  OloraculoConfig.cs           Strongly typed configuration
  Program.cs                   App startup and DI
Oloraculo.Web.Tests/           xUnit test project
```

## Getting started

Prerequisites:

- .NET 9 SDK

Restore and run:

```bash
dotnet restore
dotnet run --project Oloraculo.Web
```

On first run the SQLite database is created automatically and the seed CSVs are imported (via `Oloraculo.Web/Services/CsvImportService.cs`), so no manual database setup is required.

## Configuration

Application settings live under the `Oloraculo` section (bound to `Oloraculo.Web/OloraculoConfig.cs`):


| Key                    | Description                                             |
| ---------------------- | ------------------------------------------------------- |
| `SimulationCount`      | Number of Monte Carlo tournament simulations to run     |
| `SimulationSeed`       | Optional seed for repeatable simulations                |
| `RecentResultCount`    | How many recent matches the recent-form model considers |
| `GoalModelYearsWindow` | Year window used by the Poisson goal model              |
| `ApiFootballBaseUrl`   | Base URL for the API-Football service                   |
| `ApiFootballApiKey`    | API-Football key (do not commit this, see below)        |
| `ApiFootballLeagueId`  | League id used when fetching fixtures                   |
| `ApiFootballSeason`    | Season used when fetching fixtures                      |


The API-Football key belongs in `appsettings.Development.json` (which is gitignored) or in .NET user-secrets.

## Testing

To run the xUnit suite:

```bash
dotnet test
```

## Data sources

Seed data is stored as CSV files in `Oloraculo.Web/Data`:

- `historical_results.csv` (past international match results, retrieved once from [https://raw.githubusercontent.com/martj42/international_results/master/results.csv](https://raw.githubusercontent.com/martj42/international_results/master/results.csv))
- `fifa_rankings.csv` (FIFA ranking snapshot)
- `elo_snapshot.csv` (Elo rating snapshot)
- `wc2026_groups.csv` (2026 World Cup group draw)

