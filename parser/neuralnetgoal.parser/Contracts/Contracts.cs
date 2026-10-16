using System.Text.Json;
using NeuralNetGoal.Parser.Events;
using NeuralNetGoal.Parser.Models;
using NeuralNetGoal.Parser.Services;

namespace NeuralNetGoal.Parser.Contracts;

// Wire contracts shared by the Web API and its .NET client.

public sealed record StartResultsRequest(LeagueRef League, ResultsOptions? Options = null);
public sealed record StartFixturesRequest(LeagueRef League, FixturesOptions? Options = null);

public sealed record StartJobResponse(string JobId, string StatusUrl, string EventsUrl);

public sealed record JobInfo(
    string Id,
    string Kind,
    string Status,
    string Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Error,
    long LastSeq,
    ScrapeCounters? Counters);

public static class JobStatuses
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";

    public static bool IsFinished(string status) => status is Completed or Failed or Cancelled;
}

/// <summary>A job plus (once it finished) its result, which is a serialized <see cref="ScrapeOutcome{T}"/>.</summary>
public sealed record JobResponse(JobInfo Job, JsonElement? Result);

public sealed record SiteStatus(bool Reachable, int StatusCode, string? Error, DateTimeOffset CheckedAt);

public sealed record HealthResponse(string Status, string Version, SiteStatus Site);
