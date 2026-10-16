using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralNetGoal.Parser;
using NeuralNetGoal.Parser.Api;
using NeuralNetGoal.Parser.Api.Jobs;
using NeuralNetGoal.Parser.Contracts;
using NeuralNetGoal.Parser.Events;
using NeuralNetGoal.Parser.Models;
using NeuralNetGoal.Parser.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---- logging: console + rolling file (every scrape event is mirrored here by the scrapers) ----
var logDir = builder.Configuration["Logging:Directory"] ?? "logs";
builder.Host.UseSerilog((ctx, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} {Level:u3} {JobId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(Path.Combine(logDir, "parser-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3} {JobId} {Message:lj}{NewLine}{Exception}"));

// ---- JSON: same wire format as the client ----
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.PropertyNameCaseInsensitive = true;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ---- parser library + jobs ----
builder.Services.Configure<BetexplorerOptions>(builder.Configuration.GetSection("Betexplorer"));
builder.Services.AddNeuralNetGoalParser();
builder.Services.Configure<JobOptions>(builder.Configuration.GetSection("Jobs"));
builder.Services.AddSingleton<JobManager>();
builder.Services.AddHostedService<JobCleanupService>();
builder.Services.AddSingleton<SiteHealthChecker>();
builder.Services.AddOpenApi();

var app = builder.Build();

var apiKey = app.Configuration["Parser:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("Parser:ApiKey is not configured. Set it with the Parser__ApiKey environment variable.");
    app.Logger.LogWarning("Parser:ApiKey is empty - API is OPEN (Development only).");
}

if (app.Environment.IsDevelopment()) app.MapOpenApi();

// ---- API key on everything under /v1 ----
var apiKeyBytes = Encoding.UTF8.GetBytes(apiKey ?? "");
app.Use(async (ctx, next) =>
{
    if (apiKeyBytes.Length > 0 && ctx.Request.Path.StartsWithSegments("/v1"))
    {
        var supplied = Encoding.UTF8.GetBytes(ctx.Request.Headers["X-Api-Key"].ToString());
        if (supplied.Length != apiKeyBytes.Length || !CryptographicOperations.FixedTimeEquals(supplied, apiKeyBytes))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { error = "Missing or invalid X-Api-Key header." });
            return;
        }
    }
    await next();
});

// ---------------------------------------------------------------- health
app.MapGet("/health", async (SiteHealthChecker site, CancellationToken ct) =>
{
    var s = await site.CheckAsync(ct);
    return new HealthResponse(s.Reachable ? "Healthy" : "Degraded",
        typeof(Program).Assembly.GetName().Version?.ToString() ?? "0", s);
});

// ---------------------------------------------------------------- catalog (quick, synchronous)
app.MapGet("/v1/countries", async (ILeagueCatalogScraper catalog, CancellationToken ct) =>
    await catalog.GetCountriesAsync(ct: ct));

app.MapGet("/v1/leagues", async (string countryName, string countryUrlPath, ILeagueCatalogScraper catalog, CancellationToken ct) =>
    await catalog.GetLeagueSeasonsAsync(new CountryRef(countryName, countryUrlPath), ct: ct));

// ---------------------------------------------------------------- jobs
app.MapPost("/v1/results/scrape", (StartResultsRequest req, JobManager jobs) =>
{
    if (!Valid(req.League)) return Results.BadRequest(new { error = "League.Country, Division, Season and UrlPath are required." });
    return Accepted(jobs.StartResults(req.League, req.Options));
});

app.MapPost("/v1/fixtures/scrape", (StartFixturesRequest req, JobManager jobs) =>
{
    if (!Valid(req.League)) return Results.BadRequest(new { error = "League.Country, Division, Season and UrlPath are required." });
    return Accepted(jobs.StartFixtures(req.League, req.Options));
});

app.MapGet("/v1/jobs", (JobManager jobs) => jobs.List().Select(j => j.ToInfo()));

