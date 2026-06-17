using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Tests;

[TestFixture]
public class WorkflowServiceTests
{
    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(dbName).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
        public Task<CmsDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
    }

    private static IDbContextFactory<CmsDbContext> NewFactory() =>
        new InMemoryFactory("wf_" + Guid.NewGuid().ToString("N"));

    private static ClaimsPrincipal Admin() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "test"));

    private static ClaimsPrincipal Editor() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Editor")], "test"));

    private static ClaimsPrincipal Viewer() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Viewer")], "test"));

    private static async Task<(WorkflowService Svc, int DefId, int PageId)> SetupEditorialWorkflowAsync(
        IDbContextFactory<CmsDbContext> factory)
    {
        var svc = new WorkflowService(factory);

        var def = await svc.CreateDefinitionAsync("Editorial", "Draft", isDefault: true);
        await svc.AddTransitionAsync(def.Id, "Draft", "Review", requiredRole: "Editor", label: "Submit for Review");
        await svc.AddTransitionAsync(def.Id, "Review", "Published", requiredRole: "Admin", label: "Publish");
        await svc.AddTransitionAsync(def.Id, "Published", "Draft", requiredRole: "Admin", label: "Revert to Draft");

        await using var db = factory.CreateDbContext();
        var page = new CmsPage
        {
            Slug = "test-page", Title = "Test", Kind = MindAttic.Ideas.Abstractions.PageKind.Data,
            BodyTrust = MindAttic.Ideas.Abstractions.ContentTrust.Untrusted,
            WorkflowDefinitionId = def.Id, WorkflowState = "Draft",
            IsPublished = false, Enabled = true, CreatedUtc = DateTime.UtcNow,
        };
        db.Pages.Add(page);
        await db.SaveChangesAsync();

        return (svc, def.Id, page.Id);
    }

    [Test]
    public async Task CreateDefinition_Persists_WithInitialStateAndTransitions()
    {
        var factory = NewFactory();
        var svc = new WorkflowService(factory);

        var def = await svc.CreateDefinitionAsync("Default", "Draft");
        await svc.AddTransitionAsync(def.Id, "Draft", "Published", label: "Publish");

        var loaded = await svc.GetDefinitionAsync(def.Id);
        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Name, Is.EqualTo("Default"));
            Assert.That(loaded.InitialState, Is.EqualTo("Draft"));
            Assert.That(loaded.Transitions, Has.Count.EqualTo(1));
            Assert.That(loaded.Transitions.First().ToState, Is.EqualTo("Published"));
        });
    }

    [Test]
    public async Task CreateDefinition_IsDefault_DemotesPreviousDefault()
    {
        var factory = NewFactory();
        var svc = new WorkflowService(factory);

        var first = await svc.CreateDefinitionAsync("First", "Draft", isDefault: true);
        var second = await svc.CreateDefinitionAsync("Second", "Draft", isDefault: true);

        var all = await svc.GetDefinitionsAsync();
        Assert.Multiple(() =>
        {
            Assert.That(all.First(d => d.Id == first.Id).IsDefault, Is.False, "first was demoted");
            Assert.That(all.First(d => d.Id == second.Id).IsDefault, Is.True, "second is default");
        });
    }

    [Test]
    public async Task TransitionPage_ValidTransition_ChangesWorkflowState()
    {
        var factory = NewFactory();
        var (svc, _, pageId) = await SetupEditorialWorkflowAsync(factory);

        var (ok, err) = await svc.TransitionPageAsync(pageId, "Review", Editor());

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True, err);
            Assert.That(err, Is.Null);
        });

        await using var db = factory.CreateDbContext();
        var page = await db.Pages.FindAsync(pageId);
        Assert.That(page!.WorkflowState, Is.EqualTo("Review"));
    }

    [Test]
    public async Task TransitionPage_ToPublished_SetsIsPublishedTrue()
    {
        var factory = NewFactory();
        var (svc, _, pageId) = await SetupEditorialWorkflowAsync(factory);

        await svc.TransitionPageAsync(pageId, "Review", Editor());
        var (ok, _) = await svc.TransitionPageAsync(pageId, "Published", Admin());

        await using var db = factory.CreateDbContext();
        var page = await db.Pages.FindAsync(pageId);
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(page!.IsPublished, Is.True, "Published state syncs IsPublished");
            Assert.That(page.WorkflowState, Is.EqualTo("Published"));
        });
    }

    [Test]
    public async Task TransitionPage_FromPublishedToDraft_SetsIsPublishedFalse()
    {
        var factory = NewFactory();
        var (svc, _, pageId) = await SetupEditorialWorkflowAsync(factory);

        // Get to Published first.
        await svc.TransitionPageAsync(pageId, "Review", Editor());
        await svc.TransitionPageAsync(pageId, "Published", Admin());

        // Revert.
        var (ok, _) = await svc.TransitionPageAsync(pageId, "Draft", Admin());

        await using var db = factory.CreateDbContext();
        var page = await db.Pages.FindAsync(pageId);
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(page!.IsPublished, Is.False, "Draft state clears IsPublished");
        });
    }

    [Test]
    public async Task TransitionPage_MissingTransition_ReturnsError()
    {
        var factory = NewFactory();
        var (svc, _, pageId) = await SetupEditorialWorkflowAsync(factory);

        var (ok, err) = await svc.TransitionPageAsync(pageId, "Published", Editor());

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(err, Does.Contain("not defined in workflow"));
        });
    }

    [Test]
    public async Task TransitionPage_InsufficientRole_ReturnsError()
    {
        var factory = NewFactory();
        var (svc, _, pageId) = await SetupEditorialWorkflowAsync(factory);

        // Viewer cannot submit for Review (requires Editor).
        var (ok, err) = await svc.TransitionPageAsync(pageId, "Review", Viewer());

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(err, Does.Contain("requires the 'Editor' role"));
        });
    }

    [Test]
    public async Task TransitionPage_AdminBypassesRoleGate()
    {
        var factory = NewFactory();
        var (svc, _, pageId) = await SetupEditorialWorkflowAsync(factory);

        // Admin can do any transition even if RequiredRole is "Editor".
        var (ok, err) = await svc.TransitionPageAsync(pageId, "Review", Admin());
        Assert.That(ok, Is.True, err);
    }

    [Test]
    public async Task AssignWorkflow_SetsWorkflowAndInitialState()
    {
        var factory = NewFactory();
        var svc = new WorkflowService(factory);
        var def = await svc.CreateDefinitionAsync("Simple", "Draft");

        await using var setupDb = factory.CreateDbContext();
        var page = new CmsPage
        {
            Slug = "my-page", Title = "My Page",
            Kind = MindAttic.Ideas.Abstractions.PageKind.Data,
            BodyTrust = MindAttic.Ideas.Abstractions.ContentTrust.Untrusted,
            Enabled = true, CreatedUtc = DateTime.UtcNow,
        };
        setupDb.Pages.Add(page);
        await setupDb.SaveChangesAsync();

        var ok = await svc.AssignWorkflowAsync(page.Id, def.Id);

        await using var db = factory.CreateDbContext();
        var updated = await db.Pages.FindAsync(page.Id);
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(updated!.WorkflowDefinitionId, Is.EqualTo(def.Id));
            Assert.That(updated.WorkflowState, Is.EqualTo("Draft"));
        });
    }

    [Test]
    public async Task AssignWorkflow_ResetsStateEvenWhenPageAlreadyHasState()
    {
        // Regression: ??= left old WorkflowState intact when reassigning a different workflow.
        var factory = NewFactory();
        var svc = new WorkflowService(factory);

        var defA = await svc.CreateDefinitionAsync("DefA", "Draft");
        var defB = await svc.CreateDefinitionAsync("DefB", "Review");

        await using var setupDb = factory.CreateDbContext();
        var page = new CmsPage
        {
            Slug = "reassign-page", Title = "Reassign Test",
            Kind = MindAttic.Ideas.Abstractions.PageKind.Data,
            BodyTrust = MindAttic.Ideas.Abstractions.ContentTrust.Untrusted,
            Enabled = true, CreatedUtc = DateTime.UtcNow,
            WorkflowDefinitionId = defA.Id, WorkflowState = "Published",
        };
        setupDb.Pages.Add(page);
        await setupDb.SaveChangesAsync();

        await svc.AssignWorkflowAsync(page.Id, defB.Id);

        await using var db = factory.CreateDbContext();
        var updated = await db.Pages.FindAsync(page.Id);
        Assert.That(updated!.WorkflowState, Is.EqualTo("Review"),
            "AssignWorkflow must reset state to InitialState of the new definition");
    }
}
