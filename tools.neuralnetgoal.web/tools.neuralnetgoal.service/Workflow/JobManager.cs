using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Service.Workflow;

/// <summary>Runs jobs in the background (one at a time: they write to the same tables) and remembers the latest ones in memory.
/// Jobs are lost when the application restarts; the data they saved is not.</summary>
public sealed class JobManager(IServiceScopeFactory scopes, ILogger<JobManager> log)
{
    private const int KeepJobs = 20;
    private readonly object _lock = new();
    private readonly List<Job> _jobs = [];

    private static readonly string[] Commands = [JobCommands.Results, JobCommands.Tables, JobCommands.Fixtures, JobCommands.All, JobCommands.Training];

    /// <summary>Starts a job, or returns null (with the reason) when one is already running or the command is unknown.</summary>
    public Job? TryStart(StartJobRequest request, string userName, out string? problem)
    {
        var command = (request.Command ?? "").Trim().ToLowerInvariant();
        if (!Commands.Contains(command)) { problem = $"Unknown command '{request.Command}'."; return null; }

        Job job;
        lock (_lock)
        {
            if (_jobs.Any(j => j.State == JobStates.Running)) { problem = "Another job is still running. Wait for it to finish or cancel it."; return null; }
            job = new Job(command, request with { Command = command }, userName);
            _jobs.Insert(0, job);
            if (_jobs.Count > KeepJobs) { DeleteFile(_jobs[^1]); _jobs.RemoveAt(_jobs.Count - 1); }
        }

        problem = null;
        job.Description = Describe(job);
        _ = Task.Run(() => RunAsync(job));
        return job;
    }

    public Job? Get(string id) { lock (_lock) return _jobs.FirstOrDefault(j => j.Id == id); }
    public List<Job> List() { lock (_lock) return [.. _jobs]; }

    public bool Cancel(string id)
    {
        var job = Get(id);
        if (job is null || job.State != JobStates.Running) return false;
        job.Cts.Cancel();
        return true;
    }

    private async Task RunAsync(Job job)
    {
        job.Info($"Started by {job.StartedBy}: {job.Description}");
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<WorkflowService>().RunAsync(job, job.Cts.Token);
            job.Complete();
            job.Info("Finished.");
        }
        catch (OperationCanceledException) { job.Cancelled(); }
        catch (Exception ex)
        {
            log.LogError(ex, "Job {Id} ({Command}) failed", job.Id, job.Command);
            job.Failed(ex.Message);
        }
    }

    private static string Describe(Job job)
    {
        var r = job.Request;
        var parts = new List<string> { job.Command };
        if (!string.IsNullOrWhiteSpace(r.Country)) parts.Add(r.Country!);
        if (!string.IsNullOrWhiteSpace(r.Division)) parts.Add(r.Division!);
        if (r.Recent is { } n) parts.Add($"recent {n}");
        return string.Join(" / ", parts);
    }

    private static void DeleteFile(Job job)
    {
        try { if (job.FilePath is not null && File.Exists(job.FilePath)) File.Delete(job.FilePath); } catch { /* best effort */ }
    }
}
