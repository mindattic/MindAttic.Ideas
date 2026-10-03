using MindAttic.Ideas.Blazor.Cli;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// <c>--seed from-md</c> reads each project's README from the MindAttic workspace. The paths are relative
/// to that workspace (the parent of this checkout, or <c>MINDATTIC_WORKSPACE</c>), never a hard-coded
/// drive, and UiUx lives inside MindAttic.Web as MindAttic.Web.Shared.
/// </summary>
[TestFixture]
public class SeedReadmesCliTests
{
    [Test]
    public void EveryReadmePathIsRelativeToTheWorkspace()
    {
        Assert.Multiple(() =>
        {
            foreach (var p in SeedReadmesCli.Projects)
            {
                Assert.That(Path.IsPathRooted(p.RelativePath), Is.False, $"{p.Slug}: {p.RelativePath} is absolute");
                Assert.That(p.ReadmePath, Does.StartWith(SeedReadmesCli.WorkspaceRoot), p.Slug);
            }
        });
    }

    [Test]
    public void TheWorkspaceIsTheParentOfThisCheckout()
    {
        var root = SeedReadmesCli.ResolveWorkspaceRoot();
        if (Environment.GetEnvironmentVariable("MINDATTIC_WORKSPACE") is { Length: > 0 })
            Assert.Ignore("MINDATTIC_WORKSPACE overrides the discovered root.");

        var checkout = Directory.EnumerateDirectories(root)
            .Any(d => File.Exists(Path.Combine(d, "MindAttic.Ideas.slnx")));
        Assert.That(checkout, Is.True, $"{root} does not directly contain this checkout");
    }

    [Test]
    public void UiUxReadsTheWebSharedReadme()
    {
        var uiux = SeedReadmesCli.Projects.Single(p => p.Slug == "uiux");

        Assert.That(uiux.RelativePath, Is.EqualTo("MindAttic.Web/MindAttic.Web.Shared/README.md"));
    }
}
