using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Core.Rendering;

/// <summary>
/// The ONE grammar for the include token <c>{{ MindAttic.Ideas.{Kind}.{Name}[.V{n}|.Latest] [attrs] }}</c>,
/// extracted so the renderer (<see cref="IncludeExpander"/>) and the delete-guard
/// (ContentLifecycleService) parse references identically — no divergent duplicate. We use DOUBLE BRACES
/// (not a custom HTML element) because <c>&lt;MindAttic.Ideas.…/&gt;</c> is non-conforming HTML: the parser
/// ignores its self-close so it swallows following siblings, and a sanitizer strips the unknown element.
/// A <c>{{…}}</c> token is inert text — it survives HTML parsing, sanitization, and WYSIWYG editing — and is
/// resolved here. Tokens are matched only inside TEXT nodes (AngleSharp), so a token in an HTML comment is
/// ignored. The kind segment is REQUIRED (the catalog/guard/hoist key on kind+key). Attributes after the
/// reference are parsed by the renderer, not here (they don't affect reference identity).
/// </summary>
public static class IncludeReferenceParser
{
    private static readonly HtmlParser Parser = new();

    /// <summary>The include-token grammar: group 1 = the dotted reference, group 2 = the raw attribute tail.</summary>
    public static readonly Regex BraceInclude =
        new(@"\{\{\s*(MindAttic\.Ideas\.[A-Za-z0-9_.]+)([^}]*?)\}\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Every include reference in author HTML, in document order. Empty for null/blank.</summary>
    public static IReadOnlyList<(ContentKind Kind, string Key, int? Version)> Parse(string? html)
    {
        var result = new List<(ContentKind, string, int?)>();
        if (string.IsNullOrWhiteSpace(html)) return result;
        using var doc = Parser.ParseDocument("<!DOCTYPE html><html><head></head><body>" + html + "</body></html>");
        Walk(doc.Body!.ChildNodes, result);
        return result;
    }

    private static void Walk(INodeList nodes, List<(ContentKind, string, int?)> acc)
    {
        foreach (var node in nodes)
        {
            if (node is IText t)
            {
                foreach (Match m in BraceInclude.Matches(t.Text))
                    if (TryParseTag(m.Groups[1].Value.ToLowerInvariant(), out var kind, out var key, out var version))
                        acc.Add((kind, key, version));
            }
            else if (node is IElement el)
            {
                Walk(el.ChildNodes, acc);   // tokens live in text, but recurse so nested text is scanned
            }
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

    // ---- uses[] manifest grammar: "<Kind>.<key>[@<version>]" (the declarative dependency form a
    //      COMPILED page emits via [Uses], parallel to the data-page include tag). ----

    /// <summary>Parse one manifest <c>uses[]</c> entry, e.g. "Component.tooltip" or "Theme.cyberspace@1".</summary>
    public static bool TryParseUse(string? entry, out ContentKind kind, out string key, out int? version)
    {
        kind = default; key = ""; version = null;
        if (string.IsNullOrWhiteSpace(entry)) return false;
        var s = entry.Trim();

        var at = s.IndexOf('@');
        if (at >= 0)
        {
            if (int.TryParse(s.AsSpan(at + 1), out var v)) version = v;
            s = s[..at];
        }

        var dot = s.IndexOf('.');
        if (dot <= 0 || dot >= s.Length - 1) return false;
        if (!Enum.TryParse(s[..dot], ignoreCase: true, out kind)) return false;
        key = s[(dot + 1)..].Trim().ToLowerInvariant();
        return key.Length > 0;
    }

    /// <summary>Every parseable <c>uses[]</c> reference, in declared order. Empty for null/blank entries.</summary>
    public static IReadOnlyList<(ContentKind Kind, string Key, int? Version)> ParseUses(IEnumerable<string>? uses)
    {
        var result = new List<(ContentKind, string, int?)>();
        if (uses is null) return result;
        foreach (var u in uses)
            if (TryParseUse(u, out var k, out var key, out var v)) result.Add((k, key, v));
        return result;
    }

    /// <summary>True if a <c>uses[]</c> list pins this exact (kind,key,version).</summary>
    public static bool UsesPinsVersion(IEnumerable<string>? uses, ContentKind kind, string key, int version) =>
        ParseUses(uses).Any(r => r.Kind == kind && string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase) && r.Version == version);

    /// <summary>True if a <c>uses[]</c> list references this key with NO version pin (floats to latest).</summary>
    public static bool UsesFloatsKey(IEnumerable<string>? uses, ContentKind kind, string key) =>
        ParseUses(uses).Any(r => r.Kind == kind && string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase) && r.Version is null);
}
