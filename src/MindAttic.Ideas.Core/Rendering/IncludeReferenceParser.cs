using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Core.Rendering;

/// <summary>
/// The ONE grammar for the locked include tag <c>&lt;MindAttic.Ideas.{Kind}.{Name}[.V{n}|.Latest]/&gt;</c>,
/// extracted so the renderer (<see cref="IncludeExpander"/>) and the delete-guard
/// (ContentLifecycleService) parse references identically — no divergent duplicate. Uses AngleSharp
/// tokenization (never regex matching of the body), so tags in text/comments are handled correctly.
/// </summary>
public static class IncludeReferenceParser
{
    private static readonly HtmlParser Parser = new();

    // Custom <MindAttic.Ideas.X /> isn't self-closing in HTML; normalize to paired tags first.
    private static readonly Regex SelfClosingInclude =
        new(@"<(MindAttic\.Ideas\.[\w.]+)((?:\s[^<>]*?)?)\s*/>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Every include reference in author HTML, in document order. Empty for null/blank.</summary>
    public static IReadOnlyList<(ContentKind Kind, string Key, int? Version)> Parse(string? html)
    {
        var result = new List<(ContentKind, string, int?)>();
        if (string.IsNullOrWhiteSpace(html)) return result;
        var normalized = SelfClosingInclude.Replace(html, "<$1$2></$1>");
        using var doc = Parser.ParseDocument("<!DOCTYPE html><html><head></head><body>" + normalized + "</body></html>");
        Walk(doc.Body!.ChildNodes, result);
        return result;
    }

    private static void Walk(INodeList nodes, List<(ContentKind, string, int?)> acc)
    {
        foreach (var node in nodes)
        {
            if (node is not IElement el) continue;
            if (el.LocalName.StartsWith(IncludeExpander.TagPrefix, StringComparison.OrdinalIgnoreCase)
                && TryParseTag(el.LocalName, out var kind, out var key, out var version))
            {
                acc.Add((kind, key, version));
            }
            Walk(el.ChildNodes, acc);
        }
    }

    /// <summary>Parse "mindattic.ideas.{kind}.{key...}[.v{n}|.latest]". Version null = float to latest.</summary>
    public static bool TryParseTag(string localName, out ContentKind kind, out string key, out int? version)
    {
        kind = default; key = ""; version = null;
        var parts = localName.Split('.');
        if (parts.Length < 4 || !Enum.TryParse(parts[2], ignoreCase: true, out kind)) return false;

        int keyEnd = parts.Length;
        if (TryParseVersion(parts[^1], out var v)) { version = v; keyEnd = parts.Length - 1; }
        else if (parts[^1].Equals("latest", StringComparison.OrdinalIgnoreCase)) { keyEnd = parts.Length - 1; }

        key = string.Join('.', parts[3..keyEnd]);
        return key.Length > 0;
    }

    public static bool TryParseVersion(string seg, out int version)
    {
        version = 0;
        return seg.Length > 1 && seg[0] is 'v' or 'V' && int.TryParse(seg.AsSpan(1), out version);
    }

    /// <summary>True if author HTML pins this exact (kind,key,version).</summary>
    public static bool BodyPinsVersion(string? html, ContentKind kind, string key, int version) =>
        Parse(html).Any(r => r.Kind == kind && string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase) && r.Version == version);

    /// <summary>True if author HTML references this key at all (pinned or floating).</summary>
    public static bool BodyReferencesKey(string? html, ContentKind kind, string key) =>
        Parse(html).Any(r => r.Kind == kind && string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>True if author HTML references this key with NO version pin (floats to latest).</summary>
    public static bool BodyFloatsKey(string? html, ContentKind kind, string key) =>
        Parse(html).Any(r => r.Kind == kind && string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase) && r.Version is null);
}
