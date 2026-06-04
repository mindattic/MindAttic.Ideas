using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Core.Discovery;

/// <summary>
/// The unified in-memory catalog of active, enabled citizens. Populated by <c>DiscoveryService</c> at
/// boot (and after any package install in Phase 5). Lookups are by the pinned (Kind, Key, Version).
/// </summary>
public sealed class ContentCatalog(ITypeResolver resolver) : IContentCatalog
{
    private volatile IReadOnlyList<ContentDescriptor> _all = Array.Empty<ContentDescriptor>();

    /// <summary>Replace the snapshot atomically.</summary>
    public void Load(IEnumerable<ContentDescriptor> descriptors) => _all = descriptors.ToArray();

    public IReadOnlyCollection<ContentDescriptor> All => _all;

    public ContentDescriptor? Find(ContentKind kind, string key, int version) =>
        _all.FirstOrDefault(d => d.Kind == kind && d.Key == key && d.Version == version);

    public ContentDescriptor? FindLatest(ContentKind kind, string key) =>
        _all.Where(d => d.Kind == kind && d.Key == key).MaxBy(d => d.Version);

    public Type? ResolveType(ContentDescriptor descriptor) => resolver.Resolve(descriptor);
}
