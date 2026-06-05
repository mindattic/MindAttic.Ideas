using System.Text;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Tests.Packaging;

[TestFixture]
public class PackageExtractorTests
{
    private string _root = "";

    [SetUp]
    public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "ma-extract-" + Guid.NewGuid().ToString("N"));

    [TearDown]
    public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    [Test]
    public void Extract_WritesBinEntries_AndIsExtractedSeesTheEntryAssembly()
    {
        var extractor = new PackageExtractor(_root);
        using var idea = IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Component", Kind = "code", Key = "ui.tooltip", Version = 2,
                DisplayName = "Tooltip", Sdk = 1, EntryType = "MindAttic.Ideas.Component.Tooltip.V2", AssemblyName = "Tooltip",
            }),
            ["bin/Tooltip.dll"] = "fake-assembly-bytes",
            ["bin/Dep.dll"] = "dep",
            ["wwwroot/css/tip.css"] = ".tip{}",
        });
        using var reader = IdeaArchiveReader.Open(idea);

        var dir = extractor.Extract(reader, "Component", "ui.tooltip", 2);

        Assert.Multiple(() =>
        {
            Assert.That(extractor.IsExtracted("Component", "ui.tooltip", 2, "Tooltip"), Is.True);
            Assert.That(File.Exists(Path.Combine(dir, "Tooltip.dll")), Is.True);
            Assert.That(File.Exists(Path.Combine(dir, "Dep.dll")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(dir, "Tooltip.dll")), Is.EqualTo("fake-assembly-bytes"));
            Assert.That(File.Exists(Path.Combine(dir, "wwwroot", "css", "tip.css")), Is.True, "wwwroot is extracted too");
        });
    }

    [Test]
    public void ResolveAsset_FindsExtractedFile_AndGuardsTraversal()
    {
        var extractor = new PackageExtractor(_root);
        using var idea = IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Component", Kind = "code", Key = "ui.tooltip", Version = 1,
                DisplayName = "Tooltip", Sdk = 1, EntryType = "X", AssemblyName = "Tooltip",
            }),
            ["bin/Tooltip.dll"] = "asm",
            ["wwwroot/css/tip.css"] = ".tip{}",
        });
        using var reader = IdeaArchiveReader.Open(idea);
        extractor.Extract(reader, "Component", "ui.tooltip", 1);

        Assert.Multiple(() =>
        {
            Assert.That(extractor.ResolveAsset("Component", "ui.tooltip", 1, "css/tip.css"), Is.Not.Null);
            Assert.That(extractor.ResolveAsset("Component", "ui.tooltip", 1, "css/missing.css"), Is.Null);
            Assert.That(extractor.ResolveAsset("Component", "ui.tooltip", 1, "../../../secrets.txt"), Is.Null,
                "a traversal path must not escape the package wwwroot");
        });
    }

    [Test]
    public void EntryDllPath_FollowsTheConvention()
    {
        var extractor = new PackageExtractor(_root);
        var path = extractor.EntryDllPath("Component", "ui.tooltip", 3, "Tooltip");
        Assert.That(path, Is.EqualTo(Path.Combine(_root, "Component", "ui.tooltip", "3", "Tooltip.dll")));
    }

    [Test]
    public void IsExtracted_FalseBeforeExtraction()
    {
        var extractor = new PackageExtractor(_root);
        Assert.That(extractor.IsExtracted("Component", "ui.tooltip", 1, "Tooltip"), Is.False);
    }
}
