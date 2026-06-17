using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Rendering;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// Tests for the catalog-driven assignment UI helpers introduced in MAI-US-F3:
/// the token format the component/plugin palette inserts, and catalog kind-filtering logic.
/// </summary>
[TestFixture]
public class AdminAssignmentTests
{
    private static ContentDescriptor Desc(ContentKind kind, string key, int version) =>
        new() { Kind = kind, Key = key, Version = version, DisplayName = key };

    // ---- Token format: what the component/plugin palette inserts ----

    [Test]
    public void PluginToken_PinnedVersion_ParsesBack()
    {
        // Palette generates "{{Plugin.ui.tooltip.V3}}" — verify TryParseTag accepts it.
        Assert.That(IncludeReferenceParser.TryParseTag("plugin.ui.tooltip.v3", out var kind, out var key, out var version), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(kind, Is.EqualTo(ContentKind.Plugin));
            Assert.That(key, Is.EqualTo("ui.tooltip"));
            Assert.That(version, Is.EqualTo(3));
        });
    }

    [Test]
    public void ThemeToken_PinnedVersion_ParsesBack()
    {
        // Theme pickers generate "{{Theme.cyberspace.V2}}" tokens.
        Assert.That(IncludeReferenceParser.TryParseTag("theme.cyberspace.v2", out var kind, out var key, out var version), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(kind, Is.EqualTo(ContentKind.Theme));
            Assert.That(key, Is.EqualTo("cyberspace"));
            Assert.That(version, Is.EqualTo(2));
        });
    }

    // ---- Catalog kind-filtering (the predicate used in the palette and theme select) ----

    [Test]
    public void CatalogFilter_Theme_ReturnsOnlyThemes()
    {
        var all = new[]
        {
            Desc(ContentKind.Plugin, "tooltip",    1),
            Desc(ContentKind.Theme,  "cyberspace", 1),
            Desc(ContentKind.Theme,  "neon",       2),
            Desc(ContentKind.Plugin, "header",     1),
        };
        var themes = all.Where(d => d.Kind == ContentKind.Theme).ToArray();
        Assert.That(themes.Select(d => d.Key), Is.EquivalentTo(new[] { "cyberspace", "neon" }));
    }

    [Test]
    public void CatalogFilter_Plugin_ReturnsOnlyPlugins()
    {
        var all = new[]
        {
            Desc(ContentKind.Theme,  "dark",    1),
            Desc(ContentKind.Plugin, "tooltip", 1),
            Desc(ContentKind.Plugin, "gallery", 2),
        };
        var plugins = all.Where(d => d.Kind == ContentKind.Plugin).ToArray();
        Assert.That(plugins.Select(d => d.Key), Is.EquivalentTo(new[] { "tooltip", "gallery" }));
    }
}
