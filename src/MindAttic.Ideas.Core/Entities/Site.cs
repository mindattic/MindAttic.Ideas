namespace MindAttic.Ideas.Core.Entities;

/// <summary>
/// A tenant/portal resolved by host header. Multi-site is a seam from day one: Pages carry a nullable
/// SiteId, so running single-site today and multi-tenant later is a resolver change, not a schema break.
/// </summary>
public sealed class Site : ContentEntityBase
{
    public string Key { get; set; } = "";          // upsert authority, e.g. "default"
    public string Name { get; set; } = "";
    /// <summary>Comma/space separated host bindings, e.g. "mindattic.com,www.mindattic.com".</summary>
    public string HostBindings { get; set; } = "";
    public string DefaultThemeKey { get; set; } = "";
    public int DefaultThemeVersion { get; set; } = 1;
    public bool IsDefault { get; set; }
    public string? SettingsJson { get; set; }

    // ---- sandbox lifecycle (MAI-A36) ----
    /// <summary>
    /// A throwaway site: the public showroom where a visitor can author pages, place components and
    /// install their own <c>.idea</c> packages without touching anything real. Its content is
    /// restored from a baseline when nobody is using it.
    /// </summary>
    public bool IsSandbox { get; set; }

    /// <summary>"when-idle" to reset once no session has been seen for <see cref="IdleGraceMinutes"/>; null to never reset.</summary>
    public string? ResetPolicy { get; set; }

    /// <summary>
    /// How long after the last active session before a reset. A grace period rather than "the moment
    /// they leave": a visitor between page loads has no live circuit for a beat, and wiping the site
    /// under them would look like a crash.
    /// </summary>
    public int IdleGraceMinutes { get; set; } = 10;

    public DateTime? LastResetUtc { get; set; }
}
