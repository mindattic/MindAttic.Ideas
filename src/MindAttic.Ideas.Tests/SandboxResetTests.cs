using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Blazor.Cli;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;
using MindAttic.Media;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// MAI-US-M5: the showroom returns to Day Zero once nobody is using it.
/// <para>
/// This is the routine that deletes a site's content on a timer, so the tests that matter most are the
/// ones about what it must NOT do: never the default site, never another site's content, never the
/// shared library, and never the sandbox's own identity — a baseline exported from the real site would
/// otherwise rename the sandbox and overwrite the very flags that make it one.
/// </para>
/// </summary>
[TestFixture]
public class SandboxResetTests
{
    // ---- harness ---------------------------------------------------------------------------

    private sealed class FakeMediaStore : IMediaStore
    {
        public readonly List<MediaItem> Items = [];

        public Task<MediaItem> UploadAsync(Stream content, string fileName, string contentType,
            int? tenantId = null, string folder = "", string mediaType = "", int? width = null,
            int? height = null, string? notes = null, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            var bytes = ms.ToArray();
            var item = new MediaItem
            {
                Uid = Guid.NewGuid(), FileName = fileName, ContentType = contentType, Folder = folder,
                MediaType = mediaType, SizeBytes = bytes.Length, Bytes = bytes,
                Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
            };
            Items.Add(item);
            return Task.FromResult(item);
        }

        public Task<IReadOnlyList<MediaItem>> ListAsync(int? tenantId = null, string? folder = null,
            string? mediaType = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MediaItem>>(Items.ToList());

        public Task<MediaItem?> GetMetaAsync(Guid uid, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(i => i.Uid == uid));

        public Task<(MediaItem Meta, Stream Content)?> GetAsync(Guid uid, CancellationToken ct = default)
        {
            var m = Items.FirstOrDefault(i => i.Uid == uid);
            return Task.FromResult(m is null ? null : ((MediaItem, Stream)?)(m, new MemoryStream(m.Bytes!)));
        }

        public Task<bool> DeleteAsync(Guid uid, CancellationToken ct = default)
            => Task.FromResult(Items.RemoveAll(i => i.Uid == uid) > 0);
    }

    private sealed class InMemoryFactory(string dbName) : IDbContextFactory<CmsDbContext>
    {
        private readonly DbContextOptions<CmsDbContext> _opts =
            new DbContextOptionsBuilder<CmsDbContext>().UseInMemoryDatabase(dbName).Options;
        public CmsDbContext CreateDbContext() => new(_opts);
        public Task<CmsDbContext> CreateDbContextAsync(CancellationToken ct = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class NullResolver : ITypeResolver
    {
        public Type? Resolve(ContentDescriptor descriptor) => null;
    }

    /// <summary>A baseline held in memory, so a test never needs a file on disk.</summary>
    private sealed class BytesBaseline(byte[]? bytes) : ISandboxBaselineSource
    {
        public Task<Stream?> OpenAsync(Site site, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(bytes is null ? null : new MemoryStream(bytes));
    }

    private string _dir = "";

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "sbreset_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private sealed class Env
    {
        public required InMemoryFactory Factory { get; init; }
        public required FakeMediaStore Store { get; init; }
        public required IServiceProvider Services { get; init; }
        public CmsDbContext Db() => Factory.CreateDbContext();
    }

    private static Env NewEnv()
    {
        var factory = new InMemoryFactory("sbreset_" + Guid.NewGuid().ToString("N"));
        var store = new FakeMediaStore();
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<CmsDbContext>>(factory);
        services.AddScoped(_ => factory.CreateDbContext());
        services.AddSingleton<IMediaStore>(store);
        return new Env { Factory = factory, Store = store, Services = services.BuildServiceProvider() };
    }

    private static SandboxResetService Reset(Env env, byte[]? baseline)
    {
        var db = env.Db();
        var catalog = new ContentCatalog(new NullResolver());
        var discovery = new DiscoveryService(env.Factory, Array.Empty<ICmsContentSource>(), catalog);
        return new SandboxResetService(db, new SandboxService(db), new BytesBaseline(baseline), env.Store, discovery);
    }

    private static async Task<(int MainId, int SandboxId)> SeedSitesAsync(Env env, int graceMinutes = 10)
    {
        await using var db = env.Db();
        var main = new Site { Key = "default", Name = "MindAttic", IsDefault = true, CreatedUtc = DateTime.UtcNow };
        var sandbox = new Site
        {
            Key = "showroom", Name = "Showroom", HostBindings = "showroom.example.com",
            IsSandbox = true, ResetPolicy = SandboxService.WhenIdle, IdleGraceMinutes = graceMinutes,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Sites.AddRange(main, sandbox);
        await db.SaveChangesAsync();
        return (main.Id, sandbox.Id);
    }

    private static async Task<CmsPage> AddPageAsync(Env env, int siteId, string slug, string body = "<p>x</p>")
    {
        await using var db = env.Db();
        var page = new CmsPage
        {
            Uid = Guid.NewGuid(), SiteId = siteId, Slug = slug, Title = slug, Kind = PageKind.Data,
            BodyHtml = body, BodyTrust = ContentTrust.Author, AuthorTrustVersion = 1,
            IsPublished = true, Enabled = true, CreatedUtc = DateTime.UtcNow,
        };
        db.Pages.Add(page);
        await db.SaveChangesAsync();
        return page;
    }

    /// <summary>Exports the given env to a bundle and returns its bytes — a real baseline, not a fixture.</summary>
    private async Task<byte[]> BaselineFromAsync(Env env)
    {
        var path = Path.Combine(_dir, "baseline_" + Guid.NewGuid().ToString("N") + ".ideabundle");
        Assert.That(await ExportContentCli.RunAsync(["--export-content", path], env.Services), Is.Zero,
            "the baseline must export cleanly, or the test is measuring the wrong thing");
        return await File.ReadAllBytesAsync(path);
    }

    // ---- the ones that must never fail -----------------------------------------------------

    [Test]
    public async Task TheDefaultSiteIsRefused_AndNothingOfItIsTouched()
    {
        var env = NewEnv();
        var (mainId, _) = await SeedSitesAsync(env);
        await AddPageAsync(env, mainId, "frontpage");

        var outcome = await Reset(env, null).ResetAsync(mainId, DateTime.UtcNow);

        await using var db = env.Db();
        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Ok, Is.False);
            Assert.That(outcome.Refusal, Is.EqualTo(SandboxRefusal.IsDefaultSite));
            Assert.That(await db.Pages.CountAsync(), Is.EqualTo(1), "the refusal must happen BEFORE any delete");
            Assert.That((await db.Sites.SingleAsync(s => s.Id == mainId)).LastResetUtc, Is.Null);
        });
    }

