using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Blazor.Cli;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Media;
using CmsPage = MindAttic.Ideas.Core.Entities.Page;

namespace MindAttic.Ideas.Tests;

/// <summary>
/// --export-content / --import-content move AUTHORED content between environments, which is the one
/// thing a .idea package deliberately does not do. The cases that matter are the ones that decide
/// whether a promotion to production is safe to run twice: a bundle must adopt an independently
/// seeded page instead of colliding with it, media uids must be remapped (the store mints them, so
/// they cannot survive the trip), a re-import must move no bytes, and Author trust — which means raw
/// unsanitized markup — must be something the operator can refuse.
/// </summary>
[TestFixture]
public class ContentBundleTests
{
    private sealed class FakeMediaStore : IMediaStore
    {
        public readonly List<MediaItem> Items = [];
        public int Uploads;

        public Task<MediaItem> UploadAsync(Stream content, string fileName, string contentType,
            int? tenantId = null, string folder = "", string mediaType = "", int? width = null,
            int? height = null, string? notes = null, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            var bytes = ms.ToArray();
            Uploads++;
            var item = new MediaItem
            {
                // A NEW uid on every upload — this is the real store's behaviour and the whole reason
                // import has to remap references rather than trusting the exported uid.
                Uid = Guid.NewGuid(), FileName = fileName, ContentType = contentType, Folder = folder,
                MediaType = mediaType, SizeBytes = bytes.Length, Bytes = bytes, Width = width,
                Height = height, Notes = notes,
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

    private sealed class Env
    {
        public required InMemoryFactory Factory { get; init; }
        public required FakeMediaStore Store { get; init; }
        public required IServiceProvider Services { get; init; }
        public CmsDbContext Db() => Factory.CreateDbContext();
    }

    private static Env NewEnv()
    {
        var factory = new InMemoryFactory("bundle_" + Guid.NewGuid().ToString("N"));
        var store = new FakeMediaStore();
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<CmsDbContext>>(factory);
        services.AddScoped(_ => factory.CreateDbContext());
        services.AddSingleton<IMediaStore>(store);
        return new Env { Factory = factory, Store = store, Services = services.BuildServiceProvider() };
    }

    private string _dir = "";

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ideabundle_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string BundlePath => Path.Combine(_dir, "site.ideabundle");

    private static async Task SeedSiteAsync(Env env, string key = "default")
    {
        await using var db = env.Db();
        db.Sites.Add(new Site
        {
            Key = key, Name = "MindAttic", IsDefault = true, DefaultThemeKey = "cyberspace",
            DefaultThemeVersion = 1, CreatedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<CmsPage> AddPageAsync(Env env, string slug, string body,
        ContentTrust trust = ContentTrust.Author, Guid? uid = null)
    {
        await using var db = env.Db();
        var site = await db.Sites.FirstAsync();
        var page = new CmsPage
        {
            Uid = uid ?? Guid.NewGuid(), SiteId = site.Id, Slug = slug, Title = slug,
            Kind = PageKind.Data, BodyHtml = body, BodyTrust = trust, AuthorTrustVersion = 1,
            IsPublished = true, Enabled = true, ThemeKey = "cyberspace", ThemeVersion = 1,
            ActivePluginsJson = """["Plugin.tooltip"]""", CreatedUtc = DateTime.UtcNow,
        };
        db.Pages.Add(page);
        await db.SaveChangesAsync();
        return page;
    }

    private async Task<int> ExportAsync(Env env, params string[] extra) =>
        await ExportContentCli.RunAsync(["--export-content", BundlePath, .. extra], env.Services);

    private async Task<int> ImportAsync(Env env, params string[] extra) =>
        await ImportContentCli.RunAsync(["--import-content", BundlePath, .. extra], env.Services);

    // ---------------------------------------------------------------------------------------

    [Test]
    public async Task RoundTrip_PreservesTheAuthoredPage()
    {
        var source = NewEnv();
        await SeedSiteAsync(source);
        await AddPageAsync(source, "frontpage", "<h1>Hello</h1><Component.Hero />");
        await using (var db = source.Db())
        {
            var p = await db.Pages.FirstAsync();
            p.PageCss = ".x{color:red}";
            p.PageJs = "window.x=1;";
            p.SeoTitle = "Front";
            db.PageMetaTags.Add(new PageMetaTag { PageId = p.Id, Name = "seo.description", Content = "desc" });
            db.Settings.Add(new SettingEntry { Scope = "Host", Key = "css.global", Value = "body{margin:0}" });
            db.ComponentMetadata.Add(new ComponentMetadata
            {
                PageUid = p.Uid, ComponentKey = "frommd", SlotName = "main",
                MetadataJson = """{"localSourceFile":"README.md"}""",
                CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        Assert.That(await ExportAsync(source), Is.Zero);

        var target = NewEnv();
        Assert.That(await ImportAsync(target), Is.Zero);

        await using var read = target.Db();
        var imported = await read.Pages.Include(p => p.MetaTags).SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(imported.Slug, Is.EqualTo("frontpage"));
            Assert.That(imported.BodyHtml, Is.EqualTo("<h1>Hello</h1><Component.Hero />"));
            Assert.That(imported.PageCss, Is.EqualTo(".x{color:red}"));
            Assert.That(imported.PageJs, Is.EqualTo("window.x=1;"));
            Assert.That(imported.SeoTitle, Is.EqualTo("Front"));
            Assert.That(imported.BodyTrust, Is.EqualTo(ContentTrust.Author));
            Assert.That(imported.ThemeKey, Is.EqualTo("cyberspace"));
            Assert.That(imported.ActivePluginsJson, Is.EqualTo("""["Plugin.tooltip"]"""));
            Assert.That(imported.MetaTags.Single().Content, Is.EqualTo("desc"));
            Assert.That((await read.Settings.SingleAsync(s => s.Key == "css.global")).Value,
                Is.EqualTo("body{margin:0}"));
            Assert.That(await read.ComponentMetadata.CountAsync(), Is.EqualTo(1));
            Assert.That((await read.Sites.SingleAsync()).DefaultThemeKey, Is.EqualTo("cyberspace"));
        });
    }

    [Test]
    public async Task ImportAdoptsAnIndependentlySeededPage_BySlug_RatherThanDuplicatingIt()
    {
        var source = NewEnv();
        await SeedSiteAsync(source);
        await AddPageAsync(source, "frontpage", "<h1>authored</h1>");
        Assert.That(await ExportAsync(source), Is.Zero);

        // The target was seeded on its own, so "frontpage" exists with a DIFFERENT uid — the exact
        // shape of a production database that booted before anyone exported anything.
        var target = NewEnv();
        await SeedSiteAsync(target);
        await AddPageAsync(target, "frontpage", "<h1>seeded</h1>");

        Assert.That(await ImportAsync(target), Is.Zero);

        await using var read = target.Db();
        Assert.Multiple(async () =>
        {
            Assert.That(await read.Pages.CountAsync(p => p.Slug == "frontpage"), Is.EqualTo(1),
                "a uid mismatch must not create a second page on the same slug");
            Assert.That((await read.Pages.SingleAsync()).BodyHtml, Is.EqualTo("<h1>authored</h1>"),
                "the bundle is the authority for a page it carries");
        });
    }

    [Test]
    public async Task MediaUidsAreRemapped_SoEveryReferenceStillResolves()
    {
        var source = NewEnv();
        await SeedSiteAsync(source);
        var upload = await source.Store.UploadAsync(
            new MemoryStream([1, 2, 3, 4]), "logo.png", "image/png", folder: "site", mediaType: "image");
        var oldUid = upload.Uid;

        await AddPageAsync(source, "home",
            $"""<Component.MediaImage uid="{oldUid}" alt="logo" /><div style="background:url(/_media/{oldUid})"></div>""");
        await using (var db = source.Db())
        {
            var p = await db.Pages.FirstAsync();
            db.ComponentMetadata.Add(new ComponentMetadata
            {
                PageUid = p.Uid, ComponentKey = "gallery", SlotName = "main",
                MetadataJson = $$"""{"items":["{{oldUid}}"]}""",
                CreatedUtc = DateTime.UtcNow, ModifiedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        Assert.That(await ExportAsync(source), Is.Zero);

        var target = NewEnv();
        Assert.That(await ImportAsync(target), Is.Zero);

        var newUid = target.Store.Items.Single().Uid;
        await using var read = target.Db();
        var body = (await read.Pages.SingleAsync()).BodyHtml!;
        var meta = (await read.ComponentMetadata.SingleAsync()).MetadataJson;

        Assert.Multiple(() =>
        {
            Assert.That(newUid, Is.Not.EqualTo(oldUid), "the store mints the uid, so it must have changed");
            Assert.That(body, Does.Not.Contain(oldUid.ToString()), "no reference may still point at the source uid");
            Assert.That(body, Does.Contain($"uid=\"{newUid}\""));
            Assert.That(body, Does.Contain($"/_media/{newUid}"));
            Assert.That(meta, Does.Contain(newUid.ToString()).And.Not.Contain(oldUid.ToString()));
            Assert.That(target.Store.Items.Single().Bytes, Is.EqualTo(new byte[] { 1, 2, 3, 4 }),
                "the payload itself must survive the archive round trip");
        });
    }

    [Test]
    public async Task SecondImportUploadsNothingAndCreatesNothing()
    {
        var source = NewEnv();
        await SeedSiteAsync(source);
        await source.Store.UploadAsync(new MemoryStream([9, 9, 9]), "a.png", "image/png");
        await AddPageAsync(source, "home", "<p>hi</p>");
        Assert.That(await ExportAsync(source), Is.Zero);

        var target = NewEnv();
        Assert.That(await ImportAsync(target), Is.Zero);
        var afterFirst = target.Store.Uploads;

        Assert.That(await ImportAsync(target), Is.Zero);

        await using var read = target.Db();
        Assert.Multiple(async () =>
        {
            Assert.That(afterFirst, Is.EqualTo(1));
            Assert.That(target.Store.Uploads, Is.EqualTo(1),
                "identical bytes are matched by SHA-256, so a re-import moves no payloads");
            Assert.That(await read.Pages.CountAsync(), Is.EqualTo(1));
            Assert.That(target.Store.Items, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task UntrustedFlag_DowngradesAuthorTrust()
    {
        var source = NewEnv();
        await SeedSiteAsync(source);
        await AddPageAsync(source, "home", "<script>alert(1)</script>", ContentTrust.Author);
        Assert.That(await ExportAsync(source), Is.Zero);

        var target = NewEnv();
        Assert.That(await ImportAsync(target, "--untrusted"), Is.Zero);

        await using var read = target.Db();
        Assert.That((await read.Pages.SingleAsync()).BodyTrust, Is.EqualTo(ContentTrust.Untrusted),
            "an operator must be able to refuse raw-markup trust from a bundle they did not author");
    }

    [Test]
    public async Task DryRunImport_WritesNothing()
    {
        var source = NewEnv();
        await SeedSiteAsync(source);
        await source.Store.UploadAsync(new MemoryStream([7]), "b.png", "image/png");
        await AddPageAsync(source, "home", "<p>hi</p>");
        Assert.That(await ExportAsync(source), Is.Zero);

        var target = NewEnv();
        Assert.That(await ImportAsync(target, "--dry-run"), Is.Zero);

        await using var read = target.Db();
        Assert.Multiple(async () =>
        {
            Assert.That(await read.Pages.CountAsync(), Is.Zero);
            Assert.That(target.Store.Uploads, Is.Zero);
        });
    }

    [Test]
    public async Task SlugFilter_ExportsOnlyTheMatchingSubtree()
    {
        var source = NewEnv();
        await SeedSiteAsync(source);
        await AddPageAsync(source, "frontpage", "<p>front</p>");
        await AddPageAsync(source, "projects/alpha", "<p>a</p>");
        await AddPageAsync(source, "projects/beta", "<p>b</p>");

        Assert.That(await ExportAsync(source, "--slug", "projects/"), Is.Zero);

        var target = NewEnv();
        Assert.That(await ImportAsync(target), Is.Zero);

        await using var read = target.Db();
        var slugs = await read.Pages.Select(p => p.Slug).OrderBy(s => s).ToListAsync();
        Assert.That(slugs, Is.EqualTo(new[] { "projects/alpha", "projects/beta" }));
    }

    [Test]
    public async Task PageTree_SurvivesOnParentUid()
    {
        var source = NewEnv();
        await SeedSiteAsync(source);
        var parent = await AddPageAsync(source, "projects", "<p>index</p>");
        var child = await AddPageAsync(source, "projects/alpha", "<p>a</p>");
        await using (var db = source.Db())
        {
            var c = await db.Pages.FirstAsync(p => p.Uid == child.Uid);
            c.ParentId = parent.Id;
            await db.SaveChangesAsync();
        }

        Assert.That(await ExportAsync(source), Is.Zero);

        var target = NewEnv();
        Assert.That(await ImportAsync(target), Is.Zero);

        await using var read = target.Db();
        var importedParent = await read.Pages.SingleAsync(p => p.Slug == "projects");
        var importedChild = await read.Pages.SingleAsync(p => p.Slug == "projects/alpha");
        Assert.That(importedChild.ParentId, Is.EqualTo(importedParent.Id),
            "the tree must be rebuilt from uids, never from the source environment's integer ids");
    }

    [Test]
    public async Task ABundleFromAFutureFormat_IsRefusedRatherThanPartiallyApplied()
    {
        var source = NewEnv();
        await SeedSiteAsync(source);
        await AddPageAsync(source, "home", "<p>hi</p>");
        Assert.That(await ExportAsync(source), Is.Zero);

        // Rewrite the manifest as a version this host does not know.
        using (var zip = System.IO.Compression.ZipFile.Open(BundlePath, System.IO.Compression.ZipArchiveMode.Update))
        {
            var entry = zip.GetEntry("bundle.json")!;
            string json;
            using (var r = new StreamReader(entry.Open())) json = await r.ReadToEndAsync();
            json = json.Replace("\"formatVersion\": 1", "\"formatVersion\": 99");
            entry.Delete();
            var replacement = zip.CreateEntry("bundle.json");
            await using var w = new StreamWriter(replacement.Open());
            await w.WriteAsync(json);
        }

        var target = NewEnv();
        Assert.That(await ImportAsync(target), Is.EqualTo(1));

        await using var read = target.Db();
        Assert.That(await read.Pages.CountAsync(), Is.Zero, "a refused bundle must leave no partial state");
    }

    [Test]
    public async Task NotABundle_IsReportedRatherThanThrowing()
    {
        await File.WriteAllBytesAsync(BundlePath, System.Text.Encoding.UTF8.GetBytes("not a zip"));
        var target = NewEnv();
        Assert.That(await ImportAsync(target), Is.EqualTo(1),
            "a corrupt archive is reported as a failure exit code, not a stack trace");
    }
}
