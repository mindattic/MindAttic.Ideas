using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Core.Discovery;

/// <summary>
/// Extracts an installed code package's <c>bin/</c> to disk so the ALC loader can load it, and owns the
/// extracted-path convention shared by install (which extracts) and <see cref="AlcAwareTypeResolver"/>
/// (which loads from the same place). Extracted files persist across restarts; nothing here deletes them.
/// </summary>
public interface IPackageExtractor
{
    /// <summary>Extract the package's bin/ to its conventional dir; returns that dir.</summary>
    string Extract(IdeaArchiveReader archive, string category, string key, int version);

    /// <summary>The expected entry-assembly path for a package (whether or not it is extracted yet).</summary>
    string EntryDllPath(string category, string key, int version, string assemblyName);

    /// <summary>True once the package's entry assembly is present on disk.</summary>
    bool IsExtracted(string category, string key, int version, string assemblyName);
}

/// <summary>Local-filesystem extractor rooted at <c>%APPDATA%\MindAttic\Ideas\extracted</c> by default.</summary>
public sealed class PackageExtractor : IPackageExtractor
{
    private readonly string _root;

    public PackageExtractor(string? root = null)
    {
        _root = Path.GetFullPath(root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MindAttic", "Ideas", "extracted"));
    }

    public string DirFor(string category, string key, int version) =>
        Path.Combine(_root, category, key, version.ToString());

    public string EntryDllPath(string category, string key, int version, string assemblyName) =>
        Path.Combine(DirFor(category, key, version), assemblyName + ".dll");

    public bool IsExtracted(string category, string key, int version, string assemblyName) =>
        File.Exists(EntryDllPath(category, key, version, assemblyName));

    public string Extract(IdeaArchiveReader archive, string category, string key, int version)
    {
        var dir = DirFor(category, key, version);
        Directory.CreateDirectory(dir);
        archive.ExtractTo(dir, "bin/");
        return dir;
    }
}

/// <summary>No-op extractor (for hosts/tests that don't run the ALC loader). Never writes disk.</summary>
public sealed class NullPackageExtractor : IPackageExtractor
{
    public string Extract(IdeaArchiveReader archive, string category, string key, int version) => "";
    public string EntryDllPath(string category, string key, int version, string assemblyName) => "";
    public bool IsExtracted(string category, string key, int version, string assemblyName) => false;
}
