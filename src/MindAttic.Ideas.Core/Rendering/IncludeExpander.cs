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

    // A custom element like <MindAttic.Ideas.Plugin.Tooltip /> is NOT self-closing in HTML — the
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
            EmitMissing(b, ref c.Next, el.LocalName);
            return;
        }

        var attrs = new List<KeyValuePair<string, object?>>(el.Attributes.Length);
        foreach (var attr in el.Attributes) attrs.Add(new(attr.Name, attr.Value));

        // Inner content is only built when there ARE child nodes; the HasChildContent gate is applied in
        // EmitInclude so the same rule governs both the data-page and the CmsInclude (code-page) path.
        RenderFragment? child = null;
        if (el.ChildNodes.Length > 0)
        {
            var children = el.ChildNodes;
            child = inner =>
            {
                var ic = new Counter();
                RenderNodes(inner, ic, children, ctx);
            };
        }

        EmitInclude(b, ref c.Next, kind, key, version, el.LocalName, ctx.Catalog,
            attrs, child, ctx.Alerts, ctx.PageId, ctx.Slug);
    }

    /// <summary>
    /// The ONE resolve-and-render routine, shared by the data-page expander (above) and the compiled-page
    /// <see cref="IncludeRenderer"/>/<c>CmsInclude</c> seam — so both produce byte-identical render trees
    /// (attribute flow, ChildContent, Disabled-vs-Missing placeholder, Admin-Inbox alert). Never throws.
    /// </summary>
    internal static void EmitInclude(
        RenderTreeBuilder b, ref int seq,
        ContentKind kind, string key, int? version, string displayTag,
        IContentCatalog catalog,
        IReadOnlyList<KeyValuePair<string, object?>> attributes,
        RenderFragment? childContent,
        IRenderAlertSink? alerts, Guid pageId, string slug)
    {
        var resolved = catalog.ResolveTag(kind, key, version);
        switch (resolved.Outcome)
        {
            case ContentResolution.Resolved:
                var type = resolved.Type!;
                b.OpenComponent(seq++, type);
                foreach (var attr in attributes)
                    b.AddAttribute(seq++, attr.Key, attr.Value);
                // Pass inner content as ChildContent only if the resolved type actually declares it.
                if (childContent is not null && HasChildContent(type))
                    b.AddAttribute(seq++, "ChildContent", childContent);
                b.CloseComponent();
                break;

            case ContentResolution.Disabled:
                EmitMissing(b, ref seq, displayTag);
                alerts?.RaiseDisabled(kind, key, version, pageId, slug);
                break;

            default: // Missing
                EmitMissing(b, ref seq, displayTag);
                alerts?.RaiseMissing(kind, key, version, pageId, slug);
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

    internal static void EmitMissing(RenderTreeBuilder b, ref int seq, string tag)
    {
        b.OpenComponent<MissingContent>(seq++);
        b.AddComponentParameter(seq++, nameof(MissingContent.Key), tag);
        b.CloseComponent();
    }
}
