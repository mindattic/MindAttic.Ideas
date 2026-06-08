using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;

namespace MindAttic.Ideas.Tests;

[TestFixture]
public class PackageRegistryServiceTests
{
    private sealed class InMemoryFactory(string db) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(db).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
        public Task<CmsDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
    }

    private static IDbContextFactory<CmsDbContext> NewFactory() =>
        new InMemoryFactory("pkgreg_" + Guid.NewGuid().ToString("N"));

    private static InstalledPackage Pkg(string category, string key, int version, bool active = true, bool enabled = true) =>
        new()
        {
            Category = category, Key = key, Version = version,
            DisplayName = $"{key} V{version}",
            BlobPath = $"{category}/{key}/{version}.idea",
            Sha256 = new string('a', 64),
            IsActiveVersion = active, Enabled = enabled,
            InstalledUtc = DateTime.UtcNow,
        };

    [Test]
    public async Task ListAsync_ReturnsAllPackages_SortedByCategoryKeyVersionDesc()
    {
        var factory = NewFactory();
        await using (var db = factory.CreateDbContext())
        {
            db.InstalledPackages.AddRange(
                Pkg("Widget", "gallery", 1),
                Pkg("Theme",  "dark",    2),
                Pkg("Theme",  "dark",    1),
                Pkg("Widget", "tooltip", 1));
            await db.SaveChangesAsync();
        }

        var result = await new PackageRegistryService(factory).ListAsync();

        Assert.That(result.Select(p => (p.Category, p.Key, p.Version)), Is.EqualTo(new[]
        {
            ("Theme",  "dark",    2),
            ("Theme",  "dark",    1),
            ("Widget", "gallery", 1),
            ("Widget", "tooltip", 1),
        }));
    }

    [Test]
    public async Task ListAsync_Empty_ReturnsEmptyList()
    {
        var result = await new PackageRegistryService(NewFactory()).ListAsync();
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task ListAsync_MapsAllFields()
    {
        var factory = NewFactory();
        var installed = DateTime.UtcNow.AddHours(-1);
        await using (var db = factory.CreateDbContext())
        {
            db.InstalledPackages.Add(new InstalledPackage
            {
                Category = "Widget", Key = "ui.tooltip", Version = 3,
                DisplayName = "Tooltip", BlobPath = "Widget/ui.tooltip/3.idea",
                Sha256 = new string('f', 64), IsActiveVersion = true, Enabled = false,
                InstalledUtc = installed,
            });
            await db.SaveChangesAsync();
        }

        var p = (await new PackageRegistryService(factory).ListAsync())[0];

        Assert.Multiple(() =>
        {
            Assert.That(p.Category,       Is.EqualTo("Widget"));
            Assert.That(p.Key,            Is.EqualTo("ui.tooltip"));
            Assert.That(p.Version,        Is.EqualTo(3));
            Assert.That(p.DisplayName,    Is.EqualTo("Tooltip"));
            Assert.That(p.BlobPath,       Is.EqualTo("Widget/ui.tooltip/3.idea"));
            Assert.That(p.IsActiveVersion, Is.True);
            Assert.That(p.Enabled,        Is.False);
            Assert.That(p.Sha256,         Is.EqualTo(new string('f', 64)));
        });
    }
}
