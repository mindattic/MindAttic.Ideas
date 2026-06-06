using System.IO.Compression;
using System.Text;
using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Tests.Packaging;

/// <summary>Synthesizes a <c>.idea</c> archive in memory so the read path can be tested without disk.</summary>
internal static class IdeaTestArchive
{
    /// <summary>Build a zip with the given entry paths→content. Returns a seekable stream positioned at 0.</summary>
    public static MemoryStream Build(IReadOnlyDictionary<string, string> entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                var e = zip.CreateEntry(path);
                using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
                w.Write(content);
            }
        }
        ms.Position = 0;
        return ms;
    }

    /// <summary>A minimal valid code-package manifest JSON with one bin/ assembly.</summary>
    public static MemoryStream CodePackage(string key = "ui.tooltip", int version = 1, string category = "Plugin") =>
        Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = category, Kind = "code", Key = key, Version = version,
                DisplayName = "Tooltip", Sdk = 1, EntryType = $"MindAttic.Ideas.{category}.Demo.V{version}",
                AssemblyName = "Demo", Assets = ["css/x.css"],
            }),
            ["bin/Demo.dll"] = "MZ-fake",
            ["wwwroot/css/x.css"] = ".x{}",
        });
}
