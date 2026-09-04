using System.Text.RegularExpressions;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// MAI-A32. A GitHub runner has no <c>C:\LocalNuGet</c> and no <c>../local-feed</c>, and NuGet
/// tolerates a missing local source <i>silently</i> — so a MindAttic package that was bumped in a
/// csproj but never vendored into <c>lib/local-packages/</c> does not fail here, it fails in CI with
/// a confusing NU1101 about a package that plainly exists on the dev box. This fixture closes that
/// gap: it is the only thing standing between a one-line version bump and a red deploy.
/// </summary>
[TestFixture]
public class DeploymentPackagingTests
{
    private static readonly Regex MindAtticPackageRef = new(
        @"<PackageReference\s+Include=""(?<id>MindAttic\.[^""]+)""\s+Version=""(?<version>[^""]+)""",
        RegexOptions.Compiled);

    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MindAttic.Ideas.slnx")))
            dir = dir.Parent;

        Assert.That(dir, Is.Not.Null, "Could not locate the repo root (MindAttic.Ideas.slnx).");
        return dir!;
    }

    private static IEnumerable<(string Project, string Id, string Version)> ReferencedMindAtticPackages()
    {
        var srcDir = Path.Combine(RepoRoot().FullName, "src");
        foreach (var csproj in Directory.EnumerateFiles(srcDir, "*.csproj", SearchOption.AllDirectories))
        {
            foreach (Match m in MindAtticPackageRef.Matches(File.ReadAllText(csproj)))
            {
                yield return (Path.GetFileName(csproj), m.Groups["id"].Value, m.Groups["version"].Value);
            }
        }
    }

    [Test]
    public void EveryReferencedMindAtticPackageIsVendoredForCi()
    {
        var vendorDir = Path.Combine(RepoRoot().FullName, "lib", "local-packages");
        Assert.That(Directory.Exists(vendorDir), Is.True, $"Missing vendored feed at {vendorDir}.");

        var referenced = ReferencedMindAtticPackages().ToList();
        Assert.That(referenced, Is.Not.Empty, "Expected at least one MindAttic PackageReference.");

        var missing = referenced
            .Where(r => !File.Exists(Path.Combine(vendorDir, $"{r.Id}.{r.Version}.nupkg")))
            .Select(r => $"{r.Id} {r.Version} (referenced by {r.Project})")
            .Distinct()
            .ToList();

        Assert.That(missing, Is.Empty,
            "These packages are referenced but not vendored, so a CI restore will fail. Copy the "
            + $".nupkg into lib/local-packages/:{Environment.NewLine}  " + string.Join(Environment.NewLine + "  ", missing));
    }

    [Test]
    public void NugetConfigListsTheVendoredFeed()
    {
        var config = File.ReadAllText(Path.Combine(RepoRoot().FullName, "nuget.config"));

        Assert.That(config, Does.Contain("./lib/local-packages"),
            "nuget.config must list the vendored feed, or CI has no source for the MindAttic packages.");
    }

    [Test]
    public void VendoredPackagesAreTrackedRatherThanGitIgnored()
    {
        var gitignore = File.ReadAllText(Path.Combine(RepoRoot().FullName, ".gitignore"));

        Assert.That(gitignore, Does.Contain("!lib/local-packages/*.nupkg"),
            ".gitignore excludes *.nupkg globally; the vendored feed must be re-included or it never "
            + "reaches the runner.");
    }

    [Test]
    public void DeployWorkflowPointsAtProjectsThatExist()
    {
        var root = RepoRoot().FullName;
        var workflowPath = Path.Combine(root, ".github", "workflows", "azure-deploy.yml");
        Assert.That(File.Exists(workflowPath), Is.True, "Missing .github/workflows/azure-deploy.yml.");

        var workflow = File.ReadAllText(workflowPath);

        // Paths the workflow hands to dotnet. A rename that misses the workflow is a red deploy.
        string[] mustExist =
        [
            "src/MindAttic.Ideas.Blazor/MindAttic.Ideas.Blazor.csproj",
            "src/MindAttic.Ideas.Tests/MindAttic.Ideas.Tests.csproj",
            "src/MindAttic.Ideas.Core",
            "MindAttic.Ideas.slnx",
        ];

        Assert.Multiple(() =>
        {
            foreach (var relative in mustExist)
            {
                Assert.That(workflow, Does.Contain(relative), $"Workflow no longer references {relative}.");
                var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.Exists(full) || Directory.Exists(full), Is.True,
                    $"Workflow references {relative}, which does not exist.");
            }
        });
    }

    /// <summary>
    /// Floors for the three packages bumped to clear security advisories (MAI-A32). Pinned versions
    /// are easy to revert by accident during a merge, and a downgrade silently reintroduces the
    /// advisory — nothing else in the build would notice.
    /// </summary>
    [TestCase("AngleSharp", "1.7.2", "GHSA-pgww-w46g-26qg")]
    [TestCase("HtmlSanitizer", "9.2.1039", "requires the patched AngleSharp line")]
    [TestCase("System.Security.Cryptography.Xml", "10.0.11", "five HIGH advisories against 10.0.8")]
    public void SecurityPinnedPackagesAreNotDowngraded(string id, string minimum, string why)
    {
        var pattern = new Regex(
            $@"<PackageReference\s+Include=""{Regex.Escape(id)}""\s+Version=""(?<version>[^""]+)""",
            RegexOptions.Compiled);

        var srcDir = Path.Combine(RepoRoot().FullName, "src");
        var found = new List<(string Project, Version Version)>();

        foreach (var csproj in Directory.EnumerateFiles(srcDir, "*.csproj", SearchOption.AllDirectories))
        {
            foreach (Match m in pattern.Matches(File.ReadAllText(csproj)))
            {
                if (Version.TryParse(m.Groups["version"].Value, out var parsed))
                    found.Add((Path.GetFileName(csproj), parsed));
            }
        }

        Assert.That(found, Is.Not.Empty, $"Expected a pinned PackageReference for {id}.");

        var floor = Version.Parse(minimum);
        foreach (var (project, version) in found)
        {
            Assert.That(version, Is.GreaterThanOrEqualTo(floor),
                $"{project} pins {id} {version}, below the {minimum} floor ({why}).");
        }
    }

    [Test]
    public void ProductionRequiresItsDataProtectionSettingsByName()
    {
        var program = File.ReadAllText(Path.Combine(
            RepoRoot().FullName, "src", "MindAttic.Ideas.Blazor", "Program.cs"));

        Assert.Multiple(() =>
        {
            // The app fail-closes in production without these. docs/DEPLOYMENT.md documents both by
            // name; if a rename lands here the runbook silently becomes wrong.
            Assert.That(program, Does.Contain("DataProtection:BlobUri"));
            Assert.That(program, Does.Contain("DataProtection:KeyVaultKeyId"));
            // App Service health-checks this path (infra/main.bicep healthCheckPath).
            Assert.That(program, Does.Contain("/_health"));
        });
    }
}
