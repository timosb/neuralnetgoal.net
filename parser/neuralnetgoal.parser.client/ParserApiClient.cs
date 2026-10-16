using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using NeuralNetGoal.Parser.Contracts;
using NeuralNetGoal.Parser.Events;
using NeuralNetGoal.Parser.Models;
using NeuralNetGoal.Parser.Services;

namespace NeuralNetGoal.Parser.Client;

public sealed class ParserApiException(string message, int? statusCode = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}

/// <summary>
/// Typed client for the parser Web API. Besides raw calls it exposes <see cref="Results"/>, <see cref="Fixtures"/> and
/// <see cref="Catalog"/>, which implement the same interfaces as the in-process library: code written against
/// <see cref="IResultsScraper"/> does not care whether the parser runs locally or on the VPS.
/// </summary>
public sealed class ParserApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public ParserApiClient(HttpClient http, bool ownsHttp = false)
    {
        _http = http;
        _ownsHttp = ownsHttp;
        Results = new RemoteResultsScraper(this);
        Fixtures = new RemoteFixturesScraper(this);
        Catalog = new RemoteCatalogScraper(this);
    }

    public static ParserApiClient Create(string baseUrl, string? apiKey)
    {
        var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"), Timeout = Timeout.InfiniteTimeSpan };
        if (!string.IsNullOrWhiteSpace(apiKey)) http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return new ParserApiClient(http, ownsHttp: true);
    }

    public IResultsScraper Results { get; }
    public IFixturesScraper Fixtures { get; }
    public ILeagueCatalogScraper Catalog { get; }

    // ------------------------------------------------------------------ plain calls

    public Task<HealthResponse> GetHealthAsync(CancellationToken ct = default) => GetAsync<HealthResponse>("health", ct);

    public Task<StartJobResponse> StartResultsAsync(LeagueRef league, ResultsOptions? options = null, CancellationToken ct = default) =>
        PostAsync<StartJobResponse>("v1/results/scrape", new StartResultsRequest(league, options), ct);

    public Task<StartJobResponse> StartFixturesAsync(LeagueRef league, FixturesOptions? options = null, CancellationToken ct = default) =>
        PostAsync<StartJobResponse>("v1/fixtures/scrape", new StartFixturesRequest(league, options), ct);

    public Task<JobResponse> GetJobAsync(string jobId, CancellationToken ct = default) => GetAsync<JobResponse>($"v1/jobs/{jobId}", ct);

    public async Task CancelJobAsync(string jobId, CancellationToken ct = default)
    {
        using var resp = await _http.DeleteAsync($"v1/jobs/{jobId}", ct).ConfigureAwait(false);
        // 409 = already finished, which is fine
        if (!resp.IsSuccessStatusCode && (int)resp.StatusCode != 409) await ThrowAsync(resp).ConfigureAwait(false);
    }

    internal Task<ScrapeOutcome<CountryRef>> GetCountriesAsync(CancellationToken ct) =>
        GetAsync<ScrapeOutcome<CountryRef>>("v1/countries", ct);

    internal Task<ScrapeOutcome<LeagueSeasonRef>> GetLeagueSeasonsAsync(CountryRef c, CancellationToken ct) =>
        GetAsync<ScrapeOutcome<LeagueSeasonRef>>(
            $"v1/leagues?countryName={Uri.EscapeDataString(c.Name)}&countryUrlPath={Uri.EscapeDataString(c.UrlPath)}", ct);

    // ------------------------------------------------------------------ live events

    /// <summary>
    /// Streams a job's events as they happen. Reconnects automatically (resuming after the last event it saw, so nothing
    /// is lost) if the connection drops, and completes when the job reaches a final state.
    /// </summary>
    public async IAsyncEnumerable<ScrapeEvent> StreamEventsAsync(
        string jobId,
        long afterSeq = 0,
        ScrapeLevel? minLevel = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var last = afterSeq;
        var failures = 0;

        while (!ct.IsCancellationRequested)
        {
            var url = $"v1/jobs/{jobId}/events?after={last}" + (minLevel is null ? "" : $"&level={minLevel}");
            var sawTerminal = false;
            Stream? stream = null;
            StreamReader? reader = null;
            HttpResponseMessage? resp = null;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Accept.ParseAdd("text/event-stream");
                resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) await ThrowAsync(resp).ConfigureAwait(false);

                stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                reader = new StreamReader(stream);
                failures = 0;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException && !ct.IsCancellationRequested)
            {
                resp?.Dispose();
                if (++failures > 5) throw new ParserApiException($"Lost connection to the parser API: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(10, failures * 2)), ct).ConfigureAwait(false);
                continue;
            }

            using (resp)
            using (reader)
            {
                var data = new System.Text.StringBuilder();
                string? line;
                while (true)
                {
                    try { line = await reader.ReadLineAsync(ct).ConfigureAwait(false); }
                    catch (Exception ex) when (ex is IOException or HttpRequestException && !ct.IsCancellationRequested) { line = null; }
                    if (line is null) break; // stream ended or dropped

                    if (line.Length == 0) // blank line = dispatch the event
                    {
                        if (data.Length > 0)
                        {
                            var e = JsonSerializer.Deserialize<ScrapeEvent>(data.ToString(), ParserJson.Options);
                            data.Clear();
                            if (e is not null)
                            {
                                if (e.Seq > last) last = e.Seq;
                                yield return e;
                                if (e.Kind is ScrapeEventKind.JobCompleted or ScrapeEventKind.JobFailed or ScrapeEventKind.JobCancelled)
                                    sawTerminal = true;
                            }
                        }
                        continue;
                    }

                    if (line[0] == ':') continue; // comment / heartbeat
                    if (line.StartsWith("data:", StringComparison.Ordinal)) data.Append(line.AsSpan(5).TrimStart());
                }
            }

            if (sawTerminal) yield break;

            // The stream ended without a final event: either a dropped connection, or the job is long finished and
            // already trimmed. Ask the server which it is.
            var job = await GetJobAsync(jobId, ct).ConfigureAwait(false);
            if (JobStatuses.IsFinished(job.Job.Status) && job.Job.LastSeq <= last) yield break;
            await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
        }
    }

    /// <summary>Forwards a job's events to <paramref name="events"/> until it ends, then returns its result.</summary>
    internal async Task<ScrapeOutcome<T>> WaitForOutcomeAsync<T>(string jobId, IScrapeEventSink? events, CancellationToken ct)
    {
        try
        {
            await foreach (var e in StreamEventsAsync(jobId, ct: ct).ConfigureAwait(false))
            {
                try { events?.Emit(e); } catch { /* a broken listener must not break the call */ }
            }
        }
        catch (OperationCanceledException)
        {
            // caller cancelled: stop the job on the server too, then surface the cancellation
            try { await CancelJobAsync(jobId, CancellationToken.None).ConfigureAwait(false); } catch { }
            throw;
        }

        var job = await GetJobAsync(jobId, ct).ConfigureAwait(false);
        if (job.Job.Status != JobStatuses.Completed)
            throw new ParserApiException($"Job {jobId} ended as {job.Job.Status}: {job.Job.Error ?? "no details"}");
        if (job.Result is not { } result)
            throw new ParserApiException($"Job {jobId} completed without a result.");

        return result.Deserialize<ScrapeOutcome<T>>(ParserJson.Options)
               ?? throw new ParserApiException($"Job {jobId} returned an unreadable result.");
    }

    // ------------------------------------------------------------------ helpers

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) await ThrowAsync(resp).ConfigureAwait(false);
        return (await resp.Content.ReadFromJsonAsync<T>(ParserJson.Options, ct).ConfigureAwait(false))!;
    }

    private async Task<T> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync(url, body, ParserJson.Options, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) await ThrowAsync(resp).ConfigureAwait(false);
        return (await resp.Content.ReadFromJsonAsync<T>(ParserJson.Options, ct).ConfigureAwait(false))!;
    }

    private static async Task ThrowAsync(HttpResponseMessage resp)
    {
        var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw new ParserApiException($"Parser API returned {(int)resp.StatusCode} {resp.ReasonPhrase}: {text}", (int)resp.StatusCode);
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}

