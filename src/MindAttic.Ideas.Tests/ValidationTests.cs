using System.IO.Compression;
using System.Text.Json;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Portability;
using MindAttic.Ideas.Core.Rendering;
using MindAttic.Ideas.Core.Services;
using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Tests;

/// <summary>MAI-A46: page bodies are validated at save, citizens at pack, and both again in this suite.</summary>
[TestFixture]
public class PageMarkupValidatorTests
{
    private static readonly CitizenSchemaLookup Lookup = (kind, key, _) =>
        kind == ContentKind.Component && key == "card"
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Radius"] = "string", ["Columns"] = "integer", ["Animate"] = "bool" }
            : null;

    private static IReadOnlyList<PageIssue> Validate(string body, ContentTrust trust = ContentTrust.Author) =>
        PageMarkupValidator.Validate(body, trust, Lookup, new RawContentGate());

    [Test]
    public void CleanPage_HasNoIssues() =>
        Assert.That(Validate("""<p>Hi</p><Component.Card radius="8px" columns="3" animate class="x" data-foo="1" />"""), Is.Empty);

    [Test]
    public void UnknownCitizen_IsAnError() =>
        Assert.That(Validate("<Component.Nope />").Single().Severity, Is.EqualTo(PageIssueSeverity.Error));

    [Test]
    public void UndeclaredAttribute_Warns_AndBadTypedValue_Errors()
    {
        var issues = Validate("""<Component.Card colour="red" columns="three" />""");
        Assert.Multiple(() =>
        {
            Assert.That(issues.Any(i => i.Severity == PageIssueSeverity.Warning && i.Message.Contains("'colour'")), Is.True);
            Assert.That(issues.Any(i => i.Severity == PageIssueSeverity.Error && i.Message.Contains("columns=")), Is.True);
        });
    }

    [Test]
    public void WhatTheSanitizerStrips_IsReported()
    {
        var issues = Validate("""<p onclick="x()">a</p><script>alert(1)</script>""");
        Assert.Multiple(() =>
        {
            Assert.That(issues.Any(i => i.Message.Contains("onclick")), Is.True);
            Assert.That(issues.Any(i => i.Message.Contains("<script>")), Is.True);
        });
    }

    [Test]
    public void UntrustedPageWithComponents_WarnsTheyWontRender() =>
        Assert.That(Validate("<Component.Card />", ContentTrust.Untrusted).Any(i => i.Message.Contains("Untrusted")), Is.True);
}

[TestFixture]
public class CitizenValidatorTests
{
    [TestCase("x.js", "var y = eval(code);")]
    [TestCase("x.js", "var f = new Function('a', 'return a');")]
    [TestCase("x.js", "document.write('<b>')")]
    [TestCase("x.js", "setTimeout(\"go()\", 10)")]
    [TestCase("x.css", ".a{width:expression(alert(1))}")]
    [TestCase("x.css", ".a{background:url(javascript:alert(1))}")]
    [TestCase("x.css", ".a{behavior:url(x.htc)}")]
    public void UnsafeAssetPatterns_AreRejected(string path, string content) =>
        Assert.That(CitizenValidator.ValidateAssets([(path, content)]), Is.Not.Empty);

    [TestCase("x.css", "html{scroll-behavior:smooth} .m{overscroll-behavior:contain}")]
    [TestCase("x.js", "// don't eval(anything) here\nsetTimeout(run, 10); el.evaluate();")]
    [TestCase("x.css", "/* never use expression( */ .a{color:red}")]
    public void LookalikesAndComments_AreAllowed(string path, string content) =>
        Assert.That(CitizenValidator.ValidateAssets([(path, content)]), Is.Empty);

    [Test]
    public void SettingsDifferingOnlyByCase_OrReservedNames_AreRejected()
    {
        var problems = CitizenValidator.ValidateSettings(
        [
            new IdeaManifestSetting { Name = "Radius" }, new IdeaManifestSetting { Name = "radius" },
            new IdeaManifestSetting { Name = "Kind" },
        ]);
        Assert.That(problems, Has.Count.EqualTo(2));
    }
}

