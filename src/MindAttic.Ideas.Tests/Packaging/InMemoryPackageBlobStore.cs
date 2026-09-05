using System.Collections.Concurrent;
using MindAttic.Ideas.Core.Services;

namespace MindAttic.Ideas.Tests.Packaging;

internal sealed class InMemoryPackageBlobStore : IPackageBlobStore
{
    public readonly ConcurrentDictionary<string, byte[]> Saved = new();

    public Task<string> SaveAsync(string category, string key, int version, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
        SaveAsync(category, key, version, bytes, siteId: null, ct);

    // Keyed exactly like the real store, site prefix included — otherwise two sites' copies of the same
    // (category, key, version) would land on one key here and the double would hide the collision the
    // production layout exists to prevent.
    public Task<string> SaveAsync(string category, string key, int version, ReadOnlyMemory<byte> bytes, int? siteId, CancellationToken ct = default)
    {
        var path = LocalFilePackageBlobStore.BlobPathFor(category, key, version, siteId);
        Saved[path] = bytes.ToArray();
        return Task.FromResult(path);
    }

    public Task<Stream?> OpenAsync(string blobPath, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(Saved.TryGetValue(blobPath, out var b) ? new MemoryStream(b) : null);

    public Task<bool> ExistsAsync(string blobPath, CancellationToken ct = default) =>
        Task.FromResult(Saved.ContainsKey(blobPath));
}