    [Test]
    public async Task ASiteFlaggedSandboxButAlsoDefaultIsStillRefused()
    {
        // The worst case, again at the executor: a row hand-edited in SQL past every write-time check.
        var env = NewEnv();
        var (mainId, _) = await SeedSitesAsync(env);
        await using (var db = env.Db())
        {
            var main = await db.Sites.SingleAsync(s => s.Id == mainId);
            main.IsSandbox = true;
            main.ResetPolicy = SandboxService.WhenIdle;
            await db.SaveChangesAsync();
        }
        await AddPageAsync(env, mainId, "frontpage");

        var outcome = await Reset(env, null).ResetAsync(mainId, DateTime.UtcNow);

        await using var read = env.Db();
        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Refusal, Is.EqualTo(SandboxRefusal.IsDefaultSite));
            Assert.That(await read.Pages.CountAsync(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ASandboxWithNoResetPolicyIsRefused()
    {
        var env = NewEnv();
        var (_, sandboxId) = await SeedSitesAsync(env);
        await using (var db = env.Db())
        {
            var s = await db.Sites.SingleAsync(x => x.Id == sandboxId);
            s.ResetPolicy = null;   // a sandbox nobody asked to be wiped
            await db.SaveChangesAsync();
        }
        await AddPageAsync(env, sandboxId, "visitor-page");

        var outcome = await Reset(env, null).ResetAsync(sandboxId, DateTime.UtcNow);

        await using var read = env.Db();
        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Refusal, Is.EqualTo(SandboxRefusal.NoResetPolicy));
            Assert.That(await read.Pages.CountAsync(), Is.EqualTo(1));
        });
    }

    // ---- what a reset actually does --------------------------------------------------------

