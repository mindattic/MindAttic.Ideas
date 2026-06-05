using System.Reflection;
using System.Runtime.Loader;
using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Core.Discovery;

/// <summary>
/// The pure deferral decision behind <see cref="CmsPackageLoadContext"/> — the ALC unification linchpin
/// (ADR Appendix E). A name is DEFERRED to the host's Default context (so the package's base types unify by
/// reference identity with the host's, and casts succeed) when it is a host/framework assembly OR is already
/// loaded in Default; otherwise it is a private dependency the package loads itself. Pure → unit-testable.
/// </summary>
public static class AlcDeferralPolicy
{
    public static bool ShouldDefer(string? simpleName, Func<string, bool> isLoadedInDefault)
    {
        if (string.IsNullOrEmpty(simpleName)) return true;            // nameless -> let Default decide
        return ManifestValidator.IsHostAssemblyName(simpleName) || isLoadedInDefault(simpleName);
    }
}

/// <summary>
/// A per-package collectible <see cref="AssemblyLoadContext"/> that loads a runtime <c>.idea</c>'s private
/// assemblies while DEFERRING host/framework names to the Default context (see <see cref="AlcDeferralPolicy"/>).
/// This is the load primitive only — it is NOT wired as the live <c>ITypeResolver</c> yet, and true
/// <see cref="AssemblyLoadContext.Unload"/> is deliberately never called (uninstall stays soft-disable +
/// effective-on-restart, so a loaded type can't pin a half-torn-down context). Blob extraction and the
/// ALC-aware resolver that consumes this are the attended Phase-5/B follow-up.
/// </summary>
public sealed class CmsPackageLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public CmsPackageLoadContext(string entryAssemblyPath)
        : base(name: "ma-idea:" + Path.GetFileNameWithoutExtension(entryAssemblyPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(entryAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Defer host/framework + already-loaded names to Default (return null) so they resolve to ONE identity.
        if (AlcDeferralPolicy.ShouldDefer(assemblyName.Name, IsLoadedInDefault))
            return null;

        // Otherwise this is a private package dependency — load it into THIS context.
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    private static bool IsLoadedInDefault(string simpleName) =>
        Default.Assemblies.Any(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
}
