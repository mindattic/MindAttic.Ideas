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
/// <c>&lt;MindAttic.Ideas.{Kind}.{Name}[.V{n}|.Latest]/&gt;</c> into the live content resolved from the
/// catalog — the SAME tag form a code page would use. Parsing uses AngleSharp tokenization (the shared
/// <see cref="IncludeReferenceParser"/> grammar), so tags inside text/comments are handled correctly and
/// an unresolved/stale/disabled tag degrades to a visible placeholder — never a render crash. When an
/// <see cref="IRenderAlertSink"/> is supplied, a missing/disabled reference also raises an Admin Inbox
/// alert (fire-and-forget). Inner content is passed to the resolved type as <c>ChildContent</c>.
/// </summary>
public static class IncludeExpander
{
    /// <summary>The frozen include-tag prefix. Identity = MindAttic.Ideas.{Kind}.{Name}.V{n}.</summary>
    public const string TagPrefix = "mindattic.ideas.";

    private static readonly HtmlParser Parser = new();

    // A custom element like <MindAttic.Ideas.Component.Tooltip /> is NOT self-closing in HTML — the
    // parser would treat it as an open tag and swallow following siblings as children. Normalize the
    // known include tags to explicit paired tags first so they parse as empty elements.
    private static readonly Regex SelfClosingInclude =
        new(@"<(MindAttic\.Ideas\.[\w.]+)((?:\s[^<>]*?)?)\s*/>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private sealed class Counter { public int Next; }

    private sealed record ExpandCtx(
        IContentCatalog Catalog, IRawContentGate Gate, ContentTrust Trust,
        IRenderAlertSink? Alerts, Guid PageId, string Slug);

    public static void Expand(
        RenderTreeBuilder builder, ref int seq, string? html,
        IContentCatalog catalog, IRawContentGate gate, ContentTrust trust,
        IRenderAlertSink? alerts = null, Guid pageId = default, string slug = "")
    {
        if (string.IsNullOrWhiteSpace(html)) return;
        html = SelfClosingInclude.Replace(html, "<$1$2></$1>");
        using var doc = Parser.ParseDocument("<!DOCTYPE html><html><head></head><body>" + html + "</body></html>");
        var counter = new Counter { Next = seq };
        RenderNodes(builder, counter, doc.Body!.ChildNodes, new ExpandCtx(catalog, gate, trust, alerts, pageId, slug));
        seq = counter.Next;
    }

    private static void RenderNodes(RenderTreeBuilder b, Counter c, INodeList nodes, ExpandCtx ctx)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case IElement el when el.LocalName.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase):
                    RenderInclude(b, c, el, ctx);
                    break;

                // Script/style inner content must be emitted raw; untrusted content drops it entirely.
                case IElement el when el.LocalName is "script" or "style":
                    b.OpenElement(c.Next++, el.LocalName);
                    foreach (var attr in el.Attributes) b.AddAttribute(c.Next++, attr.Name, attr.Value);
                    if (ctx.Trust == ContentTrust.Author) b.AddMarkupContent(c.Next++, el.InnerHtml);
                    b.CloseElement();
                    break;

                case IElement el:
                    b.OpenElement(c.Next++, el.LocalName);
                    foreach (var attr in el.Attributes) b.AddAttribute(c.Next++, attr.Name, attr.Value);
                    RenderNodes(b, c, el.ChildNodes, ctx);
                    b.CloseElement();
                    break;

                case IText t:
                    b.AddContent(c.Next++, t.Text);
                    break;
            }
        }
    }

    private static void RenderInclude(RenderTreeBuilder b, Counter c, IElement el, ExpandCtx ctx)
    {
        // Malformed include tag (no kind / no key) — placeholder only; it's an author typo, not a
        // missing dependency, so no inbox alert (we can't categorize it).
        if (!IncludeReferenceParser.TryParseTag(el.LocalName, out var kind, out var key, out var version))
        {
            RenderMissing(b, c, el.LocalName);
            return;
        }

        var resolved = ctx.Catalog.ResolveTag(kind, key, version);
        switch (resolved.Outcome)
        {
            case ContentResolution.Resolved:
                var type = resolved.Type!;
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
                        RenderNodes(inner, ic, children, ctx);
                    };
                    b.AddAttribute(c.Next++, "ChildContent", child);
                }
                b.CloseComponent();
                break;

            case ContentResolution.Disabled:
                RenderMissing(b, c, el.LocalName);
                ctx.Alerts?.RaiseDisabled(kind, key, version, ctx.PageId, ctx.Slug);
                break;

            default: // Missing
                RenderMissing(b, c, el.LocalName);
                ctx.Alerts?.RaiseMissing(kind, key, version, ctx.PageId, ctx.Slug);
                break;
        }
    }

    private static bool HasChildContent(Type type)
    {
        var p = type.GetProperty("ChildContent", BindingFlags.Public | BindingFlags.Instance);
        return p is not null
            && p.PropertyType == typeof(RenderFragment)
            && p.GetCustomAttribute<ParameterAttribute>() is not null;
    }

    private static void RenderMissing(RenderTreeBuilder b, Counter c, string tag)
    {
        b.OpenComponent<MissingContent>(c.Next++);
        b.AddComponentParameter(c.Next++, nameof(MissingContent.Key), tag);
        b.CloseComponent();
    }
}
