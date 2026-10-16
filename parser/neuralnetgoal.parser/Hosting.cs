using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeuralNetGoal.Parser.Fetching;
using NeuralNetGoal.Parser.Services;

namespace NeuralNetGoal.Parser;

/// <summary>JSON settings shared by the Web API and the client so both sides agree on the wire format.</summary>
public static class ParserJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }
}

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the three scrapers and the HTTP source. Configure <see cref="BetexplorerOptions"/> via
    /// <paramref name="configure"/> or services.Configure&lt;BetexplorerOptions&gt;(...).</summary>
    public static IServiceCollection AddNeuralNetGoalParser(this IServiceCollection services, Action<BetexplorerOptions>? configure = null)
    {
        services.AddOptions<BetexplorerOptions>();
        if (configure is not null) services.Configure(configure);

        services.AddHttpClient(HttpHtmlSource.HttpClientName)
            .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan) // timeouts are handled per request
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var o = sp.GetRequiredService<IOptions<BetexplorerOptions>>().Value;
                var handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.All,
                    AllowAutoRedirect = true,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                };
                if (!string.IsNullOrWhiteSpace(o.ProxyUrl))
                {
                    handler.Proxy = new WebProxy(o.ProxyUrl);
                    handler.UseProxy = true;
                }
                return handler;
            });

        services.AddSingleton<IHtmlSource, HttpHtmlSource>();
        services.AddSingleton<IResultsScraper, ResultsScraper>();
        services.AddSingleton<IFixturesScraper, FixturesScraper>();
        services.AddSingleton<ILeagueCatalogScraper, LeagueCatalogScraper>();
        return services;
    }
}

/// <summary>Everything a non-DI host (a console or desktop app) needs, built in one call.</summary>
public sealed class ParserServices : IDisposable
{
    private readonly ServiceProvider _provider;

    internal ParserServices(ServiceProvider provider)
    {
        _provider = provider;
        Results = provider.GetRequiredService<IResultsScraper>();
        Fixtures = provider.GetRequiredService<IFixturesScraper>();
        Catalog = provider.GetRequiredService<ILeagueCatalogScraper>();
        Html = provider.GetRequiredService<IHtmlSource>();
    }

    public IResultsScraper Results { get; }
    public IFixturesScraper Fixtures { get; }
    public ILeagueCatalogScraper Catalog { get; }
    public IHtmlSource Html { get; }

    public void Dispose() => _provider.Dispose();
}

public static class ParserFactory
{
    /// <summary>Creates the parser without a DI container on the caller's side (desktop / console use).</summary>
    public static ParserServices Create(BetexplorerOptions? options = null, ILoggerFactory? loggerFactory = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (loggerFactory is not null) services.AddSingleton(loggerFactory);
        services.AddNeuralNetGoalParser(o =>
        {
            if (options is null) return;
            foreach (var p in typeof(BetexplorerOptions).GetProperties().Where(p => p.CanWrite))
                p.SetValue(o, p.GetValue(options));
        });
        return new ParserServices(services.BuildServiceProvider());
    }
}
