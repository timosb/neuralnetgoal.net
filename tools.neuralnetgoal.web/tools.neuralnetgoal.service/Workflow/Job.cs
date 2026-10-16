using NeuralNetGoal.Parser.Events;
using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Service.Workflow;

/// <summary>One run of results / tables / fixtures / all / training. It is also the parser's event sink, so every match line the
/// parser reports lands in the job log, which the browser polls.</summary>
public sealed class Job : IScrapeEventSink
{
    private const int MaxLines = 5000;
    private readonly object _lock = new();
    private readonly List<JobLogLine> _lines = [];
    private long _seq;

    public Job(string command, StartJobRequest request, string startedBy)
    {
        Id = Guid.NewGuid().ToString("N")[..12];
        Command = command;
        Request = request;
        StartedBy = startedBy;
    }

    public string Id { get; }
    public string Command { get; }
    public StartJobRequest Request { get; }
    public string StartedBy { get; }
    public DateTime StartedUtc { get; } = DateTime.UtcNow;
    public DateTime? FinishedUtc { get; private set; }
    public string State { get; private set; } = JobStates.Running;
    public string? Description { get; set; }
    public string? Error { get; private set; }
    public string? FilePath { get; set; }
    public string? FileName => FilePath is null ? null : Path.GetFileName(FilePath);
    public CancellationTokenSource Cts { get; } = new();

    public void Log(string level, string text)
    {
        lock (_lock)
        {
            _lines.Add(new JobLogLine(++_seq, DateTime.UtcNow, level, text));
            if (_lines.Count > MaxLines) _lines.RemoveRange(0, _lines.Count - MaxLines);
        }
    }

    public void Info(string text) => Log("Info", text);
    public void Warn(string text) => Log("Warn", text);
    public void Fail(string text) => Log("Error", text);
    public void Header(string text) => Log("Header", text);

    /// <summary>The parser's events, one line per match like the old console printed.</summary>
    public void Emit(ScrapeEvent e)
    {
        if (e.Level == ScrapeLevel.Debug) return;
        if (e.Kind is ScrapeEventKind.MatchStarted or ScrapeEventKind.PageFetched) return;

        var level = e.Level switch
        {
            ScrapeLevel.Error => "Error",
            ScrapeLevel.Warn => "Warn",
            _ => e.Kind == ScrapeEventKind.MatchParsed ? "Ok" : "Info",
        };
        var progress = e.Counters is { Total: > 0 } c ? $"[{c.Done + c.Failed + c.Skipped}/{c.Total}] " : "";
        var teams = e.Home is null ? "" : $"{e.Home} - {e.Away}  ";
        Log(level, $"{progress}{e.Kind} {teams}{e.Message}".Trim());
    }

    public void Complete() { State = JobStates.Completed; FinishedUtc = DateTime.UtcNow; }
    public void Cancelled() { State = JobStates.Cancelled; FinishedUtc = DateTime.UtcNow; Info("Cancelled."); }
    public void Failed(string error) { State = JobStates.Failed; Error = error; FinishedUtc = DateTime.UtcNow; Fail(error); }

    public JobSummaryDto ToSummary() => new(Id, Command, State, StartedUtc, FinishedUtc, Description);

    public JobStatusDto ToStatus(long after)
    {
        lock (_lock)
        {
            var lines = _lines.Where(l => l.Seq > after).ToArray();
            return new JobStatusDto(Id, Command, State, Description, StartedUtc, FinishedUtc, Error, FileName, _seq, lines);
        }
    }
}
