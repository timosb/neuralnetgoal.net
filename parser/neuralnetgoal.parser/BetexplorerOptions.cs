namespace NeuralNetGoal.Parser;

/// <summary>Everything about the site and how politely we talk to it. All of it can be bound from configuration.</summary>
public sealed class BetexplorerOptions
{
    public string BaseUrl { get; set; } = "https://www.betexplorer.com";

    /// <summary>Site-relative path of the page that lists all countries. The site now uses "/football/"
    /// (the legacy scraper used "/soccer/").</summary>
    public string CatalogPath { get; set; } = "/football/";

    /// <summary>Rewrite site-relative paths that start with "/soccer/" to "/football/". Your database still stores
    /// the old URLs in SoccerRunningLeagues; this makes them work without touching the data.</summary>
    public bool RewriteLegacySoccerPaths { get; set; } = true;

    /// <summary>Appended to a league path to reach its results / fixtures pages.</summary>
    public string ResultsSuffix { get; set; } = "results/";
    public string FixturesSuffix { get; set; } = "fixtures/";

    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
    public string AcceptLanguage { get; set; } = "en-US,en;q=0.9";

    /// <summary>Optional raw Cookie header value (e.g. to pin the time zone the site renders match times in).</summary>
    public string? Cookie { get; set; }

    /// <summary>Optional HTTP(S) proxy, e.g. "http://user:pass@host:port". Use it if the site blocks the server's region.</summary>
    public string? ProxyUrl { get; set; }

    /// <summary>How many HTTP requests may be in flight at once (global for the process).</summary>
    public int MaxConcurrentRequests { get; set; } = 4;

    /// <summary>Pause after every request, while still holding a request slot. A random 0-50% jitter is added.</summary>
    public int DelayBetweenRequestsMs { get; set; } = 400;

    /// <summary>Retries for transient failures (timeouts, 429, 5xx). Blocks (403 / foreign host) are never retried.</summary>
    public int MaxRetries { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 1500;
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>How many times a match page is re-fetched when its teams cannot be found
    /// (the legacy scraper looped up to 10 times, immediately).</summary>
    public int DetailPageAttempts { get; set; } = 3;

    /// <summary>If set, every fetched page is saved here. Use it to capture real pages as samples / for debugging.</summary>
    public string? DumpDirectory { get; set; }
}
