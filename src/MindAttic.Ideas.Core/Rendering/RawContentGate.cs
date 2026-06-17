using Ganss.Xss;
using Microsoft.AspNetCore.Components;
using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Core.Rendering;

/// <summary>
/// The SOLE place a <see cref="MarkupString"/> is constructed from author content. Author trust =&gt;
/// raw passthrough (intentional admin HTML/JS honored); Untrusted =&gt; sanitized via HtmlSanitizer.
/// Everywhere else, constructing a MarkupString from stored content is forbidden by convention.
/// </summary>
public sealed class RawContentGate : IRawContentGate
{
    private readonly HtmlSanitizer _sanitizer;

    public RawContentGate()
    {
        _sanitizer = new HtmlSanitizer();

        // Form-family tags enable phishing/credential-harvesting overlays.
        foreach (var tag in new[] { "form", "fieldset", "legend", "input", "button",
                                     "select", "textarea", "option", "optgroup", "label", "datalist" })
            _sanitizer.AllowedTags.Remove(tag);

        // Embedded-content / media tags allow frame injection and plugin execution.
        foreach (var tag in new[] { "video", "audio", "source", "track", "picture",
                                     "embed", "object", "applet", "canvas", "map", "area" })
            _sanitizer.AllowedTags.Remove(tag);

        // Dangerous attributes:
        //   action/method/enctype/formaction: form hijacking
        //   style: CSS overlay + url() exfiltration
        //   target: tab-nabbing (opener.location re-assignment in the new tab)
        //   usemap/ismap: image-map phishing
        foreach (var attr in new[] { "action", "method", "enctype", "formaction",
                                      "style", "target", "usemap", "ismap" })
            _sanitizer.AllowedAttributes.Remove(attr);

        // Restrict URL schemes to safe navigational/contact values only.
        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.AllowedSchemes.Add("tel");

        // Defense-in-depth: explicitly reject data: URIs at the filter layer.
        // AllowedSchemes already blocks them; this survives any future AllowedSchemes edit.
        _sanitizer.FilterUrl += static (_, e) =>
        {
            if (e.OriginalUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                e.SanitizedUrl = string.Empty;
        };
    }

    public MarkupString Emit(string? html, ContentTrust trust)
    {
        if (string.IsNullOrEmpty(html))
            return default;

        return trust == ContentTrust.Author
            ? new MarkupString(html)                       // intentional, admin-authored — verbatim
            : new MarkupString(_sanitizer.Sanitize(html)); // untrusted — sanitized
    }
}
