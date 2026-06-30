using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;

namespace MindAttic.Ideas.Core.Services;

public sealed class AssetStorageOptions
{
    /// <summary>Root directory for disk-backed assets. Defaults to {BaseDirectory}/media.</summary>
    public string MediaRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "media");

    /// <summary>Assets at or below this threshold are stored inline in the DB; larger ones go to disk.</summary>
    public long InlineThresholdBytes { get; set; } = 2L * 1024 * 1024;
}

/// <summary>Lightweight projection for listing assets — excludes the potentially large Bytes column.</summary>
public sealed record AssetRow(int Id, Guid Uid, string? Folder, string FileName, string ContentType, long SizeBytes, DateTime CreatedUtc);

public interface IAssetService
{
    /// <summary>Store an uploaded file. Returns the saved entity.</summary>
    Task<Asset> UploadAsync(Stream content, string fileName, string contentType, int? siteId = null, string folder = "", CancellationToken ct = default);

    /// <summary>List assets (Bytes column excluded). Pass siteId=null for host-level assets.</summary>
    Task<IReadOnlyList<AssetRow>> ListAsync(int? siteId = null, string? folder = null, CancellationToken ct = default);

    /// <summary>Resolve an asset by its stable Uid. Returns null when not found. Caller owns the stream.</summary>
    Task<(Asset Meta, Stream Content)?> GetAsync(Guid uid, CancellationToken ct = default);

    /// <summary>Soft-deletes the asset and removes the disk file if present.</summary>
    Task<bool> DeleteAsync(Guid uid, CancellationToken ct = default);
}

public sealed class AssetService(IDbContextFactory<CmsDbContext> dbFactory, IOptions<AssetStorageOptions> opts) : IAssetService
{
    private readonly AssetStorageOptions _opts = opts.Value;

    public async Task<Asset> UploadAsync(Stream content, string fileName, string contentType,
        int? siteId = null, string folder = "", CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var asset = new Asset
        {
            Uid = Guid.NewGuid(),
            SiteId = siteId,
            Folder = folder ?? "",
            FileName = SanitizeFileName(Path.GetFileName(fileName)),
            ContentType = contentType,
            SizeBytes = bytes.LongLength,
            Sha256 = sha,
            CreatedUtc = DateTime.UtcNow,
            ModifiedUtc = DateTime.UtcNow,
        };

        if (bytes.LongLength <= _opts.InlineThresholdBytes)
        {
            asset.Bytes = bytes;
        }
        else
        {
            var dir = Path.Combine(_opts.MediaRoot, asset.Uid.ToString("N"));
            Directory.CreateDirectory(dir);
            var diskPath = Path.Combine(dir, asset.FileName);
            await File.WriteAllBytesAsync(diskPath, bytes, ct);
            asset.BlobUri = diskPath;
        }

        db.Assets.Add(asset);
        await db.SaveChangesAsync(ct);
        return asset;
    }

    public async Task<IReadOnlyList<AssetRow>> ListAsync(int? siteId = null, string? folder = null, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = db.Assets.AsNoTracking().Where(a => a.SiteId == siteId);
        if (folder is not null)
            q = q.Where(a => a.Folder == folder);
        return await q
            .OrderByDescending(a => a.CreatedUtc)
            .Select(a => new AssetRow(a.Id, a.Uid, a.Folder, a.FileName, a.ContentType, a.SizeBytes, a.CreatedUtc))
            .ToListAsync(ct);
    }

    public async Task<(Asset Meta, Stream Content)?> GetAsync(Guid uid, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Uid == uid, ct);
        if (asset is null) return null;

        if (asset.Bytes is { Length: > 0 })
            return (asset, new MemoryStream(asset.Bytes, writable: false));

        if (!string.IsNullOrEmpty(asset.BlobUri) && File.Exists(asset.BlobUri))
            return (asset, File.OpenRead(asset.BlobUri));

        return null;
    }

    public async Task<bool> DeleteAsync(Guid uid, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Uid == uid, ct);
        if (asset is null) return false;

        if (!string.IsNullOrEmpty(asset.BlobUri) && File.Exists(asset.BlobUri))
        {
            try
            {
                File.Delete(asset.BlobUri);
                var dir = Path.GetDirectoryName(asset.BlobUri);
                if (dir is not null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
            }
            catch { /* best-effort file cleanup */ }
        }

        asset.IsDeleted = true;
        asset.DeletedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(safe)) safe = "upload";
        // Prevent names that start with '.' to avoid dot-files (.htaccess, .env, etc.)
        if (safe[0] == '.') safe = "_" + safe;
        return safe;
    }
}
