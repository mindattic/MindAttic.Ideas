using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Tests;

[TestFixture]
public class WidgetInstanceSettingsServiceTests
{
    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(dbName).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
        public Task<CmsDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
    }

    private static IDbContextFactory<CmsDbContext> NewFactory() =>
        new InMemoryFactory("wis_" + Guid.NewGuid().ToString("N"));

    private static async Task<int> SeedPageAsync(IDbContextFactory<CmsDbContext> factory)
    {
        await using var db = factory.CreateDbContext();
        var page = new CmsPage
        {
            Slug = "hero-page", Title = "Hero",
            Kind = MindAttic.Ideas.Abstractions.PageKind.Data,
            BodyTrust = MindAttic.Ideas.Abstractions.ContentTrust.Untrusted,
            Enabled = true, CreatedUtc = DateTime.UtcNow,
        };
        db.Pages.Add(page);
        await db.SaveChangesAsync();
        return page.Id;
    }

    [Test]
    public async Task Save_Create_PersistsVersionOne()
    {
        var factory = NewFactory();
        var svc = new WidgetInstanceSettingsService(factory);
        var pageId = await SeedPageAsync(factory);

        var result = await svc.SaveAsync(pageId, "hero", "Plugin.hero@1", """{"color":"blue"}""", "admin");

        Assert.Multiple(() =>
        {
            Assert.That(result.SettingsVersion, Is.EqualTo(1));
            Assert.That(result.SettingsJson, Does.Contain("blue"));
            Assert.That(result.SlotName, Is.EqualTo("hero"));
        });
    }

    [Test]
    public async Task Save_Update_BumpsVersionAndWritesHistory()
    {
        var factory = NewFactory();
        var svc = new WidgetInstanceSettingsService(factory);
        var pageId = await SeedPageAsync(factory);

        await svc.SaveAsync(pageId, "hero", "Plugin.hero@1", """{"color":"blue"}""");
        var v2 = await svc.SaveAsync(pageId, "hero", "Plugin.hero@1", """{"color":"red"}""", "editor");

        Assert.That(v2.SettingsVersion, Is.EqualTo(2));

        var history = await svc.GetHistoryAsync(pageId, "hero");
        Assert.That(history, Has.Count.EqualTo(1), "one history snapshot (the V1 pre-save state)");
        Assert.That(history[0].SettingsVersion, Is.EqualTo(1));
        Assert.That(history[0].SettingsJson, Does.Contain("blue"));
    }

    [Test]
    public async Task Save_MultipleUpdates_AccumulatesHistory()
    {
        var factory = NewFactory();
        var svc = new WidgetInstanceSettingsService(factory);
        var pageId = await SeedPageAsync(factory);

        await svc.SaveAsync(pageId, "hero", "Plugin.hero@1", """{"v":1}""");
        await svc.SaveAsync(pageId, "hero", "Plugin.hero@1", """{"v":2}""");
        await svc.SaveAsync(pageId, "hero", "Plugin.hero@1", """{"v":3}""");

        var current = await svc.GetAsync(pageId, "hero");
        var history = await svc.GetHistoryAsync(pageId, "hero");

        Assert.Multiple(() =>
        {
            Assert.That(current!.SettingsVersion, Is.EqualTo(3));
            Assert.That(history, Has.Count.EqualTo(2), "two snapshots for V1 and V2");
        });
    }

    [Test]
    public async Task Rollback_RestoresPreviousSettingsAndBumpsVersion()
    {
        var factory = NewFactory();
        var svc = new WidgetInstanceSettingsService(factory);
        var pageId = await SeedPageAsync(factory);

        await svc.SaveAsync(pageId, "hero", "Plugin.hero@1", """{"color":"blue"}""");   // V1
        await svc.SaveAsync(pageId, "hero", "Plugin.hero@1", """{"color":"red"}""");    // V2

        var ok = await svc.RollbackAsync(pageId, "hero", version: 1);

        var current = await svc.GetAsync(pageId, "hero");
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(current!.SettingsJson, Does.Contain("blue"), "rolled back to V1 content");
            Assert.That(current.SettingsVersion, Is.EqualTo(3), "version always increases");
        });
    }

    [Test]
    public async Task Rollback_UnknownVersion_ReturnsFalse()
    {
        var factory = NewFactory();
        var svc = new WidgetInstanceSettingsService(factory);
        var pageId = await SeedPageAsync(factory);
        await svc.SaveAsync(pageId, "hero", "Plugin.hero@1", "{}");

        var ok = await svc.RollbackAsync(pageId, "hero", version: 99);

        Assert.That(ok, Is.False);
    }

    [Test]
    public async Task GetAsync_UnknownSlot_ReturnsNull()
    {
        var factory = NewFactory();
        var svc = new WidgetInstanceSettingsService(factory);
        var pageId = await SeedPageAsync(factory);

        var result = await svc.GetAsync(pageId, "nonexistent");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetHistoryAsync_UnknownSlot_ReturnsEmpty()
    {
        var factory = NewFactory();
        var svc = new WidgetInstanceSettingsService(factory);
        var pageId = await SeedPageAsync(factory);

        var history = await svc.GetHistoryAsync(pageId, "ghost");

        Assert.That(history, Is.Empty);
    }
}
