# tools.neuralnetgoal.web

Web version of the console: Blazor WebAssembly client + minimal API service, same SQL Server database. Plan: [PLAN.md](PLAN.md).

| Project | What |
|---|---|
| `tools.neuralnetgoal.service` | Minimal API. Repositories (Dapper), cookie authentication, jobs (results / tables / fixtures / training), Excel export. Also serves the client. Owns every secret. |
| `tools.neuralnetgoal.client` | Blazor WebAssembly UI (Bootstrap 5 + Telerik UI for Blazor). No secrets. |
| `tools.neuralnetgoal.shared` | Request/response classes used by both. |
| `database/001_auth_schema.sql` | `auth.*` tables + the Admin user (already run). |

## Run locally

Secrets live in user-secrets of the **service** project (never in git):

```bash
dotnet run --project tools.neuralnetgoal.web/tools.neuralnetgoal.service
```

Already set from the console: `ConnectionStrings:Default`, `Parser:ApiKey`, `Export:GemBoxLicense`.
