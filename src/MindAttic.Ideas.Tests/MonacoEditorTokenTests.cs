using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Rendering;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// Verifies that the tag strings MonacoEditor generates for its catalog IntelliSense provider
/// round-trip through the include-reference grammar (MAI-US-F8).
/// The completion now emits dotted PascalCase HTML tags: &lt;Kind.Key /&gt;
/// </summary>
[TestFixture]
public class MonacoEditorTokenTests
{
    // Mirrors the interpolation in MonacoEditor.razor:
    // $"<{d.Kind}.{char.ToUpperInvariant(d.Key[0])}{d.Key[1..]} />"
    private static string FormatTag(ContentKind kind, string key)
    {
        var pascalKey = char.ToUpperInvariant(key[0]) + key[1..];
        return $"<{kind}.{pascalKey} />";
    }

    [TestCase(ContentKind.Plugin, "tooltip",    "tooltip")]
    [TestCase(ContentKind.Theme,  "cyberspace", "cyberspace")]
    [TestCase(ContentKind.Theme,  "dark",       "dark")]
    public void IntelliSenseTag_ParsesBackViaIncludeReferenceParser(
        ContentKind kind, string key, string expectedKey)
    {
        var tag = FormatTag(kind, key);

        var refs = IncludeReferenceParser.Parse(tag);
        Assert.That(refs, Has.Count.EqualTo(1), $"Parse did not find a reference in '{tag}'");

        Assert.Multiple(() =>
        {
            Assert.That(refs[0].Kind, Is.EqualTo(kind));
            Assert.That(refs[0].Key,  Is.EqualTo(expectedKey));
        });
    }

    [Test]
    public void IntelliSenseTag_InsertedInBody_ParsedByIncludeReferenceParser()
    {
        // Proves the full round-trip: Monaco inserts tag → page body → Parse → (kind, key, version).
        var tag  = FormatTag(ContentKind.Plugin, "tooltip");
        var html = $"<p>Embed: {tag}</p>";

        var refs = IncludeReferenceParser.Parse(html);

        Assert.That(refs, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(refs[0].Kind,    Is.EqualTo(ContentKind.Plugin));
            Assert.That(refs[0].Key,     Is.EqualTo("tooltip"));
            Assert.That(refs[0].Version, Is.Null, "floating tag has no version pin");
        });
    }
}