internal sealed class RemoteResultsScraper(ParserApiClient api) : IResultsScraper
{
    public async Task<ScrapeOutcome<MatchResult>> GetResultsAsync(LeagueRef league, ResultsOptions? options = null, IScrapeEventSink? events = null, CancellationToken ct = default)
    {
        var job = await api.StartResultsAsync(league, options, ct).ConfigureAwait(false);
        return await api.WaitForOutcomeAsync<MatchResult>(job.JobId, events, ct).ConfigureAwait(false);
    }
}

internal sealed class RemoteFixturesScraper(ParserApiClient api) : IFixturesScraper
{
    public async Task<ScrapeOutcome<Fixture>> GetFixturesAsync(LeagueRef league, FixturesOptions? options = null, IScrapeEventSink? events = null, CancellationToken ct = default)
    {
        var job = await api.StartFixturesAsync(league, options, ct).ConfigureAwait(false);
        return await api.WaitForOutcomeAsync<Fixture>(job.JobId, events, ct).ConfigureAwait(false);
    }
}

internal sealed class RemoteCatalogScraper(ParserApiClient api) : ILeagueCatalogScraper
{
    public Task<ScrapeOutcome<CountryRef>> GetCountriesAsync(IScrapeEventSink? events = null, CancellationToken ct = default) =>
        api.GetCountriesAsync(ct);

    public Task<ScrapeOutcome<LeagueSeasonRef>> GetLeagueSeasonsAsync(CountryRef country, IScrapeEventSink? events = null, CancellationToken ct = default) =>
        api.GetLeagueSeasonsAsync(country, ct);
}
