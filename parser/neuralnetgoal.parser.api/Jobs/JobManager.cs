using System.Collections.Concurrent;
using NeuralNetGoal.Parser.Contracts;
using NeuralNetGoal.Parser.Events;
using NeuralNetGoal.Parser.Models;
using NeuralNetGoal.Parser.Services;

namespace NeuralNetGoal.Parser.Api.Jobs;

public sealed class JobOptions
{
    public int MaxConcurrentJobs { get; set; } = 2;
    public int RetentionHours { get; set; } = 6;
    /// <summary>How many events per job are kept for replay.</summary>
    public int EventBufferSize { get; set; } = 5000;
}

public sealed class JobManager
{
    private readonly ConcurrentDictionary<string, Job> _jobs = new();
    private readonly SemaphoreSlim _slots;
    private readonly JobOptions _opt;
    private readonly IResultsScraper _results;
    private readonly IFixturesScraper _fixtures;
    private readonly ILogger<JobManager> _log;

    public JobManager(
        IResultsScraper results,
        IFixturesScraper fixtures,
        Microsoft.Extensions.Options.IOptions<JobOptions> options,
        ILogger<JobManager> log)
    {
        _results = results;
        _fixtures = fixtures;
        _opt = options.Value;
        _log = log;
        _slots = new SemaphoreSlim(Math.Max(1, _opt.MaxConcurrentJobs));
    }

    public Job StartResults(LeagueRef league, ResultsOptions? options) =>
        Start(JobKind.Results, $"Results {league.Country} / {league.Division} / {league.Season}",
            (job, ct) => _results.GetResultsAsync(league, options, job, ct));

    public Job StartFixtures(LeagueRef league, FixturesOptions? options) =>
        Start(JobKind.Fixtures, $"Fixtures {league.Country} / {league.Division} / {league.Season}",
            (job, ct) => _fixtures.GetFixturesAsync(league, options, job, ct));

    public Job? Get(string id) => _jobs.TryGetValue(id, out var j) ? j : null;

    public IEnumerable<Job> List() => _jobs.Values.OrderByDescending(j => j.CreatedAt);

    public bool Cancel(string id)
    {
        if (!_jobs.TryGetValue(id, out var job) || job.IsFinished) return false;
        job.Cts.Cancel();
        return true;
    }

    public int RemoveExpired()
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromHours(Math.Max(1, _opt.RetentionHours));
        var removed = 0;
        foreach (var job in _jobs.Values.Where(j => j.IsFinished && j.FinishedAt < cutoff).ToList())
        {
            if (_jobs.TryRemove(job.Id, out var j)) { j.Cts.Dispose(); removed++; }
        }
        return removed;
    }

    private Job Start<T>(JobKind kind, string description, Func<Job, CancellationToken, Task<ScrapeOutcome<T>>> work)
    {
        var job = new Job(Guid.NewGuid().ToString("N")[..12], kind, description, _opt.EventBufferSize);
        _jobs[job.Id] = job;

        _ = Task.Run(async () =>
        {
            using var scope = _log.BeginScope(new Dictionary<string, object> { ["JobId"] = job.Id });
            try
            {
                await _slots.WaitAsync(job.Cts.Token).ConfigureAwait(false);
                try
                {
                    job.Status = JobStatuses.Running;
                    job.StartedAt = DateTimeOffset.UtcNow;
                    job.Append(new ScrapeEvent { Kind = ScrapeEventKind.JobStarted, Level = ScrapeLevel.Info, Message = description });

                    var outcome = await work(job, job.Cts.Token).ConfigureAwait(false);

                    job.Result = outcome;
                    job.Status = JobStatuses.Completed;
                    job.Append(new ScrapeEvent
                    {
                        Kind = ScrapeEventKind.JobCompleted,
                        Level = outcome.HasIssues ? ScrapeLevel.Warn : ScrapeLevel.Info,
                        Message = $"{outcome.Items.Count} items, {outcome.Issues.Count} issues, {outcome.Stats.PagesFetched} pages in {outcome.Stats.Duration.TotalSeconds:0.#}s.",
                        Counters = job.LastCounters,
                    });
                }
                finally
                {
                    _slots.Release();
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = JobStatuses.Cancelled;
                job.Append(new ScrapeEvent { Kind = ScrapeEventKind.JobCancelled, Level = ScrapeLevel.Warn, Message = "Job cancelled." });
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Job {JobId} failed", job.Id);
                job.Status = JobStatuses.Failed;
                job.Error = ex.Message;
                job.Append(new ScrapeEvent { Kind = ScrapeEventKind.JobFailed, Level = ScrapeLevel.Error, Message = ex.Message });
            }
            finally
            {
                job.FinishedAt = DateTimeOffset.UtcNow;
                job.CloseEvents();
            }
        });

        return job;
    }
}

public sealed class JobCleanupService(JobManager jobs, ILogger<JobCleanupService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var n = jobs.RemoveExpired();
            if (n > 0) log.LogInformation("Removed {Count} expired jobs", n);
        }
    }
}
