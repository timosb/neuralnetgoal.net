# neuralnetgoal parser

> Full architecture and reference documentation: [docs/parser-architecture.md](../docs/parser-architecture.md)

Database-agnostic library + Web API that reads results, fixtures and the league catalog from betexplorer.com.

```
parser/
  neuralnetgoal.parser          class library: models, events, fetching, pure HTML parsers, the 3 scraper interfaces
  neuralnetgoal.parser.api      ASP.NET Core host (for the VPS): jobs, live event stream (SSE), API key
  neuralnetgoal.parser.client   .NET client for the API; implements the same interfaces over HTTP
  deploy/                       systemd unit + nginx config for the Linux VPS
../neuralnetgoal.console        TEMPORARY desktop client (database, league tables, Excel). Replaced later by the Blazor app.
```

Target framework is set in ONE place: `parser/Directory.Build.props` (net10.0).

## The library

```csharp
IResultsScraper   GetResultsAsync(LeagueRef, ResultsOptions?, IScrapeEventSink?, ct)   // finished matches
IFixturesScraper  GetFixturesAsync(LeagueRef, FixturesOptions?, IScrapeEventSink?, ct) // upcoming matches + odds
ILeagueCatalogScraper  GetCountriesAsync / GetLeagueSeasonsAsync                       // what leagues exist
```

* The caller owns the list of leagues (`SoccerRunningLeagues`) and passes one `LeagueRef` per call. The library never sees a database.
* `ResultsOptions.KnownMatchUrls`: match URLs the caller already stored are skipped without fetching their page.
  Store `MatchResult.MatchUrl` to benefit. `MaxMatches` limits a run to the first N unknown rows (the site lists newest first).
* Every match the parser tries to read produces events: `MatchStarted`, then `MatchParsed` / `MatchFailed` / `MatchSkipped`.
* All XPaths / class names are in `neuralnetgoal.parser/Parsing/Selectors.cs`. When the site changes its HTML, edit only that file.
* `BetexplorerOptions`: base URL, catalog path (now `/football/`; legacy `/soccer/` paths are rewritten automatically), request rate, retries, proxy, cookie,
  and `DumpDirectory` which saves every fetched page (use it to capture real pages for debugging).
* A response that redirects to another host (firewall / captive portal) or answers 401/403 is reported as `Blocked`
  and the scrape stops instead of hammering the site.

## The Web API

| Method | Path | |
|---|---|---|
| GET | `/health` | API up + can the server reach betexplorer? (no key needed) |
| GET | `/v1/countries`, `/v1/leagues?countryName=&countryUrlPath=` | catalog (quick, synchronous) |
| POST | `/v1/results/scrape` `{league, options}` | starts a job, returns 202 + jobId |
| POST | `/v1/fixtures/scrape` `{league, options}` | same |
| GET | `/v1/jobs`, `/v1/jobs/{id}` | status; the result is included once the job finished |
| DELETE | `/v1/jobs/{id}` | cancel |
| GET | `/v1/jobs/{id}/events?after=&level=&kinds=` | **live event stream** (Server-Sent Events) |

Everything under `/v1` needs the header `X-Api-Key`. In Production the API refuses to start without `Parser__ApiKey`.

**Event stream.** Each event has a strictly increasing `seq`. The job keeps the last 5000 events, so a client that connects
late or reconnects with `?after=<last seq>` (or the `Last-Event-ID` header) gets what it missed. The scrape never waits for
a listener. Filters: `level=Warn` (job lifecycle events always pass), `kinds=MatchFailed,MatchParsed`.

```csharp
using var client = ParserApiClient.Create("https://parser.example.com", apiKey);
var job = await client.StartResultsAsync(league);
await foreach (var e in client.StreamEventsAsync(job.JobId))
    Console.WriteLine(e);                       // one line per match
var result = await client.GetJobAsync(job.JobId);
// or simply: await client.Results.GetResultsAsync(league, options, sink)  (same interface as the in-process library)
```

Logs: console + rolling file `logs/parser-.log` (every event, tagged with the job id).

## Running locally

```bash
dotnet run --project parser/neuralnetgoal.parser.api            # http://localhost:5234, open (no key) in Development
```

## Deploying to the Linux VPS

1. Reinstall the OS on an LTS release (Ubuntu 25.10 is out of support). Create a non-root user, SSH keys, firewall (allow 22, 80, 443).
2. Install the ASP.NET Core 10 runtime and nginx.
3. `dotnet publish parser/neuralnetgoal.parser.api -c Release -o publish`, copy to `/opt/neuralnetgoal-parser`.
4. Create `/etc/neuralnetgoal-parser.env` containing `Parser__ApiKey=<long random string>` (chmod 600).
5. Install `deploy/neuralnetgoal-parser.service` (`systemctl enable --now neuralnetgoal-parser`) and `deploy/nginx-neuralnetgoal-parser.conf`, then `certbot --nginx`.
6. `curl https://parser.example.com/health` must show `"reachable": true`. If not, the VPS region is blocked: set `Betexplorer__ProxyUrl`.

## Capturing real pages (do this first, from a network that can reach the site)

Set `Betexplorer__DumpDirectory=/some/dir` (API) or run the console with `--local --dump=DIR`. Every page is saved as HTML.
These are what the selectors are verified against. Verified on 2026-10-12 against real pages: catalog, country, results (current + old season), fixtures, match pages, stage tabs (Champions League). Not yet captured: a postponed match page.
