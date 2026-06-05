using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Core.Discovery;

/// <summary>
/// The unified in-memory catalog of active, enabled citizens. Populated by <c>DiscoveryService</c> at
/// boot (and after any package install in Phase 5). Lookups are by the pinned (Kind, Key, Version).
/// </summary>
public sealed class ContentCatalog(ITypeResolver resolver) : IContentCatalog
{
    private volatile IReadOnlyList<ContentDescriptor> _all = Array.Empty<ContentDescriptor>();
    // Identities that EXIST but are admin-disabled — kept separately so ResolveTag can report Disabled
    // (vs Missing) without putting them in the enabled snapshot the renderer composes from.
    private volatile IReadOnlyList<(ContentKind Kind, string Key, int Version)> _disabled =
        Array.Empty<(ContentKind, string, int)>();

    /// <summary>Replace the enabled-winners snapshot atomically.</summary>
    public void Load(IEnumerable<ContentDescriptor> descriptors) => _all = descriptors.ToArray();

    /// <summary>Replace the disabled-identity snapshot atomically (drives Disabled vs Missing).</summary>
    public void LoadDisabled(IEnumerable<(ContentKind Kind, string Key, int Version)> disabled) => _disabled = disabled.ToArray();

    public IReadOnlyCollection<ContentDescriptor> All => _all;

    public ContentDescriptor? Find(ContentKind kind, string key, int version) =>
        _all.FirstOrDefault(d => d.Kind == kind && d.Key == key && d.Version == version);

    public ContentDescriptor? FindLatest(ContentKind kind, string key) =>
        _all.Where(d => d.Kind == kind && d.Key == key).MaxBy(d => d.Version);

    public Type? ResolveType(ContentDescriptor descriptor) => resolver.Resolve(descriptor);

    /// <summary>Override the default: report Disabled when an enabled winner is absent but the identity is known-disabled.</summary>
    public ResolvedContent ResolveTag(ContentKind kind, string key, int? version)
    {
        var desc = version is int v ? (Find(kind, key, v) ?? FindLatest(kind, key)) : FindLatest(kind, key);
        if (desc is not null)
        {
            var type = ResolveType(desc);
            return type is null
                ? new ResolvedContent(ContentResolution.Missing, null, desc)
                : new ResolvedContent(ContentResolution.Resolved, type, desc);
        }

        var known = version is int pinned
            ? _disabled.Any(d => d.Kind == kind && d.Key == key && d.Version == pinned)
            : _disabled.Any(d => d.Kind == kind && d.Key == key);
        return new ResolvedContent(known ? ContentResolution.Disabled : ContentResolution.Missing, null, null);
    }
}