    [Test]
    public async Task AResetDropsOnlyTheSandboxsOwnContent()
    {
        var env = NewEnv();
        var (mainId, sandboxId) = await SeedSitesAsync(env);
        var mainPage = await AddPageAsync(env, mainId, "frontpage");
        await AddPageAsync(env, sandboxId, "visitor-page");

        await using (var db = env.Db())
        {
            // A visitor's own install, and the shared library it sits beside.
            db.ContentDefinitions.AddRange(
                new CmsContentDefinition
                {
                    Kind = ContentKind.Component, Key = "visitorthing", Version = 1,
                    Origin = ContentOrigin.Package, Category = "Component", SiteId = sandboxId,
                    IsActive = true, Enabled = true,
                },
                new CmsContentDefinition
                {
                    Kind = ContentKind.Component, Key = "hero", Version = 1,
                    Origin = ContentOrigin.Package, Category = "Component", SiteId = null,
                    IsActive = true, Enabled = true,
                });
            db.InstalledPackages.AddRange(
                new InstalledPackage { Category = "Component", Key = "visitorthing", Version = 1, SiteId = sandboxId },
                new InstalledPackage { Category = "Component", Key = "hero", Version = 1, SiteId = null });
            db.Settings.AddRange(
                new SettingEntry { Scope = "Site", ScopeId = sandboxId, Key = "css.site", Value = "body{}" },
                new SettingEntry { Scope = "Site", ScopeId = mainId, Key = "css.site", Value = "main{}" });
            await db.SaveChangesAsync();
        }

        var outcome = await Reset(env, null).ResetAsync(sandboxId, DateTime.UtcNow);

        await using var read = env.Db();
        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Ok, Is.True);
            Assert.That(outcome.PagesRemoved, Is.EqualTo(1));
            Assert.That(outcome.PackagesRemoved, Is.EqualTo(1));

