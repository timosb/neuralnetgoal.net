using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using Tools.NeuralNetGoal.Service;
using Tools.NeuralNetGoal.Service.Auth;
using Tools.NeuralNetGoal.Service.Data;
using Tools.NeuralNetGoal.Service.Endpoints;
using Tools.NeuralNetGoal.Service.Workflow;
using Tools.NeuralNetGoal.Shared;

// ---------------------------------------------------------------------------------------------------------------
// tools.neuralnetgoal.service - the server of the NeuralNetGoal web app (minimal API).
//   * owns every secret (database, parser API key, GemBox licence): the Blazor client never sees them
//   * repositories (Dapper) over the EXISTING SQL Server database; no Entity Framework, no migrations
//   * cookie authentication against the auth.* tables (database/001_auth_schema.sql)
//   * runs results / tables / fixtures jobs and builds the Excel file
//   * also serves the Blazor WebAssembly client, so the site is ONE deployment on ONE address
// ---------------------------------------------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// secrets: dotnet user-secrets (development) or environment variables / appsettings.Production.json (hosting)
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

builder.Services.AddMemoryCache();

// data
builder.Services.AddSingleton<Db>();
builder.Services.AddScoped<ILeagueRepository, LeagueRepository>();
builder.Services.AddScoped<IResultsRepository, ResultsRepository>();
builder.Services.AddScoped<IFixturesRepository, FixturesRepository>();
builder.Services.AddScoped<ITrainingRepository, TrainingRepository>();
builder.Services.AddScoped<IErrorRepository, ErrorRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// workflow
builder.Services.AddSingleton(sp => ParserHost.Create(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<JobManager>();
builder.Services.AddScoped<WorkflowService>();

// authentication: an HttpOnly, Secure, SameSite=Strict cookie. The browser keeps no token it could leak.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "nng.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        // sign the user out when the account was deactivated or the password changed elsewhere (SecurityStamp)
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var uidText = ctx.Principal?.FindFirstValue(AuthClaims.UserId);
            var stamp = ctx.Principal?.FindFirstValue(AuthClaims.Stamp);
            if (!int.TryParse(uidText, out var uid) || stamp is null) { await Reject(ctx); return; }

            var cache = ctx.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var current = await cache.GetOrCreateAsync($"nng.user.{uid}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                var user = await ctx.HttpContext.RequestServices.GetRequiredService<IUserRepository>().FindByIdAsync(uid);
                return user is { IsActive: true } ? user.SecurityStamp.ToString() : "";
            });
            if (current != stamp) await Reject(ctx);

            static async Task Reject(CookieValidatePrincipalContext c)
            {
                c.RejectPrincipal();
                await c.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// A user who must change the password (pwd_ok missing) can only reach the change-password and logout endpoints.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Read, p => p.RequireAuthenticatedUser().RequireClaim(AuthClaims.PasswordOk, "1"))
    .AddPolicy(Policies.Run, p => p.RequireAuthenticatedUser().RequireClaim(AuthClaims.PasswordOk, "1").RequireRole(Roles.Admin, Roles.Operator))
    .AddPolicy(Policies.Admin, p => p.RequireAuthenticatedUser().RequireClaim(AuthClaims.PasswordOk, "1").RequireRole(Roles.Admin));

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.UseWebAssemblyDebugging();
else app.UseHsts();

app.UseHttpsRedirection();

// CSRF guard on top of SameSite=Strict: a browser cannot add a custom header to a cross-site request without CORS approval.
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method)
        && ctx.Request.Headers["X-NNG-Request"] != "1")
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    await next();
});

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");
api.MapAuthEndpoints();
api.MapDataEndpoints();
api.MapJobEndpoints();
api.MapUserEndpoints();
api.MapFallback(() => Results.NotFound());

app.MapFallbackToFile("index.html");

app.Run();

namespace Tools.NeuralNetGoal.Service
{
    public static class Policies
    {
        public const string Read = "Read";
        public const string Run = "Run";
        public const string Admin = "Admin";
    }

    public static class AuthClaims
    {
        public const string UserId = "nng_uid";
        public const string Stamp = "nng_stamp";
        public const string PasswordOk = "nng_pwd_ok";
    }
}
