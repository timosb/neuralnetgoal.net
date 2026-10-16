using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeuralNetGoal.Parser.Events;
using NeuralNetGoal.Parser.Models;

namespace NeuralNetGoal.Parser.Fetching;

public sealed record HtmlFetchResult(string Url, int StatusCode, string? Html, string? ErrorCode, string? ErrorMessage)
{
    public bool Ok => ErrorCode is null && Html is not null;
}

/// <summary>The only place that talks to the network. Parsers never see HTTP.</summary>
public interface IHtmlSource
{
    /// <param name="pathOrUrl">Site-relative path ("/soccer/england/") or absolute URL.</param>
    Task<HtmlFetchResult> GetAsync(string pathOrUrl, ScrapeEmitter? emitter = null, CancellationToken ct = default);
}

public sealed class HttpHtmlSource : IHtmlSource
{
    public const string HttpClientName = "neuralnetgoal.betexplorer";

    private static readonly Regex UnsafeFileChars = new("[^a-zA-Z0-9._-]+", RegexOptions.Compiled);

    private readonly IHttpClientFactory _factory;
    private readonly BetexplorerOptions _o;
    private readonly ILogger<HttpHtmlSource>? _log;
    private readonly SemaphoreSlim _gate;
    private readonly string _baseHost;
    private long _dumpCounter;

    public HttpHtmlSource(IHttpClientFactory factory, IOptions<BetexplorerOptions> options, ILogger<HttpHtmlSource>? log = null)
    {
        _factory = factory;
        _o = options.Value;
        _log = log;
        _gate = new SemaphoreSlim(Math.Max(1, _o.MaxConcurrentRequests));
        _baseHost = NormalizeHost(new Uri(_o.BaseUrl).Host);
    }

    public async Task<HtmlFetchResult> GetAsync(string pathOrUrl, ScrapeEmitter? emitter = null, CancellationToken ct = default)
    {
        var url = Resolve(pathOrUrl);
        var maxAttempts = Math.Max(0, _o.MaxRetries) + 1;
        HtmlFetchResult last = new(url, 0, null, IssueCodes.HttpError, "No attempt made");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var retryAfter = TimeSpan.Zero;
            await _gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.TryAddWithoutValidation("User-Agent", _o.UserAgent);
                    req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
                    req.Headers.TryAddWithoutValidation("Accept-Language", _o.AcceptLanguage);
                    if (!string.IsNullOrWhiteSpace(_o.Cookie)) req.Headers.TryAddWithoutValidation("Cookie", _o.Cookie);

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _o.RequestTimeoutSeconds)));

                    using var resp = await _factory.CreateClient(HttpClientName).SendAsync(req, cts.Token).ConfigureAwait(false);
                    var status = (int)resp.StatusCode;

                    // A redirect to another host means a firewall / captive portal / geo-block page, not the site.
                    var finalHost = resp.RequestMessage?.RequestUri?.Host;
                    if (finalHost is not null && NormalizeHost(finalHost) != _baseHost)
                    {
                        last = new(url, status, null, IssueCodes.Blocked, $"Redirected to another host ({finalHost}); the request is being intercepted or blocked.");
                        emitter?.Emit(ScrapeEventKind.PageFetched, ScrapeLevel.Error, last.ErrorMessage!, httpStatus: status, attempt: attempt);
                        return last;
                    }

                    var body = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

                    if (resp.IsSuccessStatusCode)
                    {
                        Dump(url, body);
                        emitter?.Emit(ScrapeEventKind.PageFetched, ScrapeLevel.Debug, url, httpStatus: status, attempt: attempt, durationMs: (int)sw.ElapsedMilliseconds);
                        return new HtmlFetchResult(url, status, body, null, null);
                    }

                    if (resp.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized
                        || (status == 503 && LooksLikeChallenge(body)))
                    {
                        last = new(url, status, null, IssueCodes.Blocked, $"HTTP {status}: access refused or challenged (possible geo/bot block).");
                        emitter?.Emit(ScrapeEventKind.PageFetched, ScrapeLevel.Error, last.ErrorMessage!, httpStatus: status, attempt: attempt);
                        return last;
                    }

                    last = new(url, status, null, IssueCodes.HttpError, $"HTTP {status} {resp.ReasonPhrase}");
                    if (resp.StatusCode == HttpStatusCode.NotFound) // permanent, do not retry
                    {
                        emitter?.Emit(ScrapeEventKind.PageFetched, ScrapeLevel.Warn, last.ErrorMessage!, httpStatus: status, attempt: attempt);
                        return last;
                    }

                    if (resp.Headers.RetryAfter?.Delta is { } delta) retryAfter = delta;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    last = new(url, 0, null, IssueCodes.Timeout, $"Timed out after {_o.RequestTimeoutSeconds}s");
                }
                catch (HttpRequestException ex)
                {
                    last = new(url, 0, null, IssueCodes.HttpError, ex.Message);
                }
                finally
                {
                    // politeness delay while still holding the slot, so the slot count really limits the rate
                    var jitter = Random.Shared.Next(0, Math.Max(1, _o.DelayBetweenRequestsMs / 2));
                    try { await Task.Delay(_o.DelayBetweenRequestsMs + jitter, ct).ConfigureAwait(false); } catch (OperationCanceledException) { }
                }
            }
            finally
            {
                _gate.Release();
            }

            if (attempt == maxAttempts) break;

            var backoff = TimeSpan.FromMilliseconds(_o.RetryBaseDelayMs * Math.Pow(2, attempt - 1));
            if (retryAfter > backoff) backoff = retryAfter;
            emitter?.Emit(ScrapeEventKind.PageRetry, ScrapeLevel.Warn,
                $"{last.ErrorMessage} - retrying in {backoff.TotalSeconds:0.#}s ({url})",
                httpStatus: last.StatusCode == 0 ? null : last.StatusCode, attempt: attempt);
            await Task.Delay(backoff, ct).ConfigureAwait(false);
        }

        emitter?.Emit(ScrapeEventKind.PageFetched, ScrapeLevel.Error, $"Giving up: {last.ErrorMessage} ({url})", httpStatus: last.StatusCode == 0 ? null : last.StatusCode);
        return last;
    }

    private string Resolve(string pathOrUrl)
    {
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var abs) && (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            return abs.ToString();

        var path = "/" + pathOrUrl.TrimStart('/');
        if (_o.RewriteLegacySoccerPaths && path.StartsWith("/soccer/", StringComparison.OrdinalIgnoreCase))
            path = "/football/" + path["/soccer/".Length..];

        return _o.BaseUrl.TrimEnd('/') + path;
    }

    private static string NormalizeHost(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..].ToLowerInvariant() : host.ToLowerInvariant();

    private static bool LooksLikeChallenge(string body) =>
        body.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
        || body.Contains("cf-chl", StringComparison.OrdinalIgnoreCase)
        || body.Contains("captcha", StringComparison.OrdinalIgnoreCase);

    private void Dump(string url, string html)
    {
        if (string.IsNullOrWhiteSpace(_o.DumpDirectory)) return;
        try
        {
            Directory.CreateDirectory(_o.DumpDirectory);
            var path = new Uri(url).PathAndQuery;
            var name = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Interlocked.Increment(ref _dumpCounter):D5}_{UnsafeFileChars.Replace(path.Trim('/'), "_")}";
            if (name.Length > 150) name = name[..150];
            File.WriteAllText(Path.Combine(_o.DumpDirectory, name + ".html"), html);
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Could not dump page {Url}", url);
        }
    }
}
