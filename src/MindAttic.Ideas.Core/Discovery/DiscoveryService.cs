using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;

namespace MindAttic.Ideas.Core.Discovery;

/// <summary>
/// Runs every <see cref="ICmsContentSource"/> at boot, upserts <see cref="CmsContentDefinition"/> rows
/// by (Kind,Key,Version,Origin), flips disappeared rows to inactive (degrade to placeholder, never
/// delete), resolves collisions by Priority (compiled wins; loser kept visible as shadowed), then
/// loads the active+enabled winners into the in-memory <see cref="ContentCatalog"/>.
/// </summary>
public sealed class DiscoveryService(
    IDbContextFactory<CmsDbContext> dbFactory,
    IEnumerable<ICmsContentSource> sources,
    ContentCatalog catalog)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var discovered = sources
            .SelectMany(s => s.Discover().Select(d => d with { Origin = s.Origin, Priority = s.Priority }))
            .ToList();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.ContentDefinitions.ToListAsync(ct);
        var byIdentity = existing.ToDictionary(e => (e.Kind, e.Key, e.Version, e.Origin));
        var now = DateTime.UtcNow;
        var seen = new HashSet<(ContentKind, string, int, ContentOrigin)>();

        foreach (var d in discovered)
        {
            var id = (d.Kind, d.Key, d.Version, d.Origin);
            seen.Add(id);
            if (byIdentity.TryGetValue(id, out var row))
            {
                row.DisplayName = d.DisplayName; row.Category = d.Category; row.Strategy = d.Strategy;
                row.RenderMode = d.RenderMode; row.Scope = d.Scope; row.ClrTypeName = d.ClrTypeName;
                row.AssemblyName = d.AssemblyName; row.AssetMount = d.AssetMount; row.Priority = d.Priority;
                row.RawBundleJson = null; row.IsActive = true; row.DiscoveredUtc = now;
            }
            else
            {
                db.ContentDefinitions.Add(new CmsContentDefinition
                {
                    Kind = d.Kind, Key = d.Key, Version = d.Version, Origin = d.Origin,
                    DisplayName = d.DisplayName, Category = d.Category, Strategy = d.Strategy,
                    RenderMode = d.RenderMode, Scope = d.Scope, ClrTypeName = d.ClrTypeName,
                    AssemblyName = d.AssemblyName, AssetMount = d.AssetMount, Priority = d.Priority,
                    IsActive = true, Enabled = true, DiscoveredUtc = now,
                });
            }
        }

        // Disappeared rows -> inactive (kept for history + stable placeholders), never deleted.
        foreach (var row in existing.Where(e => !seen.Contains((e.Kind, e.Key, e.Version, e.Origin))))
            row.IsActive = false;

        await db.SaveChangesAsync(ct);

        // Collision resolution: within a (Kind,Key,Version) the highest Priority active row wins; the
        // rest are shadowed (a Package may only win over Compiled when AllowOverride was confirmed).
        var all = await db.ContentDefinitions.Where(x => x.IsActive).ToListAsync(ct);
        foreach (var grp in all.GroupBy(x => (x.Kind, x.Key, x.Version)))
        {
            var ordered = grp.OrderByDescending(x => x.Priority).ToList();
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].IsShadowed = i != 0;
        }
        await db.SaveChangesAsync(ct);

        var winners = all.Where(x => !x.IsShadowed && x.Enabled).Select(ToDescriptor);
        catalog.Load(winners);
    }

    private static ContentDescriptor ToDescriptor(CmsContentDefinition x) => new()
    {
        Kind = x.Kind, Key = x.Key, Version = x.Version, DisplayName = x.DisplayName,
        Category = x.Category, Origin = x.Origin, Priority = x.Priority, Strategy = x.Strategy,
        RenderMode = x.RenderMode, Scope = x.Scope, ClrTypeName = x.ClrTypeName,
        AssemblyName = x.AssemblyName, AssetMount = x.AssetMount, AllowOverride = x.AllowOverride,
    };
}
