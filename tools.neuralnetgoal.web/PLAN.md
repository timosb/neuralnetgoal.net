# tools.neuralnetgoal.web - execution plan

Goal: replace `neuralnetgoal.console` with a web app on SmarterASP.NET: same database, same parser (on the VPS), same Excel.

## 1. Decisions (change any of them before step 1)

| Topic | Decision | Why |
|---|---|---|
| Layout | `tools.neuralnetgoal.web/` with **`tools.neuralnetgoal.service`** (minimal API), **`tools.neuralnetgoal.client`** (Blazor WebAssembly), **`tools.neuralnetgoal.shared`** (DTOs only) and `database/` | The shared project is the only addition to what you asked for: it avoids duplicating request/response classes in two projects. |
| Hosting | **One site.** The service also serves the Blazor files (`UseBlazorFrameworkFiles`) | One deployment, one URL, no CORS, and the cookie works with no extra setup. |
| Secrets | Only the **service** knows the connection string, the parser API key and the GemBox licence. The browser never receives them | Blazor WebAssembly runs in the browser; anything in the client project is public. |
| Data access | **Dapper + repository pattern**, no Entity Framework, no migrations. The existing `Database.cs` is split into repositories | The console already proved this code against your database. |
| Repositories | `ILeagueRepository`, `IResultsRepository`, `IFixturesRepository`, `ILeagueTableRepository`, `IErrorRepository`, `ITrainingRepository`, `IUserRepository` | One per aggregate, nothing more. |
| Authentication | **Cookie authentication** (HttpOnly, Secure, SameSite=Strict) against the `auth.*` tables, PBKDF2 password hashing, roles Admin/Operator/Viewer, lockout after repeated failures, `auth.LoginAudit` | Same-origin, so no tokens in browser storage. Custom tables read with Dapper, so no ASP.NET Identity EF stores. |
| Admin password | The SQL creates `Admin` **without** a password. The service sets it at first start from the secret `Auth:AdminInitialPassword` and forces a change at first login | SQL cannot compute a PBKDF2 hash, and a password should not sit in a script. You type it only into user-secrets or the host's environment settings. |
| Long jobs | A scrape takes about 70 s per league, too long for one HTTP request. The service starts a **job** (in memory) and the browser **polls** `GET /api/jobs/{id}?after=n` once a second to show the live per-match log | Polling works on shared IIS (no websockets needed). Jobs are lost if the app pool recycles, acceptable for version 1. |
| Excel | Built on the server with GemBox from the template, returned as a **download** (no auto-open on a server) | |
| UI | Bootstrap 5 for layout and navigation, **Telerik UI for Blazor** for grid, buttons, dropdowns, dialogs, notifications and the progress log, using Telerik's **Bootstrap theme** so both match | |
| Logo | `NNG_LOGO.svg` copied to `client/wwwroot/`; used in the navbar and as favicon. Palette taken from the logo: background `#3a3a3a`, accent `#00bfff`, text `#c0c0c0` | |

## 2. Things to verify first (cheap now, expensive later)

1. **SmarterASP.NET runtime.** In the control panel check that the plan supports **.NET 10** (otherwise the service would target the newest supported one).
2. **Outbound access to the parser.** The shared host must be able to call `https://parser.neuralnetgoal.cloud:5234`. Many shared hosts block odd outbound ports. Test it in step 9; the fix is a one-line nginx change on the VPS to also serve the parser on port 443.
3. **Telerik packages.** Visual Studio must be able to restore from the Telerik NuGet feed with your licence. Never paste the credentials into the chat; they stay in your NuGet configuration. A Telerik licence key file is also needed at build time (`TelerikLicense.txt`/user setting, which I will add to `.gitignore`).
4. **Database permission.** The SmarterASP SQL user must be allowed to `CREATE SCHEMA` and `CREATE TABLE` (step 2).

## 3. Steps

Each step ends with a checkpoint that you can test before the next one.

| # | Who | What | Checkpoint |
|---|---|---|---|
| 1 | Claude | Create the three projects, add them to `NeuralNetGoalTools.sln`, reference `neuralnetgoal.parser` and `neuralnetgoal.parser.client` from the service, copy the logo, `.gitignore` entries | `dotnet build` is green |
| 2 | **You** | Run `database/001_auth_schema.sql` in SSMS / the SmarterASP SQL manager | The last SELECT shows `Admin`, HasPassword = 0, role Admin |
| 3 | Claude | **Secrets**: copy the three values (connection string, parser API key, GemBox licence) from the console's user-secrets into the service's user-secrets *without printing them*; generate the cookie key; add the `Auth:AdminInitialPassword` placeholder for you to fill. Remove the GemBox key that sits in the tracked `neuralnetgoal.console/appsettings.json` | `dotnet user-secrets list` shows the keys; nothing secret in git |
| 4 | Claude | Service: configuration, Serilog, `/health`, repositories (moved from `Database.cs`), dependency injection | `/health` answers and reads `SoccerRunningLeagues` |
| 5 | Claude | Authentication: user repository, password hasher, `POST /api/auth/login`, `/logout`, `/me`, `/change-password`, role policies, login lockout and audit, first-start Admin password setup | You can sign in as Admin and are asked to change the password |
| 6 | **You** | Put your chosen first password in `Auth:AdminInitialPassword` (user-secrets) and start the service once | Admin gets a hash in `auth.Users` |
| 7 | Claude | Business endpoints: leagues, results/tables/fixtures **jobs** (the console's `App` logic moved into a `WorkflowService`), job polling, errors list, Excel download | Postman: start a job, poll it, download the file |
| 8 | Claude | Blazor client: layout with logo, login page, `AuthenticationStateProvider`, pages **Dashboard, Leagues, Run (live log), Fixtures & Excel, Errors, Users (Admin)** in Telerik components | Everything works locally against the real database |
| 9 | **You** | Publish to SmarterASP.NET (Visual Studio *Publish* to a folder, upload with FTP/FileZilla as usual) and set the secrets as environment settings in the control panel or an uploaded `appsettings.Production.json` | The site loads over HTTPS, sign in, `health` shows the parser reachable (see risk 2) |
| 10 | Both | Run a real `results` and `fixtures` from the web, compare the Excel with the console's, then retire the console | Same rows, same Excel |

## 4. Out of scope for version 1 (decide later)

User self-service/e-mail, persistent job history, scheduled runs, `MatchUrl` column to speed up incremental scraping, the training workbook page, the "time not set" fixtures handling.

## 5. What I need from you to start

1. A yes/no on the decisions in section 1, especially the extra `shared` project and the single-site hosting.
2. Confirm .NET 10 is available on your SmarterASP plan.
3. Run `001_auth_schema.sql` when we reach step 2.
