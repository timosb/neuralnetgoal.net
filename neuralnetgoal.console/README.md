# neuralnetgoal.console (temporary desktop client)

Replaces the legacy `oddsmiser.scraper.betexplorer` console. It calls the parser (in this process, or on the VPS through the
Web API), stores results in the **existing** SQL Server database, builds the league tables with the **existing** stored procedure,
and exports the final Excel file from the fixtures.

## One-time setup (secrets are NOT stored in git)

```bash
cd neuralnetgoal.console
dotnet user-secrets set "ConnectionStrings:Default" "<SQL Server connection string>"
dotnet user-secrets set "Parser:ApiKey" "<api key, only for Remote mode>"
dotnet user-secrets set "Export:GemBoxLicense" "<GemBox license key>"
```

`appsettings.json` holds the non-secret defaults: `Parser:Mode` (`Remote` = call the Web API at `Parser:ApiUrl`,
`Local` = parse inside this process), `Betexplorer:*` (used by Local mode), `Export:Directory`.
Machine-specific overrides can go in `appsettings.local.json` (git-ignored).

## Commands

```bash
dotnet run --project neuralnetgoal.console -- <command> [options]      # no command = interactive menu
```

| Command | What it does |
|---|---|
| `results` | scrape the latest results of every league in `dbo.SoccerRunningLeagues`; insert the new ones into `dbo.SoccerResults`; scrape problems go to `dbo.Errors` |
| `tables` | run `dbo.Build_Soccer_league_Tables` for each running league |
| `fixtures` | scrape fixtures, replace `dbo.SoccerFixtures` (in one transaction), read `dbo.fn_Soccer_fixtures()`, write `Fixtures<ddMMyy>.xlsx` to your **Downloads** folder and open it |
| `all` | results, then tables, then fixtures + Excel |
| `training` | training workbook from `dbo.DataVectors` (not part of the old `Main`; ported because it existed) |
| `health` | is the parser reachable, and can it reach betexplorer? |
| `catalog` | list countries the parser sees (touches no database; a quick parser check) |

Options: `--recent=N` only the newest N unknown matches per league (use this until match URLs are stored, see below),
`--country=X`, `--division=Y`, `--local` / `--remote`, `--dump=DIR` (save fetched pages), `--verbose`,
`--out=FOLDER` (where files are created; default: your Downloads folder), `--no-open` (do not open the Excel file).

You see one line per match, live, in both modes (in Remote mode the lines arrive from the API's event stream).

## Files it creates, and Windows / macOS

* **Every file the console creates goes to your Downloads folder** (`C:\Users\<you>\Downloads` on Windows, `~/Downloads` on macOS)
  and **opens automatically** in the program your system uses for it (Excel). Change the folder with `Export:Directory` or
  `--out=...`; turn the opening off with `--no-open` or `Export:OpenAfterExport=false`.
* If the file from today is still open in Excel, the new one gets a time added to its name (`Fixtures131026-122149.xlsx`) instead of failing.
* It runs on **Windows and macOS** (and Linux): it only uses cross-platform libraries, and the two things that differ (the
  Downloads folder and opening a file) are in `Platform.cs`.

### Running on a Mac

1. Install the **.NET 10 SDK** from dotnet.microsoft.com (the Apple Silicon or Intel installer to match your Mac).
2. Get the code (`git clone` the repository, or copy the folder).
3. Enter the secrets **on the Mac too**: they are stored per machine, not in git (same three `dotnet user-secrets set` commands as above, run in the repo folder).
   Instead of user-secrets you can use environment variables with the prefix `NNG_`, e.g. `NNG_ConnectionStrings__Default`.
4. Run it: `dotnet run --project neuralnetgoal.console`.

`publish-console.cmd` (Windows) and `publish-console.sh` (macOS) build a self-contained version that needs no .NET installed.
A program built on Windows for macOS is not signed; on the Mac run `chmod +x neuralnetgoal.console` and
`codesign -s - --force neuralnetgoal.console` once, and clear the download flag with `xattr -d com.apple.quarantine neuralnetgoal.console`.
Building on the Mac itself (`publish-console.sh`) signs it automatically.

## Notes / known limitations

* The `SoccerResults` table does not store the match URL, so the console cannot yet tell the parser which matches it already has;
  every run reads every match page of the league (use `--recent=N`). Adding a `MatchUrl` column would remove that cost.
* Missing half-time scores and odds are stored as `0`, as the legacy scraper did (the legacy columns are NOT NULL).
* The Excel layout is ported unchanged, including its quirk: the four "score" columns carry derived features
  (points per game, last-5 form) computed with **integer division**. Kept so the trained network sees the same inputs.
* Not ported (not called by the old `Main`): historical backfill (`GetHistoricalDataAsynch`), country/league link initialisation.

> Full architecture and reference documentation: [docs/parser-architecture.md](../docs/parser-architecture.md)
