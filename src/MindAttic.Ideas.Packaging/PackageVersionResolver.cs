namespace MindAttic.Ideas.Packaging;

/// <summary>A snapshot of one installed package version, as the resolver needs to see it.</summary>
public readonly record struct InstalledRef(string Category, string Key, int Version, bool Enabled, bool IsActiveVersion);

/// <summary>
/// The pure version/collision logic behind a package install. No DB, no IO — the host loads the installed
/// rows, hands them in, and applies the returned <see cref="InstallPlan"/>. Whole-number versioning is
/// enforced here: versions only ever move forward.
/// </summary>
public static class PackageVersionResolver
{
    /// <summary>The highest ENABLED version of a (category,key), or null if none is installed/enabled.</summary>
    public static int? ResolveLatest(string category, string key, IEnumerable<InstalledRef> installed)
    {
        int? max = null;
        foreach (var r in installed)
            if (Same(r, category, key) && r.Enabled && (max is null || r.Version > max))
                max = r.Version;
        return max;
    }

    public static InstallPlan Plan(
        IdeaManifest candidate,
        IReadOnlyList<InstalledRef> installed,
        bool compiledKeyExists,
        bool allowOverride)
    {
        var category = candidate.Category;
        var key = candidate.Key;
        var version = candidate.Version;

        // Already installed at this exact version → nothing to do (idempotent re-install).
        if (installed.Any(r => Same(r, category, key) && r.Version == version))
            return new InstallPlan(InstallAction.NoOpAlreadyInstalled,
                $"{category}/{key} v{version} is already installed.", MakeActiveVersion: false, []);

        // Whole-number versions move forward only.
        var maxInstalled = installed.Where(r => Same(r, category, key)).Select(r => (int?)r.Version).DefaultIfEmpty(null).Max();
        if (maxInstalled is int cur && version < cur)
            return new InstallPlan(InstallAction.RejectDowngrade,
                $"version {version} is below the installed {cur}; versions only move forward.", MakeActiveVersion: false, []);

        // A compiled citizen owns this key: only a package that explicitly requests an override may shadow it.
        if (compiledKeyExists)
        {
            if (!allowOverride)
                return new InstallPlan(InstallAction.Blocked,
                    $"a compiled citizen owns '{key}'; install with override to shadow it.", MakeActiveVersion: false, []);
            return new InstallPlan(InstallAction.RequiresOverrideConfirmation,
                $"a compiled citizen owns '{key}'; override confirmed.",
                MakeActiveVersion: IsNewHighest(version, maxInstalled),
                DeactivatePriorVersions: PriorActive(category, key, version, installed));
        }

        return new InstallPlan(InstallAction.Install, null,
            MakeActiveVersion: IsNewHighest(version, maxInstalled),
            DeactivatePriorVersions: PriorActive(category, key, version, installed));
    }

    private static bool Same(InstalledRef r, string category, string key) =>
        string.Equals(r.Category, category, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(r.Key, key, StringComparison.Ordinal);

    private static bool IsNewHighest(int version, int? maxInstalled) => maxInstalled is null || version > maxInstalled;

    // The currently-active prior versions of this key, to flip inactive (retained for history, never removed).
    private static IReadOnlyList<(string, string, int)> PriorActive(
        string category, string key, int version, IReadOnlyList<InstalledRef> installed) =>
        installed
            .Where(r => Same(r, category, key) && r.IsActiveVersion && r.Version != version)
            .Select(r => (r.Category, r.Key, r.Version))
            .ToList();
}
