using System.Security.Claims;
using Tools.NeuralNetGoal.Service.Workflow;
using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Service.Endpoints;

public static class JobEndpoints
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void MapJobEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/jobs");

        g.MapGet("", (JobManager jobs) => jobs.List().Select(j => j.ToSummary())).RequireAuthorization(Policies.Read);

        // The browser polls this once a second with the last sequence number it has, and gets only the new log lines.
        g.MapGet("/{id}", (string id, JobManager jobs, long after = 0) =>
            jobs.Get(id) is { } job ? Results.Ok(job.ToStatus(after)) : Results.NotFound()).RequireAuthorization(Policies.Read);

        g.MapPost("", (StartJobRequest req, ClaimsPrincipal user, JobManager jobs) =>
        {
            var job = jobs.TryStart(req, user.Identity?.Name ?? "?", out var problem);
            return job is null ? Results.Conflict(new { error = problem }) : Results.Ok(job.ToStatus(0));
        }).RequireAuthorization(Policies.Run);

        g.MapDelete("/{id}", (string id, JobManager jobs) => jobs.Cancel(id) ? Results.Accepted() : Results.NotFound())
            .RequireAuthorization(Policies.Run);

        g.MapGet("/{id}/file", (string id, JobManager jobs) =>
        {
            var job = jobs.Get(id);
            if (job?.FilePath is null || !File.Exists(job.FilePath)) return Results.NotFound();
            return Results.File(job.FilePath, Xlsx, job.FileName);
        }).RequireAuthorization(Policies.Read);
    }
}
