using System.Runtime.CompilerServices;
using NeuralNetGoal.Parser.Contracts;
using NeuralNetGoal.Parser.Events;

namespace NeuralNetGoal.Parser.Api.Jobs;

public enum JobKind { Results, Fixtures }

/// <summary>
/// One scrape run. It is also the event sink handed to the scraper: every event gets a sequence number and goes into a
/// bounded buffer. Subscribers read from that buffer by sequence number, so the scrape never waits for a client, a late
/// client can replay what it missed, and a reconnecting client resumes with "after = last seen Seq".
/// </summary>
public sealed class Job : IScrapeEventSink
{
    private readonly object _lock = new();
    private readonly List<ScrapeEvent> _events = new();
    private readonly int _capacity;
    private long _nextSeq = 1;
    private long _firstSeq = 1;
    private bool _eventsClosed;
    private TaskCompletionSource _signal = NewSignal();

    public Job(string id, JobKind kind, string description, int eventCapacity)
    {
        Id = id;
        Kind = kind;
        Description = description;
        _capacity = Math.Max(100, eventCapacity);
    }

    public string Id { get; }
    public JobKind Kind { get; }
    public string Description { get; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = JobStatuses.Queued;
    public string? Error { get; set; }
    public object? Result { get; set; }
    public ScrapeCounters? LastCounters { get; private set; }
    public CancellationTokenSource Cts { get; } = new();

    public long LastSeq { get { lock (_lock) return _nextSeq - 1; } }

    public bool IsFinished => JobStatuses.IsFinished(Status);

    /// <summary>Called by the scraper (any thread).</summary>
    public void Emit(ScrapeEvent e) => Append(e);

    public void Append(ScrapeEvent e)
    {
        TaskCompletionSource toSignal;
        lock (_lock)
        {
            if (_eventsClosed) return;
            var stamped = e with { Seq = _nextSeq++ };
            _events.Add(stamped);
            if (stamped.Counters is not null) LastCounters = stamped.Counters;

            if (_events.Count > _capacity + 500) // trim in chunks so we are not shifting the list on every event
            {
                _events.RemoveRange(0, 500);
                _firstSeq += 500;
            }

            toSignal = _signal;
            _signal = NewSignal();
        }
        toSignal.TrySetResult();
    }

    /// <summary>No more events will be added; readers drain what is left and complete.</summary>
    public void CloseEvents()
    {
        TaskCompletionSource toSignal;
        lock (_lock)
        {
            _eventsClosed = true;
            toSignal = _signal;
            _signal = NewSignal();
        }
        toSignal.TrySetResult();
    }

    /// <summary>
    /// Streams events with Seq greater than <paramref name="afterSeq"/>. Yields <c>null</c> as a heartbeat when idle.
    /// If the requested position was already trimmed, a <see cref="ScrapeEventKind.Gap"/> event says how many were lost.
    /// </summary>
    public async IAsyncEnumerable<ScrapeEvent?> ReadAsync(long afterSeq, TimeSpan heartbeat, [EnumeratorCancellation] CancellationToken ct)
    {
        var next = afterSeq + 1;

        while (!ct.IsCancellationRequested)
        {
            List<ScrapeEvent> batch;
            Task signalTask;
            bool closed;
            long dropped = 0;

            lock (_lock)
            {
                if (next < _firstSeq)
                {
                    dropped = _firstSeq - next;
                    next = _firstSeq;
                }

                var start = (int)(next - _firstSeq);
                batch = start < _events.Count ? _events.GetRange(start, _events.Count - start) : new List<ScrapeEvent>();
                signalTask = _signal.Task;
                closed = _eventsClosed;
            }

            if (dropped > 0)
            {
                yield return new ScrapeEvent
                {
                    Seq = 0,
                    Level = ScrapeLevel.Warn,
                    Kind = ScrapeEventKind.Gap,
                    Message = $"{dropped} older events are no longer buffered.",
                };
            }

            foreach (var e in batch)
            {
                yield return e;
                next = e.Seq + 1;
            }

            if (batch.Count > 0) continue;
            if (closed) yield break;

            var delay = Task.Delay(heartbeat, ct);
            var finished = await Task.WhenAny(signalTask, delay).ConfigureAwait(false);
            if (ct.IsCancellationRequested) yield break;
            if (finished == delay) yield return null; // heartbeat
        }
    }

    public JobInfo ToInfo() => new(
        Id, Kind.ToString(), Status, Description, CreatedAt, StartedAt, FinishedAt, Error, LastSeq, LastCounters);

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
