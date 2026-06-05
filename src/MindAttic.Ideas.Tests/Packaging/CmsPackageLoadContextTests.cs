using System.Reflection;
using System.Runtime.Loader;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Discovery;

namespace MindAttic.Ideas.Tests.Packaging;

/// <summary>
/// The ALC unification linchpin: a per-package collectible context defers host/framework names to Default
/// (so casts to host base types succeed) while loading private deps itself. Pure policy + real load behavior.
/// Never calls Unload (matches the soft, effective-on-restart model).
/// </summary>
[TestFixture]
public class CmsPackageLoadContextTests
{
    // ---- pure deferral policy ----

    [Test]
    public void ShouldDefer_HostAndFrameworkNames_True()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AlcDeferralPolicy.ShouldDefer("MindAttic.Ideas.Abstractions", _ => false), Is.True);
            Assert.That(AlcDeferralPolicy.ShouldDefer("MindAttic.Ideas.Core", _ => false), Is.True);
            Assert.That(AlcDeferralPolicy.ShouldDefer("System.Text.Json", _ => false), Is.True);
            Assert.That(AlcDeferralPolicy.ShouldDefer("Microsoft.EntityFrameworkCore", _ => false), Is.True);  // bare name
            Assert.That(AlcDeferralPolicy.ShouldDefer(null, _ => false), Is.True);
        });
    }

    [Test]
    public void ShouldDefer_PrivateName_FalseUnlessAlreadyLoadedInDefault()
    {
        Assert.That(AlcDeferralPolicy.ShouldDefer("Markdig", _ => false), Is.False);
        Assert.That(AlcDeferralPolicy.ShouldDefer("Markdig", n => n == "Markdig"), Is.True);   // dup in Default -> defer
    }

    // ---- real load behavior ----

    private static string PrivateDllPath()
    {
        // AngleSharp ships beside the test (transitive via Core) and is NOT a host-deferred name.
        var path = Path.Combine(AppContext.BaseDirectory, "AngleSharp.dll");
        if (!File.Exists(path)) Assert.Ignore("AngleSharp.dll not present beside the test assembly.");
        return path;
    }

    [Test]
    public void DeferredHostName_ResolvesToTheDefaultContextAssembly()
    {
        var ctx = new CmsPackageLoadContext(PrivateDllPath());
        var hostAbstractions = typeof(SharedContracts).Assembly;

        var resolved = ctx.LoadFromAssemblyName(new AssemblyName(hostAbstractions.GetName().Name!));

        Assert.That(resolved, Is.SameAs(hostAbstractions), "a deferred name must unify with the host's single identity");
    }

    [Test]
    public void PrivateAssembly_LoadsIntoThisContext_NotDefault()
    {
        var ctx = new CmsPackageLoadContext(PrivateDllPath());

        var asm = ctx.LoadFromAssemblyPath(PrivateDllPath());

        Assert.Multiple(() =>
        {
            Assert.That(asm.GetName().Name, Is.EqualTo("AngleSharp"));
            Assert.That(AssemblyLoadContext.GetLoadContext(asm), Is.SameAs(ctx), "a private dep loads into the package context");
            Assert.That(AssemblyLoadContext.GetLoadContext(asm), Is.Not.SameAs(AssemblyLoadContext.Default));
        });
    }
}
