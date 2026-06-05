using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Core.Services;

/// <summary>A package could not be installed (malformed, invalid, blocked, or a downgrade).</summary>
public sealed class InstallException(string message) : Exception(message);

public interface IPackageInstallService
{
    /// <summary>
    /// Validate a <c>.idea</c> stream and, if the plan permits, register it: upsert the
    /// <see cref="InstalledPackage"/> registry row and a mirrored <see cref="CmsContentDefinition"/>
    /// (Origin=Package), then reload the live catalog. Does NOT load the package assembly — that is the
    /// deferred Phase-5/B ALC loader; until then the descriptor's CLR type stays unresolved and the
    /// citizen degrades to a render placeholder rather than crashing. Throws <see cref="InstallException"/>
    /// for an invalid/blocked/downgrade package.
    /// </summary>
    Task<InstallPlan> InstallAsync(Stream ideaBytes, bool allowOverride, CancellationToken ct = default);

    /// <summary>Soft-disable an installed version: flip Enabled=false on both rows and reload. Bytes/rows remain.</summary>
    Task DisableAsync(string category, string key, int version, CancellationToken ct = default);
}

/// <summary>
/// The host-side install tail. All format/version/collision logic is the pure
/// <see cref="MindAttic.Ideas.Packaging"/> library; this only does the IO (read bytes, hash, persist rows)
/// and the live-catalog reload. Soft by construction — nothing is ever hard-removed.
/// </summary>
public sealed class PackageInstallService(
    IDbContextFactory<CmsDbContext> dbFactory,
    DiscoveryService discovery,
    IPackageBlobStore blobStore,
    IPackageExtractor extractor) : IPackageInstallService
{
    public async Task<InstallPlan> InstallAsync(Stream ideaBytes, bool allowOverride, CancellationToken ct = default)
    {
        // Buffer once: we need the bytes for both the hash and the (seekable) archive read.
        using var buffer = new MemoryStream();
        await ideaBytes.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var sha = Sha256Hasher.OfBytes(bytes);

        using var archive = IdeaArchiveReader.Open(new MemoryStream(bytes));
        if (!archive.TryReadManifest(out var manifest, out var error) || manifest is null)
            throw new InstallException(error ?? "package manifest could not be read.");
        var rawJson = archive.ReadManifestJson()!;   // non-null: TryReadManifest succeeded
        var binEntries = archive.BinEntries();

        var validation = ManifestValidator.Validate(manifest, binEntries);
        if (!validation.IsValid)
            throw new InstallException("invalid package — " + validation.Summary);

        if (!Enum.TryParse<ContentKind>(manifest.Category, ignoreCase: true, out var kind))
            throw new InstallException($"category '{manifest.Category}' is not a known ContentKind; cannot register the catalog row.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var installedRows = await db.InstalledPackages
            .Where(p => p.Key == manifest.Key && p.Category == manifest.Category)
            .ToListAsync(ct);
        var installedRefs = installedRows
            .Select(p => new InstalledRef(p.Category, p.Key, p.Version, p.Enabled, p.IsActiveVersion))
            .ToList();

        var compiledKeyExists = await db.ContentDefinitions.AnyAsync(
            c => c.Origin == ContentOrigin.Compiled && c.Kind == kind && c.Key == manifest.Key && c.IsActive, ct);

        var plan = PackageVersionResolver.Plan(manifest, installedRefs, compiledKeyExists, allowOverride);
        switch (plan.Action)
        {
            case InstallAction.NoOpAlreadyInstalled:
                return plan;
            case InstallAction.Blocked:
            case InstallAction.RejectDowngrade:
                throw new InstallException(plan.Reason ?? "install was rejected.");
        }

        var now = DateTime.UtcNow;

        // Persist the verbatim bytes (the source of truth) and, for a code package, extract bin/ to disk so
        // the ALC-aware resolver can load it. Done only once the install is going to proceed.
        var blobPath = await blobStore.SaveAsync(manifest.Category, manifest.Key, manifest.Version, bytes, ct);
        if (string.Equals(manifest.Kind, "code", StringComparison.Ordinal))
            extractor.Extract(archive, manifest.Category, manifest.Key, manifest.Version);

        // ---- Registry row (idempotent upsert by the unique (Category,Key,Version)). ----
        var pkg = installedRows.FirstOrDefault(p => p.Version == manifest.Version)
                  ?? AddNew(db, new InstalledPackage());
        pkg.Category = manifest.Category;
        pkg.Kind = manifest.Kind;
        pkg.Key = manifest.Key;
        pkg.Version = manifest.Version;
        pkg.DisplayName = manifest.DisplayName;
        pkg.ManifestJson = rawJson;                       // verbatim — preserves any forward-compat fields
        pkg.ManifestVersion = manifest.ManifestVersion;
        pkg.Sha256 = sha;
        pkg.BlobPath = blobPath;                          // points at the persisted .idea bytes
        pkg.Enabled = true;
        pkg.IsActiveVersion = plan.MakeActiveVersion;
        pkg.InstalledUtc = now;

        // Retain prior active versions; just flip them inactive.
        foreach (var (_, _, ver) in plan.DeactivatePriorVersions)
        {
            var prior = installedRows.FirstOrDefault(p => p.Version == ver);
            if (prior is not null) prior.IsActiveVersion = false;
        }

        // ---- Mirrored catalog row (Origin=Package) so the citizen is registered without the ALC loader. ----
        var def = await db.ContentDefinitions.FirstOrDefaultAsync(
            c => c.Origin == ContentOrigin.Package && c.Kind == kind && c.Key == manifest.Key && c.Version == manifest.Version, ct)
            ?? AddNew(db, new CmsContentDefinition());
        def.Kind = kind;
        def.Key = manifest.Key;
        def.Version = manifest.Version;
        def.Origin = ContentOrigin.Package;
        def.DisplayName = manifest.DisplayName;
        def.Category = manifest.Category;
        def.Strategy = RenderStrategy.ClrType;
        def.RenderMode = ParseRenderMode(manifest.RenderMode);
        def.Scope = ParseScope(manifest.Scope);
        def.ClrTypeName = manifest.EntryType;             // resolves only once the ALC loader (B) lands
        def.AssemblyName = manifest.AssemblyName;
        def.AssetMount = $"/_ideas/{manifest.Key}/{manifest.Version}";   // the asset-source seam absorbs this in C
        def.Priority = 50;                                // Package < Compiled(100)
        def.IsActive = true;
        def.Enabled = true;
        def.AllowOverride = allowOverride;
        def.DiscoveredUtc = now;

        await db.SaveChangesAsync(ct);
        await discovery.ReloadCatalogAsync(ct);
        return plan;
    }

    public async Task DisableAsync(string category, string key, int version, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ContentKind>(category, ignoreCase: true, out var kind))
            throw new InstallException($"category '{category}' is not a known ContentKind.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var pkg = await db.InstalledPackages.FirstOrDefaultAsync(
            p => p.Category == category && p.Key == key && p.Version == version, ct);
        if (pkg is not null) pkg.Enabled = false;

        var def = await db.ContentDefinitions.FirstOrDefaultAsync(
            c => c.Origin == ContentOrigin.Package && c.Kind == kind && c.Key == key && c.Version == version, ct);
        if (def is not null) def.Enabled = false;

        await db.SaveChangesAsync(ct);
        await discovery.ReloadCatalogAsync(ct);
    }

    private static T AddNew<T>(CmsDbContext db, T entity) where T : class
    {
        db.Add(entity);
        return entity;
    }

    private static CmsRenderMode ParseRenderMode(string s) =>
        string.Equals(s, "Static", StringComparison.OrdinalIgnoreCase) ? CmsRenderMode.Static : CmsRenderMode.InteractiveServer;

    private static PlacementScope ParseScope(string s) =>
        string.Equals(s, "Global", StringComparison.OrdinalIgnoreCase) ? PlacementScope.Global : PlacementScope.Placeable;
}
