using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Portability;
using MindAttic.Media;

namespace MindAttic.Ideas.Core.Services;

/// <summary>
/// Where a sandbox's Day Zero comes from. A seam rather than a path, because the reset service must not
/// know what a content root is — and because a per-site baseline, a blob-hosted one, or a generated one
/// all slot in here without touching the destructive half.
/// </summary>
public interface ISandboxBaselineSource
{
    /// <summary>
    /// The baseline bundle for this site, or null when none is configured — in which case the reset
    /// EMPTIES the site rather than restoring it, and says so.
    /// </summary>
    Task<Stream?> OpenAsync(Entities.Site site, CancellationToken ct = default);
}

/// <summary>No baseline anywhere. A reset then clears the site and leaves it empty.</summary>
public sealed class NullSandboxBaselineSource : ISandboxBaselineSource
{
    public Task<Stream?> OpenAsync(Entities.Site site, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
}

/// <summary>What a reset attempt did, or why it refused. A refusal is never silent.</summary>
public sealed record SandboxResetOutcome(
    bool Ok,
    SandboxRefusal Refusal,
    string Explanation,
    int PagesRemoved = 0,
    int PackagesRemoved = 0,
    BundleImportResult? Restored = null);

public interface ISandboxResetService
{
    /// <summary>
    /// Return a sandbox site to its baseline: drop everything that site owns, then restore Day Zero from
    /// its baseline bundle. Refuses — writing nothing — for any site
    /// <see cref="ISandboxService.Gate"/> does not authorize. Never throws.
    /// </summary>
    Task<SandboxResetOutcome> ResetAsync(int siteId, DateTime utcNow, CancellationToken ct = default);
}

/// <summary>
/// The destructive half of Showroom mode (MAI-A36, MAI-A38).
/// <para>
/// Everything here is downstream of one question — <see cref="ISandboxService.Gate"/> — which is asked
/// again HERE, immediately before the first delete, rather than trusted from whatever decided to call
/// this. A caller that gated a minute ago, a sweep whose query predicate has drifted, or a future admin
/// button that forgets entirely all hit the same refusal. The main site can never be reset.
/// </para>
/// <para>
/// Two things deliberately survive a reset. <b>Media</b> is not site-scoped by the media package, so
/// deleting a visitor's uploads would mean deleting from a store the real site shares — the wrong
/// failure by a wide margin; a stale image is a much smaller price than a live site losing its pictures.
/// <b>Shared citizens</b> — the whole first-party library — are untouched, because they were never the
/// sandbox's to begin with; only what the site itself owns is dropped.
/// </para>
/// </summary>
public sealed class SandboxResetService(
    CmsDbContext db,
    ISandboxService sandbox,
    ISandboxBaselineSource baseline,
    IMediaStore media,
    DiscoveryService discovery) : ISandboxResetService
{
    public async Task<SandboxResetOutcome> ResetAsync(int siteId, DateTime utcNow, CancellationToken ct = default)
    {
        var site = await db.Sites.FirstOrDefaultAsync(s => s.Id == siteId, ct);

        // THE gate, asked here and not inherited. Nothing below this line runs for a site it refuses.
        var gate = sandbox.Gate(site);
        if (!gate.Allowed) return new SandboxResetOutcome(false, gate.Refusal, gate.Explanation);

        // ---- 1. drop what this site owns -----------------------------------------------------
        // Hard delete, not the soft-disable of HOUSE-LAW-2. Soft is for content someone might want back;
        // a sandbox reset exists precisely to make it unrecoverable, and rows kept forever would collide
        // with the baseline's slugs on every restore. This is the one routine allowed to do it, which is
        // why it sits behind three separate flags and the default-site refusal.
        var pages = await db.Pages.IgnoreQueryFilters().Where(p => p.SiteId == siteId).ToListAsync(ct);
        var pageUids = pages.Select(p => p.Uid).ToList();

        // ParentId is NoAction, so a child still pointing at a parent would block the delete.
        foreach (var p in pages) p.ParentId = null;
        if (pages.Count > 0) await db.SaveChangesAsync(ct);

        // ComponentMetadata keys on PageUid with no foreign key, so nothing cascades it away.
        var orphanMetadata = await db.ComponentMetadata.Where(m => pageUids.Contains(m.PageUid)).ToListAsync(ct);
        db.ComponentMetadata.RemoveRange(orphanMetadata);

        // Meta tags, slug history, role/user access and placement settings all cascade from the page.
        db.Pages.RemoveRange(pages);

        var settings = await db.Settings.Where(s => s.Scope == "Site" && s.ScopeId == siteId).ToListAsync(ct);
        db.Settings.RemoveRange(settings);

        var defs = await db.ContentDefinitions.Where(c => c.SiteId == siteId).ToListAsync(ct);
        var packages = await db.InstalledPackages.Where(p => p.SiteId == siteId).ToListAsync(ct);
        db.ContentDefinitions.RemoveRange(defs);
        db.InstalledPackages.RemoveRange(packages);

        await db.SaveChangesAsync(ct);

        // ---- 2. restore Day Zero -------------------------------------------------------------
        BundleImportResult? restored = null;
        await using (var stream = await baseline.OpenAsync(site!, ct))
        {
            if (stream is not null)
            {
                try
                {
                    using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                    var (bundle, error) = await ContentBundleImporter.ReadManifestAsync(zip);
                    if (bundle is null)
                    {
                        // The site is already cleared; say so plainly rather than pretending it restored.
                        await StampAsync(site!, utcNow, ct);
                        return new SandboxResetOutcome(false, SandboxRefusal.None,
                            $"\"{site!.Key}\" was cleared, but its baseline could not be read: {error}",
                            pages.Count, packages.Count);
                    }

                    var importer = new ContentBundleImporter(db, media);
                    restored = await importer.ImportAsync(zip, bundle,
                        // Pinned to THIS site, which also means the bundle's site row is not applied —
                        // a baseline exported from the real site would otherwise rename the sandbox,
                        // overwrite its host bindings and clear the very flags that make it a sandbox.
                        new BundleImportOptions { IntoSiteId = siteId },
                        static (_, _) => { }, ct);
                }
                catch (InvalidDataException ex)
                {
                    await StampAsync(site!, utcNow, ct);
                    return new SandboxResetOutcome(false, SandboxRefusal.None,
                        $"\"{site!.Key}\" was cleared, but its baseline is not a readable archive: {ex.Message}",
                        pages.Count, packages.Count);
                }
            }
        }

        await StampAsync(site!, utcNow, ct);

        // The site's own citizens are gone, so the live catalog has to stop offering them.
        await discovery.ReloadCatalogAsync(ct);

        var what = restored is null
            ? $"\"{site!.Key}\" was cleared. No baseline is configured, so it is now empty."
            : $"\"{site!.Key}\" was reset to its baseline: {restored.PagesCreated} page(s) restored.";
        return new SandboxResetOutcome(true, SandboxRefusal.None, what, pages.Count, packages.Count, restored);
    }

    private async Task StampAsync(Entities.Site site, DateTime utcNow, CancellationToken ct)
    {
        site.LastResetUtc = utcNow;
        await db.SaveChangesAsync(ct);
    }
}
