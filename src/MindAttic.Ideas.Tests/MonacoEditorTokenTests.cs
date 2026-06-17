using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Rendering;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// Verifies that the token strings MonacoEditor generates for its catalog IntelliSense provider
/// round-trip through the include-reference grammar (MAI-US-F8).
/// </summary>
[TestFixture]
public class MonacoEditorTokenTests
{
    // Mirrors the interpolation in MonacoEditor.razor: $"{{{{{kind}.{key}.V{version}}}}}"
    private static string FormatToken(ContentKind kind, string key, int version) =>
        $"{{{{{kind}.{key}.V{version}}}}}";

    [TestCase(ContentKind.Plugin, "tooltip",       1, "tooltip",       1)]
    [TestCase(ContentKind.Plugin, "my.complex.key", 5, "my.complex.key", 5)]
    [TestCase(ContentKind.Theme,  "cyberspace",    1, "cyberspace",    1)]
    [TestCase(ContentKind.Theme,  "dark",          2, "dark",          2)]
    public void IntelliSenseToken_ParsesBackViaTagGrammar(
        ContentKind kind, string key, int version, string expectedKey, int expectedVersion)
    {
        var token = FormatToken(kind, key, version);

        // BraceInclude extracts the inner reference from {{ … }}.
        var match = IncludeReferenceParser.BraceInclude.Match(token);
        Assert.That(match.Success, Is.True, $"BraceInclude did not match '{token}'");

        var inner = match.Groups[1].Value.ToLowerInvariant();
        Assert.That(IncludeReferenceParser.TryParseTag(inner, out var parsedKind, out var parsedKey, out var parsedVersion), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(parsedKind,    Is.EqualTo(kind));
            Assert.That(parsedKey,     Is.EqualTo(expectedKey));
            Assert.That(parsedVersion, Is.EqualTo(expectedVersion));
        });
    }

    [Test]
    public void IntelliSenseToken_InsertedInBody_ParsedByIncludeReferenceParser()
    {
        // Proves the full round-trip: Monaco inserts token → page body → Parse → (kind, key, version).
        var token = FormatToken(ContentKind.Plugin, "tooltip", 1);
        var html  = $"<p>Embed: {token}</p>";

        var refs = IncludeReferenceParser.Parse(html);

        Assert.That(refs, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(refs[0].Kind,    Is.EqualTo(ContentKind.Plugin));
            Assert.That(refs[0].Key,     Is.EqualTo("tooltip"));
            Assert.That(refs[0].Version, Is.EqualTo(1));
        });
    }
}
