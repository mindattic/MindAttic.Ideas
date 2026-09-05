namespace MindAttic.Ideas.Core.Sites;

/// <summary>
/// The small, easily-mistyped half of "which domain was asked for", in one place.
/// <para>
/// Every interactive surface that needs the current site reads <c>NavigationManager.BaseUri</c> FIRST and
/// falls back to <c>HttpContext</c>, not the other way round: an InteractiveServer component has an
/// HttpContext during prerender and none on any later interaction, so a component that trusted it would
/// resolve the right site on first paint and the DEFAULT site on every navigation afterwards.
/// </para>
/// </summary>
public static class RequestSite
{
    /// <summary>The authority (<c>host:port</c>) of an absolute URI, or null when it is not one.</summary>
    public static string? HostOf(string? absoluteUri) =>
        Uri.TryCreate(absoluteUri, UriKind.Absolute, out var u) ? u.Authority : null;
}