/// <summary>
/// The CI gate: every package the host ships (src/MindAttic.Ideas.Blazor/library) passes the pack-time
/// citizen checks, and every page of the seed site validates against those packages' declared settings
/// with nothing silently removed by the sanitizer.
/// </summary>
[TestFixture]
public class ShippedContentValidationTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MindAttic.Ideas.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static IEnumerable<string> ShippedPackages() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "MindAttic.Ideas.Blazor", "library"), "*.idea")
            .OrderBy(p => p, StringComparer.Ordinal);

    private static IdeaManifest ReadManifest(ZipArchive zip)
    {
        using var s = zip.GetEntry("idea.json")!.Open();
        using var r = new StreamReader(s);
        return ManifestReader.Read(r.ReadToEnd());
    }

    [Test]
    public void EveryShippedPackage_PassesCitizenValidation()
    {
        var failures = new List<string>();
        var count = 0;
        foreach (var path in ShippedPackages())
        {
            count++;
            using var zip = ZipFile.OpenRead(path);
            var manifest = ReadManifest(zip);
            var assets = zip.Entries
                .Where(e => e.FullName.StartsWith("wwwroot/", StringComparison.Ordinal)
                            && (e.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".css", StringComparison.OrdinalIgnoreCase)))
                .Select(e => { using var r = new StreamReader(e.Open()); return (e.FullName, r.ReadToEnd()); })
                .ToList();
            failures.AddRange(CitizenValidator.ValidateSettings(manifest.Settings).Select(p => $"{Path.GetFileName(path)}: {p}"));
            failures.AddRange(CitizenValidator.ValidateAssets(assets).Select(p => $"{Path.GetFileName(path)}: {p}"));
        }
        Assert.That(count, Is.GreaterThan(0), "no shipped packages found");
        Assert.That(failures, Is.Empty, string.Join("\n", failures));
    }

    [Test]
    public void EverySeedPage_ValidatesAgainstTheShippedPackages()
    {
        var schemas = new Dictionary<(ContentKind, string), IReadOnlyDictionary<string, string>>();
        foreach (var path in ShippedPackages())
        {
            using var zip = ZipFile.OpenRead(path);
            var m = ReadManifest(zip);
            if (!Enum.TryParse<ContentKind>(m.Category, out var kind)) continue;
            schemas[(kind, m.Key.ToLowerInvariant())] = m.Settings.ToDictionary(s => s.Name, s => s.Type, StringComparer.OrdinalIgnoreCase);
        }
        CitizenSchemaLookup lookup = (kind, key, _) => schemas.GetValueOrDefault((kind, key.ToLowerInvariant()));

        using var seed = ZipFile.OpenRead(Path.Combine(RepoRoot(), "seed", "mindattic-site.idealist"));
        IdeaList list;
        using (var s = seed.GetEntry(IdeaList.ManifestEntryName)!.Open())
            list = JsonSerializer.Deserialize<IdeaList>(s, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var gate = new RawContentGate();
        var problems = new List<string>();
        foreach (var page in list.Pages)
        {
            var trust = string.Equals(page.BodyTrust, "Author", StringComparison.OrdinalIgnoreCase) ? ContentTrust.Author : ContentTrust.Untrusted;
            foreach (var issue in PageMarkupValidator.Validate(page.BodyHtml, trust, lookup, gate))
                if (issue.Severity == PageIssueSeverity.Error || issue.Message.StartsWith("Removed by the XSS sanitizer", StringComparison.Ordinal))
                    problems.Add($"/{page.Slug}: {issue.Message}");
        }
        Assert.That(list.Pages, Is.Not.Empty);
        Assert.That(problems, Is.Empty, string.Join("\n", problems));
    }
}