            Assert.That(await read.Pages.IgnoreQueryFilters().Select(p => p.Id).ToListAsync(),
                Is.EqualTo(new[] { mainPage.Id }), "the real site's page is untouched");
            Assert.That(await read.ContentDefinitions.Select(c => c.Key).ToListAsync(),
                Is.EqualTo(new[] { "hero" }), "the shared library was never the sandbox's to drop");
            Assert.That(await read.InstalledPackages.Select(p => p.Key).ToListAsync(),
                Is.EqualTo(new[] { "hero" }));
            Assert.That(await read.Settings.Select(s => s.ScopeId).ToListAsync(),
                Is.EqualTo(new int?[] { mainId }), "only the sandbox's own settings go");
        });
    }

    [Test]
    public async Task APageIsHardDeleted_NotSoftDeleted_SoTheBaselineCanReclaimItsSlug()
    {
        var env = NewEnv();
        var (_, sandboxId) = await SeedSitesAsync(env);
        await AddPageAsync(env, sandboxId, "visitor-page");

        await Reset(env, null).ResetAsync(sandboxId, DateTime.UtcNow);

        await using var read = env.Db();
        // Soft rows would accumulate forever and collide with the baseline's slugs on the next restore.
        Assert.That(await read.Pages.IgnoreQueryFilters().AnyAsync(p => p.SiteId == sandboxId), Is.False);
    }

    [Test]
    public async Task ComponentMetadataGoesWithThePagesItKeysOn()
    {
        var env = NewEnv();
        var (mainId, sandboxId) = await SeedSitesAsync(env);
        var mainPage = await AddPageAsync(env, mainId, "frontpage");
        var visitorPage = await AddPageAsync(env, sandboxId, "visitor-page");

        await using (var db = env.Db())
        {
            // ComponentMetadata keys on PageUid with no foreign key, so nothing cascades it away.
            db.ComponentMetadata.AddRange(
                new ComponentMetadata { PageUid = visitorPage.Uid, ComponentKey = "frommd", SlotName = "main", MetadataJson = "{}" },
                new ComponentMetadata { PageUid = mainPage.Uid, ComponentKey = "frommd", SlotName = "main", MetadataJson = "{}" });
            await db.SaveChangesAsync();
        }

        await Reset(env, null).ResetAsync(sandboxId, DateTime.UtcNow);

        await using var read = env.Db();
        Assert.That(await read.ComponentMetadata.Select(m => m.PageUid).ToListAsync(),
            Is.EqualTo(new[] { mainPage.Uid }), "the sandbox's metadata is gone; the main site's is not");
    }

    // ---- Day Zero --------------------------------------------------------------------------

    [Test]
    public async Task DayZeroIsRestoredFromTheBaselineBundle()
    {
        // A real baseline: exported from a populated environment by the same CLI an operator runs.
        var origin = NewEnv();
        await using (var db = origin.Db())
        {
            db.Sites.Add(new Site { Key = "default", Name = "MindAttic", IsDefault = true, CreatedUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var originSiteId = (await origin.Db().Sites.SingleAsync()).Id;
        await AddPageAsync(origin, originSiteId, "welcome", "<h1>Day Zero</h1>");
        await AddPageAsync(origin, originSiteId, "about", "<p>about</p>");
        var baseline = await BaselineFromAsync(origin);

        var env = NewEnv();
        var (_, sandboxId) = await SeedSitesAsync(env);
        await AddPageAsync(env, sandboxId, "visitor-scribbles", "<p>whatever the visitor left</p>");

        var outcome = await Reset(env, baseline).ResetAsync(sandboxId, DateTime.UtcNow);

        await using var read = env.Db();
        var restored = await read.Pages.Where(p => p.SiteId == sandboxId).Select(p => p.Slug).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Ok, Is.True);
            Assert.That(restored, Is.EquivalentTo(new[] { "welcome", "about" }));
            Assert.That(restored, Does.Not.Contain("visitor-scribbles"), "the visitor's work is gone");
            Assert.That(outcome.Restored!.PagesCreated, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task TheBaselineNeverOverwritesTheSandboxsOwnIdentity()
    {
        // The baseline is a bundle exported from the REAL site, so its site block names the real site,
        // its bindings and its theme. Applying that would rename the sandbox, steal the production
        // hostname, and clear the flags that make it a sandbox at all — losing the showroom on the very
        // routine that is supposed to refresh it.
        var origin = NewEnv();
        await using (var db = origin.Db())
        {
            db.Sites.Add(new Site
            {
                Key = "default", Name = "MindAttic", HostBindings = "mindattic.com,www.mindattic.com",
                IsDefault = true, DefaultThemeKey = "cyberspace", CreatedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        await AddPageAsync(origin, (await origin.Db().Sites.SingleAsync()).Id, "welcome");
        var baseline = await BaselineFromAsync(origin);

        var env = NewEnv();
        var (_, sandboxId) = await SeedSitesAsync(env);

        await Reset(env, baseline).ResetAsync(sandboxId, DateTime.UtcNow);

        await using var read = env.Db();
        var sandbox = await read.Sites.SingleAsync(s => s.Id == sandboxId);
        Assert.Multiple(() =>
        {
            Assert.That(sandbox.Key, Is.EqualTo("showroom"));
            Assert.That(sandbox.Name, Is.EqualTo("Showroom"));
            Assert.That(sandbox.HostBindings, Is.EqualTo("showroom.example.com"),
                "the sandbox must not inherit production's hostname");
            Assert.That(sandbox.IsSandbox, Is.True, "and must still be a sandbox afterwards");
            Assert.That(sandbox.IsDefault, Is.False);
            Assert.That(sandbox.ResetPolicy, Is.EqualTo(SandboxService.WhenIdle));
        });
    }

    [Test]
    public async Task ARestoreNeverStealsAnotherSitesPages()
    {
        // The baseline's page uids are the REAL site's uids, and this deployment holds those very rows.
        // A uid match that ignored the site would re-point production's pages at the sandbox.
        var origin = NewEnv();
        await using (var db = origin.Db())
        {
            db.Sites.Add(new Site { Key = "default", Name = "MindAttic", IsDefault = true, CreatedUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var originSiteId = (await origin.Db().Sites.SingleAsync()).Id;
        var originPage = await AddPageAsync(origin, originSiteId, "welcome", "<h1>real</h1>");
        var baseline = await BaselineFromAsync(origin);

        var env = NewEnv();
        var (mainId, sandboxId) = await SeedSitesAsync(env);
        // The same uid already lives here, on the MAIN site.
        await using (var db = env.Db())
        {
            db.Pages.Add(new CmsPage
            {
                Uid = originPage.Uid, SiteId = mainId, Slug = "welcome", Title = "welcome",
                Kind = PageKind.Data, BodyHtml = "<h1>real</h1>", IsPublished = true, Enabled = true,
                CreatedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await Reset(env, baseline).ResetAsync(sandboxId, DateTime.UtcNow);

        await using var read = env.Db();
        Assert.Multiple(async () =>
        {
            Assert.That(await read.Pages.CountAsync(p => p.SiteId == mainId), Is.EqualTo(1),
                "the main site still has its page");
            Assert.That(await read.Pages.CountAsync(p => p.SiteId == sandboxId), Is.EqualTo(1),
                "and the sandbox got its own copy, not a move of that one");
            Assert.That(await read.Pages.CountAsync(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task WithNoBaselineTheSiteIsEmptiedAndSaysSo()
    {
        var env = NewEnv();
        var (_, sandboxId) = await SeedSitesAsync(env);
        await AddPageAsync(env, sandboxId, "visitor-page");

        var outcome = await Reset(env, null).ResetAsync(sandboxId, DateTime.UtcNow);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Ok, Is.True);
            Assert.That(outcome.Restored, Is.Null);
            Assert.That(outcome.Explanation, Does.Contain("No baseline"));
        });
    }

    [Test]
    public async Task AnUnreadableBaselineIsReportedRatherThanSwallowed()
    {
        var env = NewEnv();
        var (_, sandboxId) = await SeedSitesAsync(env);
        await AddPageAsync(env, sandboxId, "visitor-page");

        var outcome = await Reset(env, "not a zip"u8.ToArray()).ResetAsync(sandboxId, DateTime.UtcNow);

        await using var read = env.Db();
        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Ok, Is.False, "a reset that could not restore did not do its job");
            Assert.That(outcome.Explanation, Does.Contain("cleared"),
                "and it must say the site IS cleared, rather than implying nothing happened");
            Assert.That(await read.Pages.AnyAsync(p => p.SiteId == sandboxId), Is.False);
            Assert.That((await read.Sites.SingleAsync(s => s.Id == sandboxId)).LastResetUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task AResetStampsLastResetUtc()
    {
        var env = NewEnv();
        var (_, sandboxId) = await SeedSitesAsync(env);
        var when = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

        await Reset(env, null).ResetAsync(sandboxId, when);

        await using var read = env.Db();
        Assert.That((await read.Sites.SingleAsync(s => s.Id == sandboxId)).LastResetUtc, Is.EqualTo(when));
    }

    // ---- the sweep -------------------------------------------------------------------------

    private sealed class RecordingReset : ISandboxResetService
    {
        public readonly List<int> ResetSiteIds = [];
        public Task<SandboxResetOutcome> ResetAsync(int siteId, DateTime utcNow, CancellationToken ct = default)
        {
            ResetSiteIds.Add(siteId);
            return Task.FromResult(new SandboxResetOutcome(true, SandboxRefusal.None, "ok"));
        }
    }

    private static (SandboxResetSweep Sweep, RecordingReset Recorder) NewSweep(Env env, DateTime now)
    {
        var recorder = new RecordingReset();
        var services = new ServiceCollection();
        services.AddScoped(_ => env.Factory.CreateDbContext());
        services.AddScoped<ISandboxService>(sp => new SandboxService(sp.GetRequiredService<CmsDbContext>()));
        services.AddSingleton<ISandboxResetService>(recorder);
        var provider = services.BuildServiceProvider();

        var time = new FakeTimeProvider(now);
        var sweep = new SandboxResetSweep(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new SandboxSweepOptions { Enabled = true },
            NullLogger<SandboxResetSweep>.Instance,
            time);
        return (sweep, recorder);
    }

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    [Test]
    public async Task TheSweepResetsAnIdleShowroomAndNothingElse()
    {
        var env = NewEnv();
        var (mainId, sandboxId) = await SeedSitesAsync(env, graceMinutes: 10);
        var now = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

        // Nobody has been seen for well past the grace period.
        await using (var db = env.Db())
        {
            db.AuthSessions.Add(new MindAttic.Authentication.Entities.AuthSession
            {
                Id = Guid.NewGuid(), AuthUserId = Guid.NewGuid(), CreatedUtc = now.AddHours(-1),
                LastSeenUtc = now.AddMinutes(-30), AbsoluteExpiryUtc = now.AddHours(4),
            });
            await db.SaveChangesAsync();
        }

        var (sweep, recorder) = NewSweep(env, now);
        await sweep.SweepOnceAsync();

        Assert.That(recorder.ResetSiteIds, Is.EqualTo(new[] { sandboxId }),
            "the sweep offers the executor only sites the gate already allowed — never the default site");
        Assert.That(recorder.ResetSiteIds, Does.Not.Contain(mainId));
    }

    [Test]
    public async Task TheSweepLeavesALiveShowroomAlone()
    {
        var env = NewEnv();
        await SeedSitesAsync(env, graceMinutes: 10);
        var now = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

        await using (var db = env.Db())
        {
            db.AuthSessions.Add(new MindAttic.Authentication.Entities.AuthSession
            {
                Id = Guid.NewGuid(), AuthUserId = Guid.NewGuid(), CreatedUtc = now.AddHours(-1),
                LastSeenUtc = now.AddMinutes(-1),   // someone is mid-demo
                AbsoluteExpiryUtc = now.AddHours(4),
            });
            await db.SaveChangesAsync();
        }

        var (sweep, recorder) = NewSweep(env, now);
        await sweep.SweepOnceAsync();

        Assert.That(recorder.ResetSiteIds, Is.Empty, "wiping the site under a visitor would read as a crash");
    }
}
