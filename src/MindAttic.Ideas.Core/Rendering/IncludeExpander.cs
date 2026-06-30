using System.Collections.Concurrent;
using System.Globalization;
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
        // Author-trusted content: upgrade PascalCase HTML tags to <ma-component> before AngleSharp
        // normalises tag names to lowercase. Only Author content may embed component tags — untrusted
        // content leaves PascalCase tags as unknown HTML elements (AngleSharp lowercases them safely).
        if (trust == ContentTrust.Author)
            html = UpgradePascalCaseTags(html);
        using var doc = Parser.ParseDocument("<!DOCTYPE html><html><head></head><body>" + html + "</body></html>");
        var counter = new Counter { Next = seq };
        RenderNodes(builder, counter, doc.Body!.ChildNodes, new ExpandCtx(catalog, gate, trust, alerts, pageId, slug));
        seq = counter.Next;
    }

    // ── PascalCase component-tag pre-processor ────────────────────────────────────────────────
    // Converts <Alert type="error" /> and <Alert type="error">inner</Alert> to
    // <ma-component data-key="alert" type="error"></ma-component> / <ma-component ...>inner</ma-component>.
    // "ma-component" is a valid HTML5 custom element (hyphen required), so AngleSharp parses it
    // correctly and preserves all attributes and children. The data-key holds the lowercased
    // component key; all other attributes become component parameters in RenderNodes.
    // Order: self-closing first, then open tags, then close tags — all three are idempotent
    // and safe to run in sequence because self-close is consumed before open-tag pass.

    private static readonly Regex _pascalSelfClose =
        new(@"<([A-Z][A-Za-z0-9]*)((?:\s[^/>]*?)?)\s*/>", RegexOptions.Compiled);
    private static readonly Regex _pascalOpen =
        new(@"<([A-Z][A-Za-z0-9]*)((?:\s[^>]*?)?)>", RegexOptions.Compiled);
    private static readonly Regex _pascalClose =
        new(@"</([A-Z][A-Za-z0-9]*)>", RegexOptions.Compiled);

    private static string UpgradePascalCaseTags(string html)
    {
        // Quick exit: scan for '<' followed by an uppercase letter.
        bool hasPascal = false;
        for (int i = 0; i < html.Length - 1; i++)
            if (html[i] == '<' && char.IsUpper(html[i + 1])) { hasPascal = true; break; }
        if (!hasPascal) return html;

        html = _pascalSelfClose.Replace(html, static m =>
        {
            var key  = m.Groups[1].Value.ToLowerInvariant();
            var tail = m.Groups[2].Value.Trim();
            return tail.Length > 0
                ? $"<ma-component data-key=\"{key}\" {tail}></ma-component>"
                : $"<ma-component data-key=\"{key}\"></ma-component>";
        });

        html = _pascalOpen.Replace(html, static m =>
        {
            var key  = m.Groups[1].Value.ToLowerInvariant();
            var tail = m.Groups[2].Value.Trim();
            return tail.Length > 0
                ? $"<ma-component data-key=\"{key}\" {tail}>"
                : $"<ma-component data-key=\"{key}\">";
        });

        return _pascalClose.Replace(html, "</ma-component>");
    }

    private static void RenderNodes(RenderTreeBuilder b, Counter c, INodeList nodes, ExpandCtx ctx)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                // Script/style: author content is emitted as-is; untrusted content drops the entire element —
                // an empty <script></script> with attributes (e.g. nonce="…") is still an XSS vector surface.
                case IElement el when el.LocalName is "script" or "style":
                    if (ctx.Trust != ContentTrust.Author) break;   // untrusted: drop the whole element
                    b.OpenElement(c.Next++, el.LocalName);
                    foreach (var attr in el.Attributes)
                        b.AddAttribute(c.Next++, attr.Name, attr.Value);
                    b.AddMarkupContent(c.Next++, el.InnerHtml);
                    b.CloseElement();
                    break;

                // PascalCase component tag — <Alert type="error" /> or <Alert>children</Alert>.
                // UpgradePascalCaseTags() wraps these as <ma-component data-key="…" …> before parsing.
                // Try Component kind first; fall back to Plugin if not found in that kind.
                case IElement el when el.LocalName == "ma-component":
                {
                    var key = el.GetAttribute("data-key") ?? "";
                    if (string.IsNullOrEmpty(key)) break; // malformed tag — skip silently

                    var attrs = new List<KeyValuePair<string, object?>>();
                    foreach (var attr in el.Attributes)
                    {
                        if (attr.Name == "data-key") continue;
                        // Apply the same XSS filtering as regular elements (Author trust bypasses it).
                        if (ctx.Trust != ContentTrust.Author &&
                            (attr.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                             (UrlAttributes.Contains(attr.Name) && IsUnsafeUri(attr.Value))))
                            continue;
                        attrs.Add(new KeyValuePair<string, object?>(attr.Name, attr.Value));
                    }

                    RenderFragment? childContent = el.ChildNodes.Length > 0
                        ? innerBuilder =>
                        {
                            var ic = new Counter { Next = 0 };
                            RenderNodes(innerBuilder, ic, el.ChildNodes, ctx);
                        }
                        : null;

                    var res = ctx.Catalog.ResolveTag(ContentKind.Component, key, null);
                    if (res.Outcome != ContentResolution.Resolved)
                        res = ctx.Catalog.ResolveTag(ContentKind.Plugin, key, null);

                    if (res.Outcome == ContentResolution.Resolved)
                    {
                        var type   = res.Type!;
                        var pTypes = ParameterTypesOf(type);
                        b.OpenComponent(c.Next++, type);
                        foreach (var attr in attrs)
                        {
                            var val = pTypes.TryGetValue(attr.Key, out var pt)
                                ? CoerceAttributeValue(pt, attr.Value) : attr.Value;
                            b.AddAttribute(c.Next++, attr.Key, val);
                        }
                        if (childContent is not null && HasChildContent(type))
                            b.AddAttribute(c.Next++, "ChildContent", childContent);
                        b.CloseComponent();
                    }
                    else
                    {
                        EmitMissing(b, ref c.Next, $"<{key}>");
                        ctx.Alerts?.RaiseMissing(ContentKind.Component, key, null, ctx.PageId, ctx.Slug);
                    }
                    break;
                }

                case IElement el:
                    b.OpenElement(c.Next++, el.LocalName);
                    foreach (var attr in el.Attributes)
                    {
                        // Strip XSS vectors from untrusted content: event handlers, and unsafe-scheme
                        // URIs on URL-bearing attributes only. data-* custom attributes whose values
                        // start with "data:" (e.g. data-payload="data:…") are safe and must not be
                        // filtered — they are application data, not navigation targets.
                        if (ctx.Trust != ContentTrust.Author &&
                            (attr.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                             (UrlAttributes.Contains(attr.Name) && IsUnsafeUri(attr.Value))))
                            continue;
                        b.AddAttribute(c.Next++, attr.Name, attr.Value);
                    }
                    RenderNodes(b, c, el.ChildNodes, ctx);
                    b.CloseElement();
                    break;

                case IText t:
                    EmitText(b, c, t.Text, ctx);
                    break;
            }
        }
    }

    // Author text, with every {{ MindAttic.Ideas.… [attrs] }} token swapped for the live citizen it names.
    // Text around tokens is emitted verbatim (AddContent HTML-encodes it). A parseable-but-unresolved
    // reference degrades to the placeholder (EmitInclude); an unparseable token is left as literal text.
    private static void EmitText(RenderTreeBuilder b, Counter c, string text, ExpandCtx ctx)
    {
        var last = 0;
        foreach (Match m in IncludeReferenceParser.BraceInclude.Matches(text))
        {
            if (m.Index > last) b.AddContent(c.Next++, text[last..m.Index]);
            if (IncludeReferenceParser.TryParseTag(m.Groups[1].Value.ToLowerInvariant(), out var kind, out var key, out var version))
                EmitInclude(b, ref c.Next, kind, key, version, m.Value, ctx.Catalog,
                    ParseAttributes(m.Groups[2].Value), childContent: null, ctx.Alerts, ctx.PageId, ctx.Slug);
            else
                b.AddContent(c.Next++, m.Value);   // not a valid reference -> leave the literal token
            last = m.Index + m.Length;
        }
        if (last < text.Length) b.AddContent(c.Next++, text[last..]);
    }

    // One token attribute: name | name=bareValue | name="quoted" | name='quoted'. A bare name => true.
    private static readonly Regex AttrPair =
        new(@"([A-Za-z_:][\w:.\-]*)\s*(?:=\s*(?:""([^""]*)""|'([^']*)'|([^\s}]+)))?", RegexOptions.Compiled);

    private static IReadOnlyList<KeyValuePair<string, object?>> ParseAttributes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<KeyValuePair<string, object?>>();
        var list = new List<KeyValuePair<string, object?>>();
        foreach (Match m in AttrPair.Matches(raw))
        {
            object? value = m.Groups[2].Success ? m.Groups[2].Value
                          : m.Groups[3].Success ? m.Groups[3].Value
                          : m.Groups[4].Success ? m.Groups[4].Value
                          : true;   // bare attribute (e.g. required)
            list.Add(new KeyValuePair<string, object?>(m.Groups[1].Value, value));
        }
        return list;
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
                var paramTypes = ParameterTypesOf(type);
                b.OpenComponent(seq++, type);
                foreach (var attr in attributes)
                {
                    // RFC 0001 typed-attribute coercion: a token attribute that matches a declared
                    // typed [Parameter] is converted to that type (bool/int/double/enum/…); anything
                    // else keeps its raw value and lands in the CaptureUnmatchedValues bag.
                    var value = paramTypes.TryGetValue(attr.Key, out var pt)
                        ? CoerceAttributeValue(pt, attr.Value)
                        : attr.Value;
                    b.AddAttribute(seq++, attr.Key, value);
                }
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

    // Attributes whose values are treated as URLs — IsUnsafeUri is only applied to these.
    // data-* attributes (e.g. data-type="data:application/json") must NOT be filtered here because
    // the "data:" prefix in their VALUE is not a navigation URI; it is safe application data.
    private static readonly HashSet<string> UrlAttributes = new(StringComparer.OrdinalIgnoreCase)
        { "href", "src", "action", "formaction", "data", "poster", "cite", "xlink:href", "background" };

    private static bool IsUnsafeUri(string value)
    {
        var v = value.TrimStart();
        return v.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasChildContent(Type type)
    {
        var p = type.GetProperty("ChildContent", BindingFlags.Public | BindingFlags.Instance);
        return p is not null
            && p.PropertyType == typeof(RenderFragment)
            && p.GetCustomAttribute<ParameterAttribute>() is not null;
    }

    // ---- RFC 0001: typed-attribute coercion -----------------------------------------------------

    // name -> property type for every typed [Parameter] (the CaptureUnmatchedValues bag is excluded
    // on purpose: its values must stay raw). Case-insensitive: token attributes are author markup.
    private static readonly ConcurrentDictionary<Type, Dictionary<string, Type>> ParameterTypeCache = new();

    private static Dictionary<string, Type> ParameterTypesOf(Type componentType) =>
        ParameterTypeCache.GetOrAdd(componentType, static t =>
        {
            var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanWrite) continue;
                if (p.GetCustomAttribute<ParameterAttribute>() is not { CaptureUnmatchedValues: false }) continue;
                map[p.Name] = p.PropertyType;
            }
            return map;
        });

    /// <summary>
    /// Converts a token attribute's raw value (string, or bool for a bare attribute) to the declared
    /// parameter type: bool, enums (by name, case-insensitive), and the IConvertible numerics
    /// (int/long/double/decimal/…), with Nullable&lt;T&gt; unwrapped. A failed conversion returns the
    /// RAW value — a render never throws; Blazor's own parameter binding then surfaces the mismatch.
    /// </summary>
    public static object? CoerceAttributeValue(Type targetType, object? raw)
    {
        if (raw is null) return null;
        var t = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (t.IsInstanceOfType(raw)) return raw;
        if (t == typeof(string)) return raw.ToString();
        if (t == typeof(object)) return raw;
        var s = raw.ToString();
        if (string.IsNullOrWhiteSpace(s)) return raw;
        try
        {
            if (t == typeof(bool)) return bool.Parse(s);
            if (t.IsEnum) return Enum.Parse(t, s, ignoreCase: true);
            return Convert.ChangeType(s, t, CultureInfo.InvariantCulture);
        }
        catch
        {
            return raw;
        }
    }

    internal static void EmitMissing(RenderTreeBuilder b, ref int seq, string tag)
    {
        b.OpenComponent<MissingContent>(seq++);
        b.AddComponentParameter(seq++, nameof(MissingContent.Key), tag);
        b.CloseComponent();
    }
}
