using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MindAttic.Ideas.Core.Data;

namespace MindAttic.Ideas.Core.Services;

/// <summary>How often the sweep looks, and whether it runs at all.</summary>
public sealed class SandboxSweepOptions
{
    /// <summary>Off by default. A deployment with no showroom must not run a delete loop at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>How often to look for an idle showroom. Not how long a site must be idle — that is the site's own grace.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Absolute path to the baseline bundle a showroom restores to. Null = a reset empties the site.</summary>
    public string? BaselineBundlePath { get; set; }
}

/// <summary>
/// Restores an idle showroom to Day Zero (MAI-A38). Runs only when
/// <see cref="SandboxSweepOptions.Enabled"/> is set, so a deployment without a showroom never starts a
/// background loop whose job is deleting things.
/// <para>
/// The sweep decides nothing. It asks <see cref="ISandboxService.DueForResetAsync"/> which sites are
/// idle past their grace period and hands each id to <see cref="ISandboxResetService.ResetAsync"/>,
/// which re-gates before touching a row. Two independent checks of the same authority, because a
/// background loop is the one caller nobody is watching.
/// </para>
/// </summary>
public sealed class SandboxResetSweep(
    IServiceScopeFactory scopes,
    SandboxSweepOptions options,
    ILogger<SandboxResetSweep> log,
    TimeProvider? time = null) : BackgroundService
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            log.LogInformation("Sandbox reset sweep is off; no showroom will be reset automatically.");
            return;
        }

        log.LogInformation("Sandbox reset sweep is on; checking every {Interval}.", options.Interval);
        using var timer = new PeriodicTimer(options.Interval, _time);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;   // shutting down
            }
            catch (Exception ex)
            {
                // A failed sweep must never take the host down or stop the loop: the next tick retries.
                log.LogError(ex, "Sandbox reset sweep failed; will try again at the next interval.");
            }

            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>One pass. Exposed so a test can drive it without waiting on a timer.</summary>
    public async Task SweepOnceAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sandbox = scope.ServiceProvider.GetRequiredService<ISandboxService>();
        var reset = scope.ServiceProvider.GetRequiredService<ISandboxResetService>();

        var now = _time.GetUtcNow().UtcDateTime;
        var due = await sandbox.DueForResetAsync(now, ct);
        if (due.Count == 0) return;

        foreach (var site in due)
        {
            var outcome = await reset.ResetAsync(site.Id, now, ct);
            if (outcome.Ok)
                log.LogInformation("Showroom reset: {Explanation} ({Pages} page(s), {Packages} package(s) dropped.)",
                    outcome.Explanation, outcome.PagesRemoved, outcome.PackagesRemoved);
            else
                log.LogWarning("Showroom NOT reset ({Refusal}): {Explanation}", outcome.Refusal, outcome.Explanation);
        }
    }
}

/// <summary>
/// A baseline read from a file on disk. The path is configuration, so a deployment points its showroom
/// at whichever bundle it wants to demonstrate without a code change.
/// </summary>
public sealed class FileSandboxBaselineSource(SandboxSweepOptions options) : ISandboxBaselineSource
{
    public Task<Stream?> OpenAsync(Entities.Site site, CancellationToken ct = default)
    {
        var path = options.BaselineBundlePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(path));
    }
}
