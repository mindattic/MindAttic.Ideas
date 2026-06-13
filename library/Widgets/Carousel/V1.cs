using MindAttic.Ideas.Abstractions;

namespace MindAttic.Ideas.Widget.Carousel;

/// <summary>
/// MindAttic.Ideas.Widget.Carousel.V1 — the slideshow capability as an asset-only activator. Drop the
/// token once and ANY <c>&lt;div class="ma-carousel"&gt;</c> of <c>.ma-slide</c> children becomes a
/// slider with arrows, dots, and keyboard support. A slide is free-form markup; an image slide is a
/// slide carrying a base64 background-image CSS class (the page convention):
/// <code>
///   &lt;div class="ma-carousel" data-autoplay="6000"&gt;
///     &lt;div class="ma-slide img-sunset"&gt;&lt;/div&gt;
///     &lt;div class="ma-slide"&gt;&lt;h3&gt;Any markup&lt;/h3&gt;&lt;p&gt;works as a slide.&lt;/p&gt;&lt;/div&gt;
///   &lt;/div&gt;
/// </code>
/// <c>data-autoplay</c> (ms) is optional; autoplay pauses on hover/focus.
/// </summary>
public sealed class V1 : WidgetBase
{
    private const string Mount = "/_ideas/Widget/carousel/1";

    public override IReadOnlyList<string> StylesheetUrls { get; } = new[] { Mount + "/carousel.css" };
    public override IReadOnlyList<string> ScriptUrls { get; } = new[] { Mount + "/carousel.js" };
}
