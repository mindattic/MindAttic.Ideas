using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Rendering;

namespace MindAttic.Ideas.Core.Services;

public readonly record struct LifecycleResult(bool Ok, string? Error)
{
    public static readonly LifecycleResult Success = new(true, null);
    public static LifecycleResult Fail(string error) => new(false, error);
}

/// <summary>Outcome of a delete attempt. Blocked => reference-guarded; DisabledInstead => compiled-origin soft path.</summary>
public sealed record DeleteGuardResult(bool Deleted, bool Blocked, bool DisabledInstead, IReadOnlyList<string> PinnedBy, string? Message);

/// <summary>A page reference used by the (pure, DB-free) delete-guard scan.</summary>
public readonly record struct PageRef(string Slug, string? BodyHtml, string? ThemeKey, int? ThemeVersion, bool Enabled, bool IsPublished);

/// <summary>
/// Admin lifecycle for content definitions: list, enable/disable (with live catalog reload), and
/// VERSION-SPECIFIC, reference-guarded delete. The core invariant: a page must never be left invalid —
/// you cannot delete a version that an enabled+published page pins, and you cannot delete the last
/// enabled version that a floating ("latest") reference would orphan. Compiled-origin definitions are
/// disabled rather than hard-deleted (discovery would re-add them); true removal is reserved for Package
/// origin (Phase 5).
/// </summary>
public interface IContentLifecycleService
{
    Task<IReadOnlyList<CmsContentDefinition>> ListAsync(ContentKind? kind = null, CancellationToken ct = default);
    Task<LifecycleResult> SetEnabledAsync(int id, bool enabled, CancellationToken ct = default);
    Task<DeleteGuardResult> CanDeleteAsync(ContentKind kind, string key, int version, CancellationToken ct = default);
    Task<DeleteGuardResult> DeleteAsync(ContentKind kind, string key, int version, CancellationToken ct = default);
}

public sealed class ContentLifecycleService(IDbContextFactory<CmsDbContext> dbFactory, DiscoveryService discovery)
    : IContentLifecycleService
{
    public async Task<IReadOnlyList<CmsContentDefinition>> ListAsync(ContentKind? kind = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.ContentDefinitions.AsNoTracking();
        if (kind is ContentKind k) q = q.Where(d => d.Kind == k);
        return await q.OrderBy(d => d.Kind).ThenBy(d => d.Key).ThenByDescending(d => d.Version).ToListAsync(ct);
    }

    public async Task<LifecycleResult> SetEnabledAsync(int id, bool enabled, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var def = await db.ContentDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (def is null) return LifecycleResult.Fail("Content definition not found.");
        if (def.Enabled != enabled)
        {
            def.Enabled = enabled;
            await db.SaveChangesAsync(ct);
            await discovery.ReloadCatalogAsync(ct);   // live-apply: the catalog reflects it immediately
        }
        return LifecycleResult.Success;
    }

    public async Task<DeleteGuardResult> CanDeleteAsync(ContentKind kind, string key, int version, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var def = await db.ContentDefinitions
            .FirstOrDefaultAsync(d => d.Kind == kind && d.Key == key && d.Version == version, ct);
        if (def is null)
            return new DeleteGuardResult(false, false, false, Array.Empty<string>(), "Content definition not found.");

        var pages = await db.Pages
            .Select(p => new PageRef(p.Slug, p.BodyHtml, p.ThemeKey, p.ThemeVersion, p.Enabled, p.IsPublished))
            .ToListAsync(ct);

        // Versions of this key that would REMAIN enabled+live after deleting `version`.
        var otherEnabled = await db.ContentDefinitions
            .Where(d => d.Kind == kind && d.Key == key && d.Version != version && d.Enabled && d.IsActive && !d.IsShadowed)
            .Select(d => d.Version)
            .ToListAsync(ct);

        var blocking = FindBlockingPages(kind, key, version, pages, otherEnabled);
        return blocking.Count > 0
            ? new DeleteGuardResult(false, true, false, blocking, $"Blocked: {blocking.Count} page(s) still use this version.")
            : new DeleteGuardResult(false, false, false, Array.Empty<string>(), null);
    }

    public async Task<DeleteGuardResult> DeleteAsync(ContentKind kind, string key, int version, CancellationToken ct = default)
    {
        var guard = await CanDeleteAsync(kind, key, version, ct);
        if (guard.Message == "Content definition not found." || guard.Blocked) return guard;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var def = await db.ContentDefinitions
            .FirstOrDefaultAsync(d => d.Kind == kind && d.Key == key && d.Version == version, ct);
        if (def is null)
            return new DeleteGuardResult(false, false, false, Array.Empty<string>(), "Content definition not found.");

        if (def.Origin == ContentOrigin.Compiled)
        {
            // Hard delete would be undone by the next discovery pass — disable instead.
            def.Enabled = false;
            await db.SaveChangesAsync(ct);
            await discovery.ReloadCatalogAsync(ct);
            return new DeleteGuardResult(false, false, true, Array.Empty<string>(),
                "Compiled content can't be removed (discovery would re-add it); it was disabled instead.");
        }

        db.ContentDefinitions.Remove(def);   // Package origin: safe to truly remove (Phase 5)
        await db.SaveChangesAsync(ct);
        await discovery.ReloadCatalogAsync(ct);
        return new DeleteGuardResult(true, false, false, Array.Empty<string>(), null);
    }

    /// <summary>
    /// PURE delete-guard: the enabled+published pages that would be left invalid by deleting
    /// (kind,key,version). A pin on this exact version always blocks. A floating ("latest") reference
    /// blocks only when NO other enabled version would remain (otherwise it harmlessly floats to that one).
    /// </summary>
    public static IReadOnlyList<string> FindBlockingPages(
        ContentKind kind, string key, int version,
        IReadOnlyList<PageRef> pages, IReadOnlyList<int> otherEnabledVersions)
    {
        var floatingOrphans = otherEnabledVersions.Count == 0; // deleting `version` leaves nothing enabled
        var blocking = new List<string>();

        foreach (var p in pages)
        {
            if (!p.Enabled || !p.IsPublished) continue; // disabled/unpublished pages aren't live references

            bool blocks;
            if (kind == ContentKind.Theme)
            {
                var keyMatch = string.Equals(p.ThemeKey, key, StringComparison.OrdinalIgnoreCase);
                var pinned = keyMatch && p.ThemeVersion == version;
                var floating = keyMatch && p.ThemeVersion is null && floatingOrphans;
                blocks = pinned || floating;
            }
            else // Component / Control: scan the author body via the shared grammar
            {
                var pinned = IncludeReferenceParser.BodyPinsVersion(p.BodyHtml, kind, key, version);
                var floating = floatingOrphans && IncludeReferenceParser.BodyFloatsKey(p.BodyHtml, kind, key);
                blocks = pinned || floating;
            }

            if (blocks) blocking.Add(p.Slug);
        }

        return blocking;
    }
}
