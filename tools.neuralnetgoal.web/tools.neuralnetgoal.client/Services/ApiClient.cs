using System.Net;
using System.Net.Http.Json;
using Tools.NeuralNetGoal.Shared;

namespace Tools.NeuralNetGoal.Client.Services;

/// <summary>Adds the header the service requires on every non-GET call (a cross-site page cannot add it).</summary>
public sealed class CsrfHeaderHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.TryAddWithoutValidation("X-NNG-Request", "1");
        return base.SendAsync(request, ct);
    }
}

public sealed record ApiResult<T>(bool Ok, T? Value, string? Error, HttpStatusCode Status);

/// <summary>Every call to the service. Returns a result with the server's error message instead of throwing.</summary>
public sealed class ApiClient(HttpClient http)
{
    private sealed record ErrorBody(string? Error);

    public Task<ApiResult<UserInfo>> LoginAsync(string user, string password) => PostAsync<UserInfo>("api/auth/login", new LoginRequest(user, password));
    public Task<ApiResult<UserInfo>> ChangePasswordAsync(string current, string next) => PostAsync<UserInfo>("api/auth/change-password", new ChangePasswordRequest(current, next));
    public Task<ApiResult<SetupStatus>> SetupStatusAsync() => GetAsync<SetupStatus>("api/auth/setup-status");
    public Task<ApiResult<UserInfo>> SetupAsync(string password) => PostAsync<UserInfo>("api/auth/setup", new SetupRequest(password));
    public async Task LogoutAsync() { try { await http.PostAsync("api/auth/logout", null); } catch { /* the page reloads anyway */ } }
    public Task<ApiResult<UserInfo>> MeAsync() => GetAsync<UserInfo>("api/auth/me");

    public Task<ApiResult<List<LeagueDto>>> LeaguesAsync() => GetAsync<List<LeagueDto>>("api/leagues");
    public Task<ApiResult<List<ErrorRowDto>>> ErrorsAsync(int top = 200) => GetAsync<List<ErrorRowDto>>($"api/errors?top={top}");
    public Task<ApiResult<HealthDto>> HealthAsync() => GetAsync<HealthDto>("api/health");

    public Task<ApiResult<JobStatusDto>> StartJobAsync(StartJobRequest r) => PostAsync<JobStatusDto>("api/jobs", r);
    public Task<ApiResult<JobStatusDto>> JobAsync(string id, long after) => GetAsync<JobStatusDto>($"api/jobs/{id}?after={after}");
    public Task<ApiResult<List<JobSummaryDto>>> JobsAsync() => GetAsync<List<JobSummaryDto>>("api/jobs");
    public async Task CancelJobAsync(string id) { try { await http.DeleteAsync($"api/jobs/{id}"); } catch { } }

    public Task<ApiResult<List<UserDto>>> UsersAsync() => GetAsync<List<UserDto>>("api/users");
    public Task<ApiResult<List<UserDto>>> CreateUserAsync(CreateUserRequest r) => PostAsync<List<UserDto>>("api/users", r);
    public Task<ApiResult<List<UserDto>>> SetActiveAsync(int id, bool active) => PostAsync<List<UserDto>>($"api/users/{id}/active", new SetActiveRequest(active));

    private async Task<ApiResult<T>> GetAsync<T>(string url)
    {
        try { return await ReadAsync<T>(await http.GetAsync(url)); }
        catch (Exception ex) { return new(false, default, ex.Message, 0); }
    }

    private async Task<ApiResult<T>> PostAsync<T>(string url, object body)
    {
        try { return await ReadAsync<T>(await http.PostAsJsonAsync(url, body)); }
        catch (Exception ex) { return new(false, default, ex.Message, 0); }
    }

    private static async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage r)
    {
        if (r.IsSuccessStatusCode)
            return new(true, r.Content.Headers.ContentLength == 0 ? default : await r.Content.ReadFromJsonAsync<T>(), null, r.StatusCode);

        string? message = null;
        try { message = (await r.Content.ReadFromJsonAsync<ErrorBody>())?.Error; } catch { /* not JSON */ }
        message ??= r.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "You are not signed in.",
            HttpStatusCode.Forbidden => "You do not have permission for this.",
            _ => $"The server answered {(int)r.StatusCode} {r.ReasonPhrase}.",
        };
        return new(false, default, message, r.StatusCode);
    }
}
