using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;

namespace MindAttic.Ideas.Core.Services;

/// <summary>A read-only summary of an installed .idea package, for the admin packages browser.</summary>
public sealed record PackageSummary(
    int Id,
    string Category,
    string Key,
    int Version,
    string DisplayName,
    bool Enabled,
    bool IsActiveVersion,
    DateTime InstalledUtc,
    string BlobPath,
    string Sha256,
    /// <summary>Owning site, or null when shared by every site (MAI-A36).</summary>
    int? SiteId = null);

public interface IPackageRegistryService
{
    /// <summary>
    /// Installed packages VISIBLE to <paramref name="siteId"/>: the shared ones plus that site's own,
    /// which is what the site actually renders from. Null lists the shared registry only — a showroom
    /// visitor must not be shown the real site's inventory, and the real site's operator is not looking
    /// at a stranger's uploads either.
    /// </summary>
    Task<IReadOnlyList<PackageSummary>> ListAsync(int? siteId = null, CancellationToken ct = default);
}

public sealed class PackageRegistryService(IDbContextFactory<CmsDbContext> factory) : IPackageRegistryService
{
    public async Task<IReadOnlyList<PackageSummary>> ListAsync(int? siteId = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.InstalledPackages.AsNoTracking()
            .Where(p => p.SiteId == null || p.SiteId == siteId)
            .OrderBy(p => p.Category).ThenBy(p => p.Key).ThenByDescending(p => p.Version)
            .Select(p => new PackageSummary(
                p.Id, p.Category, p.Key, p.Version, p.DisplayName,
                p.Enabled, p.IsActiveVersion, p.InstalledUtc,
                p.BlobPath, p.Sha256, p.SiteId))
            .ToListAsync(ct);
    }
}
