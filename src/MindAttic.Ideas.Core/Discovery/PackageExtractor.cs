using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Core.Discovery;

/// <summary>
/// Extracts an installed code package's <c>bin/</c> to disk so the ALC loader can load it, and owns the
/// extracted-path convention shared by install (which extracts) and <see cref="AlcAwareTypeResolver"/>
/// (which loads from the same place). Extracted files persist across restarts; nothing here deletes them.
/// </summary>
public interface IPackageExtractor
{
    /// <summary>Extract the package's bin/ and wwwroot/ to its conventional dir; returns that dir.</summary>
    string Extract(IdeaArchiveReader archive, string category, string key, int version, int? siteId = null);

    /// <summary>The expected entry-assembly path for a package (whether or not it is extracted yet).</summary>
    string EntryDllPath(string category, string key, int version, string assemblyName, int? siteId = null);

    /// <summary>True once the package's entry assembly is present on disk.</summary>
    bool IsExtracted(string category, string key, int version, string assemblyName, int? siteId = null);

    /// <summary>
    /// Resolve a request-relative asset path to the physical file under the package's extracted
    /// <c>wwwroot/</c>, or null if it is absent or would escape that root (serves the <c>/_ideas</c> route).
    /// </summary>
    string? ResolveAsset(string category, string key, int version, string relativePath, int? siteId = null);
}

/// <summary>
/// Local-filesystem extractor rooted at <c>%APPDATA%\MindAttic\Ideas\extracted</c> by default.
/// <para>
/// A site-owned package extracts under <c>sites/{siteId}/</c> instead of the shared root
/// (<a href="../../../docs/AMENDMENTS.md">MAI-A36</a>). Two sites may legitimately hold the same
/// <c>(category, key, version)</c> of DIFFERENT bytes, so the identity alone is not a unique path —
/// and because <see cref="AlcAwareTypeResolver"/> keys its load contexts by the entry-assembly path,
/// site-keying the directory is also what keys the ALC by site: one site's assembly can never be
/// handed back for another site's descriptor of the same identity. The literal <c>sites</c> segment
/// cannot collide with a category, which is always a <see cref="ContentKind"/> name.
/// </para>
/// </summary>
public sealed class PackageExtractor : IPackageExtractor
{
    private readonly string _root;

    public PackageExtractor(string? root = null)
    {
        _root = Path.GetFullPath(root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MindAttic", "Ideas", "extracted"));
    }

    /// <summary>The root a site's packages extract under; the shared root when <paramref name="siteId"/> is null.</summary>
    private string RootFor(int? siteId) =>
        siteId is int id ? Path.Combine(_root, "sites", id.ToString()) : _root;

    public string DirFor(string category, string key, int version, int? siteId = null) =>
        Path.Combine(RootFor(siteId), category, key, version.ToString());

    public string WwwrootDirFor(string category, string key, int version, int? siteId = null) =>
        Path.Combine(DirFor(category, key, version, siteId), "wwwroot");

    public string EntryDllPath(string category, string key, int version, string assemblyName, int? siteId = null) =>
        Path.Combine(DirFor(category, key, version, siteId), assemblyName + ".dll");

    public bool IsExtracted(string category, string key, int version, string assemblyName, int? siteId = null) =>
        File.Exists(EntryDllPath(category, key, version, assemblyName, siteId));

    public string Extract(IdeaArchiveReader archive, string category, string key, int version, int? siteId = null)
    {
        var dir = DirFor(category, key, version, siteId);
        Directory.CreateDirectory(dir);
        archive.ExtractTo(dir, "bin/");                                           // -> dir/<assemblies>
        archive.ExtractTo(WwwrootDirFor(category, key, version, siteId), "wwwroot/");   // -> dir/wwwroot/<assets>
        return dir;
    }

    public string? ResolveAsset(string category, string key, int version, string relativePath, int? siteId = null)
    {
        var wwwroot = Path.GetFullPath(WwwrootDirFor(category, key, version, siteId));
        var rootWithSep = wwwroot.EndsWith(Path.DirectorySeparatorChar) ? wwwroot : wwwroot + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(wwwroot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)) return null;   // escapes wwwroot
        return File.Exists(target) ? target : null;
    }
}

/// <summary>No-op extractor (for hosts/tests that don't run the ALC loader). Never writes disk.</summary>
public sealed class NullPackageExtractor : IPackageExtractor
{
    public string Extract(IdeaArchiveReader archive, string category, string key, int version, int? siteId = null) => "";
    public string EntryDllPath(string category, string key, int version, string assemblyName, int? siteId = null) => "";
    public bool IsExtracted(string category, string key, int version, string assemblyName, int? siteId = null) => false;
    public string? ResolveAsset(string category, string key, int version, string relativePath, int? siteId = null) => null;
}
