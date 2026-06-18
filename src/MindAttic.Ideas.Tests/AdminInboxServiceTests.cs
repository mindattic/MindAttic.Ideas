using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Services;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// AdminInboxService upsert-by-DedupKey behavior over an in-memory CmsDbContext: repeats collapse to one
/// row, Resolve flips status, a recurrence after Resolve reopens, and UnreadCount counts only New.
/// (The unique DedupKey DB index is the runtime backstop; InMemory ignores it, so collapse is verified as
/// the service's own lookup-before-insert behavior.)
/// </summary>
[TestFixture]
public class AdminInboxServiceTests
{
    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(dbName).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
    }

    private static AdminInboxService NewService() =>
        new(new InMemoryFactory("inbox_" + Guid.NewGuid().ToString("N")));

    [Test]
    public async Task RaiseAsync_InsertsNewRow()
    {
        var svc = NewService();
        await svc.RaiseAsync("Warning", "Render", "Disabled theme", "cyberspace disabled", "render:disabled:theme:cyberspace");

        var all = await svc.ListAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo("New"));
        Assert.That(all[0].Severity, Is.EqualTo("Warning"));
    }

    [Test]
    public async Task RaiseAsync_SameDedupKey_CollapsesToOneRow()
    {
        var svc = NewService();
        await svc.RaiseAsync("Error", "Render", "Missing", "x", "render:missing:component:tooltip");
        await svc.RaiseAsync("Error", "Render", "Missing", "x again", "render:missing:component:tooltip");
        await svc.RaiseAsync("Error", "Render", "Missing", "x third", "render:missing:component:tooltip");

        Assert.That(await svc.ListAsync(), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ResolveAsync_SetsStatusAndTimestamp()
    {
        var svc = NewService();
        await svc.RaiseAsync("Info", "System", "s", "b", "k1");
        var id = (await svc.ListAsync())[0].Id;

        Assert.That(await svc.ResolveAsync(id), Is.True);
        var row = (await svc.ListAsync())[0];
        Assert.That(row.Status, Is.EqualTo("Resolved"));
        Assert.That(row.ResolvedUtc, Is.Not.Null);
    }

    [Test]
    public async Task RaiseAsync_AfterResolve_ReopensToNew()
    {
        var svc = NewService();
        await svc.RaiseAsync("Warning", "Render", "s", "b", "k2");
        var id = (await svc.ListAsync())[0].Id;
        await svc.ResolveAsync(id);

        await svc.RaiseAsync("Warning", "Render", "s", "recurred", "k2");

        var rows = await svc.ListAsync();
        Assert.That(rows, Has.Count.EqualTo(1));         // still one row (reopened, not duplicated)
        Assert.That(rows[0].Status, Is.EqualTo("New"));
        Assert.That(rows[0].ResolvedUtc, Is.Null);
    }

    [Test]
    public async Task UnreadCount_CountsOnlyNew()
    {
        var svc = NewService();
        await svc.RaiseAsync("Info", "System", "a", "b", "ka");
        await svc.RaiseAsync("Info", "System", "c", "d", "kb");
        await svc.ResolveAsync((await svc.ListAsync("New"))[0].Id);

        Assert.That(await svc.UnreadCountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task RaiseAsync_SameDedupKey_StillNew_RefreshesBody()
    {
        // Regression: when collapsing a repeat of a still-"New" message, Body was never updated, so
        // the first-ever page slug was permanently shown even if the problem moved to a different page.
        var svc = NewService();
        await svc.RaiseAsync("Warning", "Render", "Missing plugin", "first body", "render:missing:plugin:nav");
        await svc.RaiseAsync("Warning", "Render", "Missing plugin", "updated body", "render:missing:plugin:nav");

        var all = await svc.ListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(all, Has.Count.EqualTo(1), "still one row");
            Assert.That(all[0].Body, Is.EqualTo("updated body"), "body must be refreshed on still-New repeats");
        });
    }
}
