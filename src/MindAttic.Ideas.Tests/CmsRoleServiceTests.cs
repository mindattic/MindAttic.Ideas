using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Services;

namespace MindAttic.Ideas.Tests;

[TestFixture]
public class CmsRoleServiceTests
{
    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(dbName).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
        public Task<CmsDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
    }

    private static ICmsRoleService NewService() =>
        new CmsRoleService(new InMemoryFactory("role_" + Guid.NewGuid().ToString("N")));

    [Test]
    public async Task CreateRole_DuplicateNameDifferentCase_ReturnsFriendlyError()
    {
        // Regression: CreateRoleAsync used r.Name == name (case-sensitive ordinal), allowing logically
        // duplicate roles on InMemory / case-sensitive collations. Now uses ToLower() comparison.
        var svc = NewService();
        var first = await svc.CreateRoleAsync("Editor");
        Assert.That(first.Ok, Is.True, "first create must succeed");

        var duplicate = await svc.CreateRoleAsync("editor");
        Assert.Multiple(() =>
        {
            Assert.That(duplicate.Ok, Is.False, "duplicate name (different case) must be rejected");
            Assert.That(duplicate.Error, Does.Contain("already exists"), "friendly error message expected");
        });
    }
}
