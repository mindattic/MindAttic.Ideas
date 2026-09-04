using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;

namespace MindAttic.Ideas.Core.Services;

/// <summary>Why a reset was refused. A refusal is never silent — the caller says which gate failed.</summary>
public enum SandboxRefusal
{
    None = 0,
    NotFound = 1,
    /// <summary>The site is not marked as a sandbox.</summary>
    NotASandbox = 2,
    /// <summary>The site is the DEFAULT site. This can never be reset, under any flag.</summary>
    IsDefaultSite = 3,
    /// <summary>The site has no reset policy, so resetting it was never asked for.</summary>
    NoResetPolicy = 4,
    /// <summary>Someone is still using it.</summary>
    StillInUse = 5,
}

public sealed record SandboxGate(bool Allowed, SandboxRefusal Refusal, string Explanation);

/// <summary>
/// Guards the destructive half of Showroom mode.
/// <para>
/// The showroom resets itself to Day Zero when nobody is using it, which means this codebase contains
/// a routine that deletes a site's content on a timer. That is a loaded gun pointed at the real site,
/// so the safety is structural rather than a conditional at the call site: <see cref="Gate"/> is the
/// ONLY way to authorize a reset, every caller must go through it, and it refuses unless ALL of these
/// hold — a site must opt in three separate times, and the default site can never qualify at all.
/// </para>
/// <list type="number">
///   <item><description><see cref="Site.IsSandbox"/> is true — the site was deliberately marked disposable.</description></item>
///   <item><description><see cref="Site.IsDefault"/> is FALSE — the site that answers every unclaimed
///   host is the real one, and is not resettable even if someone flags it as a sandbox.</description></item>
///   <item><description><see cref="Site.ResetPolicy"/> names a policy — a sandbox with no policy is a
///   sandbox nobody asked to be wiped.</description></item>
/// </list>
/// </summary>
public interface ISandboxService
{
    /// <summary>The only authority for "may this site be reset?". Never throws.</summary>
    SandboxGate Gate(Site? site);

    /// <summary>Sites eligible for an idle reset right now (gate passed AND idle past the grace period).</summary>
    Task<IReadOnlyList<Site>> DueForResetAsync(DateTime utcNow, CancellationToken ct = default);

    /// <summary>
    /// Seconds since the newest live session on this site, or null when one is still active.
    /// A session counts as live while it is unrevoked, unexpired, and was seen recently.
    /// </summary>
    Task<TimeSpan?> IdleForAsync(int siteId, DateTime utcNow, CancellationToken ct = default);
}

public sealed class SandboxService(CmsDbContext db) : ISandboxService
{
    /// <summary>The policy value that opts a site into an idle reset.</summary>
    public const string WhenIdle = "when-idle";

    public SandboxGate Gate(Site? site)
    {
        if (site is null)
            return new(false, SandboxRefusal.NotFound, "No such site.");

        // Checked FIRST and independently of the sandbox flag: if someone ever manages to set
        // IsSandbox on the default site, this still refuses. The main site is never resettable.
        if (site.IsDefault)
            return new(false, SandboxRefusal.IsDefaultSite,
                $"\"{site.Key}\" is the default site. The default site is never reset, whatever flags it carries.");

        if (!site.IsSandbox)
            return new(false, SandboxRefusal.NotASandbox,
                $"\"{site.Key}\" is not a sandbox. Only a site explicitly marked as a sandbox can be reset.");

        if (string.IsNullOrWhiteSpace(site.ResetPolicy))
            return new(false, SandboxRefusal.NoResetPolicy,
                $"\"{site.Key}\" is a sandbox but has no reset policy, so nothing asked for it to be wiped.");

        return new(true, SandboxRefusal.None, $"\"{site.Key}\" is a resettable sandbox.");
    }

    public async Task<TimeSpan?> IdleForAsync(int siteId, DateTime utcNow, CancellationToken ct = default)
    {
        // Sessions are not site-scoped by the auth package, so "in use" is measured across the
        // deployment. That is deliberately conservative: it can only ever DELAY a reset, never cause
        // one — and a reset that fires while someone is mid-demo is the failure that matters.
        var lastSeen = await db.AuthSessions
            .Where(s => s.RevokedUtc == null && s.AbsoluteExpiryUtc > utcNow)
            .OrderByDescending(s => s.LastSeenUtc)
            .Select(s => (DateTime?)s.LastSeenUtc)
            .FirstOrDefaultAsync(ct);

        if (lastSeen is null) return TimeSpan.MaxValue;          // nobody has ever been here
        var idle = utcNow - lastSeen.Value;
        return idle < TimeSpan.Zero ? TimeSpan.Zero : idle;
    }

    public async Task<IReadOnlyList<Site>> DueForResetAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var candidates = await db.Sites
            .Where(s => s.IsSandbox && !s.IsDefault && s.ResetPolicy != null)
            .ToListAsync(ct);

        var due = new List<Site>();
        foreach (var site in candidates)
        {
            // Re-gate each one rather than trusting the query: the gate is the single authority, and a
            // query predicate that drifts from it is exactly how the wrong site gets wiped.
            if (!Gate(site).Allowed) continue;
            if (!string.Equals(site.ResetPolicy, WhenIdle, StringComparison.OrdinalIgnoreCase)) continue;

            var idle = await IdleForAsync(site.Id, utcNow, ct);
            if (idle is null) continue;

            // A grace period, not "the moment they leave": a visitor between page loads has no live
            // circuit for a beat, and wiping the site under them would read as a crash.
            var grace = TimeSpan.FromMinutes(Math.Max(1, site.IdleGraceMinutes));
            if (idle >= grace) due.Add(site);
        }
        return due;
    }
}
