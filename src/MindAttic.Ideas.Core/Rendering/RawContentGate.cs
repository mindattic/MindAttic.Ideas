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
        // Untrusted content keeps a conservative allow-list; scripts/styles are stripped.
        _sanitizer.AllowedSchemes.Add("mailto");
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
