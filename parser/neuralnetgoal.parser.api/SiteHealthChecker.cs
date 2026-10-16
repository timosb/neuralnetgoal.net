using Microsoft.Extensions.Options;
using NeuralNetGoal.Parser.Contracts;
using NeuralNetGoal.Parser.Fetching;

namespace NeuralNetGoal.Parser.Api;

/// <summary>Checks whether the VPS can actually reach betexplorer (detects geo-blocks / firewalls). Cached for a minute
/// so that polling /health does not hit the site.</summary>
public sealed class SiteHealthChecker(IHtmlSource html, IOptions<BetexplorerOptions> options)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private SiteStatus? _last;

    public async Task<SiteStatus> CheckAsync(CancellationToken ct)
    {
        if (_last is { } c && DateTimeOffset.UtcNow - c.CheckedAt < TimeSpan.FromSeconds(60)) return c;

        await _lock.WaitAsync(ct);
        try
        {
            if (_last is { } c2 && DateTimeOffset.UtcNow - c2.CheckedAt < TimeSpan.FromSeconds(60)) return c2;

            var r = await html.GetAsync(options.Value.BaseUrl, emitter: null, ct);
            _last = new SiteStatus(r.Ok, r.StatusCode, r.ErrorMessage, DateTimeOffset.UtcNow);
            return _last;
        }
        finally
        {
            _lock.Release();
        }
    }
}
