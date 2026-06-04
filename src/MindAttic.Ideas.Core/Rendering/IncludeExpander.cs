using System.Reflection;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Core.Rendering;

/// <summary>
/// Expands free-form author HTML into a render tree, rewriting each locked include tag
/// <c>&lt;MindAttic.Ideas.{Kind}.{Name}.V{n} …/&gt;</c> into the live content resolved from the
/// catalog — the SAME tag form a code page would use. Parsing uses AngleSharp tokenization (NEVER
/// regex), so tags inside text/comments are handled correctly and an unresolved/stale/disabled tag
/// degrades to a visible placeholder — never a render crash (a page must never be invalid). Inner
/// content is passed to the resolved type as <c>ChildContent</c>.
/// </summary>
public static class IncludeExpander
{
    /// <summary>The frozen include-tag prefix. Identity = MindAttic.Ideas.{Kind}.{Name}.V{n}.</summary>
    public const string TagPrefix = "mindattic.ideas.";

    private static readonly HtmlParser Parser = new();

    // A custom element like <MindAttic.Ideas.Module.Tooltip /> is NOT self-closing in HTML — the
    // parser would treat it as an open tag and swallow following siblings as children. Normalize the
    // known include tags to explicit paired tags first so they parse as empty elements.
    private static readonly Regex SelfClosingInclude =
        new(@"<(MindAttic\.Ideas\.[\w.]+)((?:\s[^<>]*?)?)\s*/>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed class Counter { public int Next; }

    public static void Expand(
        RenderTreeBuilder builder, ref int seq, string? html,
        IContentCatalog catalog, IRawContentGate gate, ContentTrust trust)
    {
        if (string.IsNullOrWhiteSpace(html)) return;
        html = SelfClosingInclude.Replace(html, "<$1$2></$1>");
        using var doc = Parser.ParseDocument("<!DOCTYPE html><html><head></head><body>" + html + "</body></html>");
        var counter = new Counter { Next = seq };
        RenderNodes(builder, counter, doc.Body!.ChildNodes, catalog, gate, trust);
        seq = counter.Next;
    }

    private static void RenderNodes(
        RenderTreeBuilder b, Counter c, INodeList nodes,
        IContentCatalog catalog, IRawContentGate gate, ContentTrust trust)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case IElement el when el.LocalName.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase):
                    RenderInclude(b, c, el, catalog, gate, trust);
                    break;

                // Script/style inner content must be emitted raw; untrusted content drops it entirely.
                case IElement el when el.LocalName is "script" or "style":
                    b.OpenElement(c.Next++, el.LocalName);
                    foreach (var attr in el.Attributes) b.AddAttribute(c.Next++, attr.Name, attr.Value);
                    if (trust == ContentTrust.Author) b.AddMarkupContent(c.Next++, el.InnerHtml);
                    b.CloseElement();
                    break;

                case IElement el:
                    b.OpenElement(c.Next++, el.LocalName);
                    foreach (var attr in el.Attributes) b.AddAttribute(c.Next++, attr.Name, attr.Value);
                    RenderNodes(b, c, el.ChildNodes, catalog, gate, trust);
                    b.CloseElement();
                    break;

                case IText t:
                    b.AddContent(c.Next++, t.Text);
                    break;
            }
        }
    }

    private static void RenderInclude(
        RenderTreeBuilder b, Counter c, IElement el,
        IContentCatalog catalog, IRawContentGate gate, ContentTrust trust)
    {
        // localName == "mindattic.ideas.{kind}.{key...}[.v{n}]"  — version segment is OPTIONAL.
        var parts = el.LocalName.Split('.');
        if (parts.Length < 4 || !Enum.TryParse<ContentKind>(parts[2], ignoreCase: true, out var kind))
        {
            RenderMissing(b, c, el.LocalName);
            return;
        }

        // Trailing version segment is OPTIONAL:
        //   .V3      -> pins version 3
        //   .Latest  -> explicitly the newest enabled version
        //   (absent) -> implicitly the newest enabled version
        int keyEnd = parts.Length;
        int? version = null;
        if (TryParseVersion(parts[^1], out var v))
        {
            version = v;
            keyEnd = parts.Length - 1;
        }
        else if (parts[^1].Equals("latest", StringComparison.OrdinalIgnoreCase))
        {
            keyEnd = parts.Length - 1; // version stays null => resolve latest
        }
        var key = string.Join('.', parts[3..keyEnd]);
        if (key.Length == 0)
        {
            RenderMissing(b, c, el.LocalName);
            return;
        }

        var desc = version is int pinned
            ? catalog.Find(kind, key, pinned) ?? catalog.FindLatest(kind, key)
            : catalog.FindLatest(kind, key);
        var type = desc is null ? null : catalog.ResolveType(desc);
        if (type is null)
        {
            RenderMissing(b, c, el.LocalName);
            return;
        }

        b.OpenComponent(c.Next++, type);
        foreach (var attr in el.Attributes)
            b.AddAttribute(c.Next++, attr.Name, attr.Value);
        // Pass inner content as ChildContent only if the resolved type actually declares it.
        if (el.ChildNodes.Length > 0 && HasChildContent(type))
        {
            var children = el.ChildNodes;
            RenderFragment child = inner =>
            {
                var ic = new Counter();
                RenderNodes(inner, ic, children, catalog, gate, trust);
            };
            b.AddAttribute(c.Next++, "ChildContent", child);
        }
        b.CloseComponent();
    }

    private static bool HasChildContent(Type type)
    {
        var p = type.GetProperty("ChildContent", BindingFlags.Public | BindingFlags.Instance);
        return p is not null
            && p.PropertyType == typeof(RenderFragment)
            && p.GetCustomAttribute<ParameterAttribute>() is not null;
    }

    private static bool TryParseVersion(string seg, out int version)
    {
        version = 0;
        return seg.Length > 1 && seg[0] is 'v' or 'V' && int.TryParse(seg.AsSpan(1), out version);
    }

    private static void RenderMissing(RenderTreeBuilder b, Counter c, string tag)
    {
        b.OpenComponent<MissingContent>(c.Next++);
        b.AddComponentParameter(c.Next++, nameof(MissingContent.Key), tag);
        b.CloseComponent();
    }
}