app.MapGet("/v1/jobs/{id}", (string id, JobManager jobs) =>
{
    var job = jobs.Get(id);
    if (job is null) return Results.NotFound(new { error = "Unknown job." });

    JsonElement? result = null;
    if (job.IsFinished && job.Result is not null)
        result = JsonSerializer.SerializeToElement(job.Result, job.Result.GetType(), ParserJson.Options);

    return Results.Json(new JobResponse(job.ToInfo(), result), ParserJson.Options);
});

app.MapDelete("/v1/jobs/{id}", (string id, JobManager jobs) =>
    jobs.Get(id) is null ? Results.NotFound(new { error = "Unknown job." })
    : jobs.Cancel(id) ? Results.Accepted() : Results.Conflict(new { error = "Job already finished." }));

// ---------------------------------------------------------------- live event stream (Server-Sent Events)
// GET /v1/jobs/{id}/events?after=123&level=Info&kinds=MatchParsed,MatchFailed
// Reconnecting clients send Last-Event-ID (browsers do it automatically) or ?after= to resume without losing events.
app.MapGet("/v1/jobs/{id}/events", async (
    string id, HttpContext ctx, JobManager jobs, long? after, string? level, string? kinds) =>
{
    var job = jobs.Get(id);
    if (job is null)
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        await ctx.Response.WriteAsJsonAsync(new { error = "Unknown job." });
        return;
    }

    var afterSeq = after ?? (long.TryParse(ctx.Request.Headers["Last-Event-ID"].ToString(), out var h) ? h : 0);
    var minLevel = Enum.TryParse<ScrapeLevel>(level, ignoreCase: true, out var lv) ? lv : ScrapeLevel.Debug;
    var kindFilter = string.IsNullOrWhiteSpace(kinds)
        ? null
        : kinds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => Enum.TryParse<ScrapeEventKind>(k, true, out var kk) ? (ScrapeEventKind?)kk : null)
            .Where(k => k is not null).Select(k => k!.Value).ToHashSet();

    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no"; // tell nginx not to buffer the stream
    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);

    try
    {
        await foreach (var e in job.ReadAsync(afterSeq, TimeSpan.FromSeconds(15), ctx.RequestAborted))
        {
            if (e is null)
            {
                await ctx.Response.WriteAsync(": ping\n\n", ctx.RequestAborted);
                await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                continue;
            }

            var lifecycle = e.Kind is ScrapeEventKind.JobStarted or ScrapeEventKind.JobCompleted
                or ScrapeEventKind.JobFailed or ScrapeEventKind.JobCancelled or ScrapeEventKind.Gap;
            if (!lifecycle && (e.Level < minLevel || (kindFilter is not null && !kindFilter.Contains(e.Kind)))) continue;

            var sb = new StringBuilder();
            if (e.Seq > 0) sb.Append("id: ").Append(e.Seq).Append('\n');
            sb.Append("event: ").Append(e.Kind).Append('\n');
            sb.Append("data: ").Append(JsonSerializer.Serialize(e, ParserJson.Options)).Append("\n\n");
            await ctx.Response.WriteAsync(sb.ToString(), ctx.RequestAborted);
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
        }
    }
    catch (OperationCanceledException) { /* client went away - the job keeps running */ }
});

app.Run();

static bool Valid(LeagueRef? l) =>
    l is not null && !string.IsNullOrWhiteSpace(l.Country) && !string.IsNullOrWhiteSpace(l.Division)
    && !string.IsNullOrWhiteSpace(l.Season) && !string.IsNullOrWhiteSpace(l.UrlPath);

static IResult Accepted(Job job) =>
    Results.Json(new StartJobResponse(job.Id, $"/v1/jobs/{job.Id}", $"/v1/jobs/{job.Id}/events"),
        ParserJson.Options, statusCode: StatusCodes.Status202Accepted);

public partial class Program;
