using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Core.Discovery;

/// <summary>
/// The unified in-memory catalog of active, enabled citizens. Populated by <c>DiscoveryService</c> at
/// boot (and after any package install in Phase 5). Lookups are by the pinned (Kind, Key, Version).
/// </summary>
public sealed class ContentCatalog(ITypeResolver resolver) : IContentCatalog
{
    // Single volatile reference so Load+LoadDisabled appear atomic to concurrent readers.
    private sealed record CatalogSnapshot(
        IReadOnlyList<ContentDescriptor> All,
        IReadOnlyList<(ContentKind Kind, string Key, int Version)> Disabled)
    {
        internal static readonly CatalogSnapshot Empty = new([], []);
    }

    private volatile CatalogSnapshot _snapshot = CatalogSnapshot.Empty;

    /// <summary>Replace the enabled-winners snapshot. Prefer <see cref="LoadSnapshot"/> to avoid a torn-state window.</summary>
    internal void Load(IEnumerable<ContentDescriptor> descriptors) =>
        _snapshot = _snapshot with { All = descriptors.ToArray() };

    /// <summary>Replace the disabled-identity snapshot. Prefer <see cref="LoadSnapshot"/> to avoid a torn-state window.</summary>
    internal void LoadDisabled(IEnumerable<(ContentKind Kind, string Key, int Version)> disabled) =>
        _snapshot = _snapshot with { Disabled = disabled.ToArray() };

    /// <summary>Replace both snapshots in a single atomic volatile write — eliminates the Disabled→Missing torn-state window.</summary>
    public void LoadSnapshot(
        IEnumerable<ContentDescriptor> enabled,
        IEnumerable<(ContentKind Kind, string Key, int Version)> disabled) =>
        _snapshot = new(enabled.ToArray(), disabled.ToArray());

    public IReadOnlyCollection<ContentDescriptor> All => _snapshot.All;

    // The site-less lookups mean SHARED-ONLY. That is what they meant before sites could own citizens
    // (every row was shared), and it is the safe reading now: if they matched any row, a package a
    // sandbox visitor installed could surface on the real site through any caller that has no site in
    // hand — a cross-tenant leak through the back door.
    public ContentDescriptor? Find(ContentKind kind, string key, int version) =>
        Find(kind, key, version, siteId: null);

    public ContentDescriptor? FindLatest(ContentKind kind, string key) =>
        FindLatest(kind, key, siteId: null);

    public Type? ResolveType(ContentDescriptor descriptor) => resolver.Resolve(descriptor);

    // ---- site-scoped lookups (MAI-A36) -----------------------------------------------------------
    //
    // A sandbox site lets visitors install their own packages, so a lookup has to be able to say WHO
    // is asking. The rule is site-first, then shared: a site's own citizen wins over the shared one of
    // the same (Kind, Key, Version), and a site that installed nothing sees exactly the shared catalog
    // it saw before this existed. Passing siteId: null asks only for shared citizens.

    /// <summary>Shared rows plus this site's own; a site's own row of the same identity wins.</summary>
    private static bool Visible(ContentDescriptor d, int? siteId) => d.SiteId is null || d.SiteId == siteId;

    /// <summary>Orders candidates so a site's OWN citizen is preferred over the shared one.</summary>
    private static int Ownership(ContentDescriptor d) => d.SiteId is null ? 0 : 1;

    public ContentDescriptor? Find(ContentKind kind, string key, int version, int? siteId) =>
        _snapshot.All
            .Where(d => d.Kind == kind && d.Key == key && d.Version == version && Visible(d, siteId))
            .OrderByDescending(Ownership)
            .FirstOrDefault();

    public ContentDescriptor? FindLatest(ContentKind kind, string key, int? siteId) =>
        _snapshot.All
            .Where(d => d.Kind == kind && d.Key == key && Visible(d, siteId))
            // Highest version wins; within one version, the site's own copy wins over the shared one.
            .OrderByDescending(d => d.Version).ThenByDescending(Ownership)
            .FirstOrDefault();

    public ResolvedContent ResolveTag(ContentKind kind, string key, int? version, int? siteId)
    {
        var snap = _snapshot;   // single read — consistent enabled+disabled pair
        var desc = version is int v ? Find(kind, key, v, siteId) : FindLatest(kind, key, siteId);
        if (desc is not null)
        {
            var type = ResolveType(desc);
            return type is null
                ? new ResolvedContent(ContentResolution.Missing, null, desc)
                : new ResolvedContent(ContentResolution.Resolved, type, desc);
        }

        var known = version is int pinned
            ? snap.Disabled.Any(d => d.Kind == kind && d.Key == key && d.Version == pinned)
            : snap.Disabled.Any(d => d.Kind == kind && d.Key == key);
        return new ResolvedContent(known ? ContentResolution.Disabled : ContentResolution.Missing, null, null);
    }

    /// <summary>
    /// Version-aware resolution: a pinned version is resolved exactly or reported Disabled/Missing — never
    /// silently promoted to the latest enabled version. A floating reference (version == null) resolves to
    /// the latest enabled version as before.
    /// </summary>
    public ResolvedContent ResolveTag(ContentKind kind, string key, int? version) =>
        ResolveTag(kind, key, version, siteId: null);
}
