using MindAttic.Ideas.Core.Entities;

namespace MindAttic.Ideas.Core.Sites;

/// <summary>
/// Who owns an upload (MAI-A36).
/// <para>
/// The rule lives here rather than inline at the upload surface, because a second upload surface that
/// re-derives its own copy is exactly how a showroom visitor's package ends up shared with production.
/// One home, one answer.
/// </para>
/// </summary>
public static class InstallScope
{
    /// <summary>
    /// The site an upload arriving on <paramref name="site"/> installs into: that site when it is a
    /// SANDBOX, and null — shared — for everything else, which is what every install meant before sites
    /// could own citizens.
    /// <para>
    /// The default site is excluded explicitly, mirroring <see cref="Services.SandboxService.Gate"/>:
    /// a row hand-edited in SQL to flag the main site as a sandbox must not start diverting its
    /// operator's installs into a per-site scope either. The redundancy is deliberate.
    /// </para>
    /// </summary>
    public static int? OwnerFor(Site? site) =>
        site is { IsSandbox: true, IsDefault: false } ? site.Id : null;
}
