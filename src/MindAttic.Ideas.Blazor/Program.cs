using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MindAttic.Authentication;
using MindAttic.Authentication.Web;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.DependencyInjection;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;
using MindAttic.Ideas.Blazor.Cli;
using MindAttic.Ideas.Blazor.Components;
using MindAttic.Ideas.Blazor.Services;
using MindAttic.Legion;
using MindAttic.Media;
using MindAttic.Vault.Configuration;
using MindAttic.Vault.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// --- MindAttic.Vault: all credentials/config flow through the Vault chain (A6). No User Secrets. ---
// "Security" is the MindAttic.Authentication trust domain (pepper, bootstrap-token, reset-token-key);
// it is NOT in the default bucket list, so it must be named explicitly or the auth secrets at
// %APPDATA%\MindAttic\Security\providers.json never bind and AuthBootstrapper fail-closes.
builder.Configuration
    .AddMindAtticVaultFiles(o => o.Buckets = new[]
    {
        "LLM", "Brokers", "Tokens", "Subtitles", "Notifications", "AudioStore", "Security", "Media",
    })
    .AddEnvironmentVariables();
builder.Services.AddMindAtticVault(builder.Configuration);

// Connection string resolves through config/env (Vault-compatible); LocalDB fallback for dev.
var connectionString =
    builder.Configuration.GetConnectionString("Ideas")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Ideas")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=MindAtticIdeas;Trusted_Connection=True;TrustServerCertificate=True";

// --- CMS Core: EF, discovery over this assembly's citizens + referenced Idea RCLs, catalog, gate,
//     seed. The MindAttic front page ships as a compiled Page Idea (an RCL NuGet). ---
builder.Services.AddIdeasCore(
    connectionString,
    typeof(Program).Assembly,
    typeof(MindAttic.Ideas.Page.Frontpage.V1).Assembly);

// --- Showroom mode (A38): a sandbox site that returns to Day Zero once nobody is using it. OFF unless
//     configured, because the sweep it starts is a loop that deletes content. The baseline path is
//     resolved against the content root so configuration can name it relatively.
//     "Showroom": { "Enabled": true, "IntervalMinutes": 2, "BaselineBundle": "seed/mindattic-site.ideabundle" }
var showroomCfg = builder.Configuration.GetSection("Showroom");
if (showroomCfg.Exists())
{
    builder.Services.AddShowroom(o =>
    {
        o.Enabled = showroomCfg.GetValue("Enabled", false);
        var minutes = showroomCfg.GetValue("IntervalMinutes", 2);
        o.Interval = TimeSpan.FromMinutes(minutes <= 0 ? 2 : minutes);
        if (showroomCfg["BaselineBundle"] is { Length: > 0 } baseline)
            o.BaselineBundlePath = Path.IsPathRooted(baseline)
                ? baseline
                : Path.Combine(builder.Environment.ContentRootPath, baseline);
    });
}

// --- Media store (A31): local disk by default, Azure Blob when Media:Provider=azure. AddIdeasCore
//     already registered the local store; the Azure registration replaces it. The page-facing contract
//     is /_media/{uid} either way, so switching the backing store changes no page markup. Blob-backed
//     media serves via a short-lived SAS redirect, which is what gives video working Range/seek. ---
var mediaRoot = Path.Combine(builder.Environment.ContentRootPath, "media");
builder.Services.AddConfiguredMediaStore<CmsDbContext>(builder.Configuration, mediaRoot);

// --- MindAttic.Legion: LLM + voting (A7). Zero-config; keys resolve via Vault when used. ---
builder.Services.AddLegionClient();

// --- Blazor (global InteractiveServer available; auth pages stay static SSR). ---
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();

// --- Auth: the unified, Vault-backed MindAttic.Authentication engine (FOUNDATION_AMENDMENTS A16),
//     replacing the interim cookie/AuthService stack. It registers the cookie schemes, MaPolicies.Admin,
//     Data Protection, cascading auth state + a revalidating provider, and all auth services over
//     CmsDbContext (the IAuthDataContext). MFA is off for now ⇒ MaPolicies.Admin is role-only. ---
builder.Services.AddMindAtticAuthentication<CmsDbContext>(builder.Configuration, o =>
{
    o.AppName = "Ideas";                                   // per-app Data Protection trust boundary (no cross-app SSO)
    o.IsProduction = !builder.Environment.IsDevelopment();
    // Keep the Ideas-owned policies working on the library's principal. Neither name is the canonical
    // ma:admin, so this composes with the library's own MaPolicies.Admin registration.
    o.ConfigureAdditionalPolicies = authz =>
    {
        authz.AddPolicy("Admin", p => p.RequireRole(MaRoles.Admin));
        authz.AddPolicy("AuthorRawMarkup", p => p.RequireClaim(CmsClaims.AuthorRawMarkup));
    };
    if (o.IsProduction)
    {
        // PROD: persist + protect the Data Protection key ring (the library fail-closes if absent in prod).
        o.ConfigureDataProtection = dp =>
        {
            var cred = new Azure.Identity.DefaultAzureCredential();
            var blobUri = builder.Configuration["DataProtection:BlobUri"]
                ?? throw new InvalidOperationException("DataProtection:BlobUri is required in production.");
            var kvKeyId = builder.Configuration["DataProtection:KeyVaultKeyId"]
                ?? throw new InvalidOperationException("DataProtection:KeyVaultKeyId is required in production.");
            dp.PersistKeysToAzureBlobStorage(new Uri(blobUri), cred)
              .ProtectKeysWithAzureKeyVault(new Uri(kvKeyId), cred);
        };
    }
    // DEV: the library persists the key ring to %APPDATA%\MindAttic\DataProtection\Ideas.
});

// Re-emit the Ideas Cms.AuthorRawMarkup claim at sign-in for trusted authors (Admins).
builder.Services.AddScoped<IMaClaimsAugmentor, IdeasClaimsAugmentor>();

var app = builder.Build();

// --- Startup: migrate -> discover citizens -> seed CMS content -> bootstrap admin. ---
// MigrateAsync is dev-only (prod runs DDL in the CI migrate job under db_ddladmin). AuthBootstrapper
// seeds 'admin' from the Vault Security:bootstrap-token (MustChangePassword) and no-ops once a user exists.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    if (app.Environment.IsDevelopment())
        await sp.GetRequiredService<CmsDbContext>().Database.MigrateAsync();
    await sp.GetRequiredService<DiscoveryService>().RunAsync();
    await sp.GetRequiredService<SeedService>().SeedAsync();
    await sp.GetRequiredService<MindAttic.Authentication.Services.AuthBootstrapper>().SeedAdminAsync();

    // SHIPS-WITH-A-LIBRARY: install the bundled first-party widgets (./library/*.idea, packed from
    // MindAttic.Ideas.Library) through the REAL install path, so a fresh CMS has the Cyberspace theme +
    // Widgets/Controls available to reference by <Kind.Key /> tag out of the box. Idempotent and allowOverride:false —
    // an already-installed version is a NoOp and an admin-edited catalog row is never clobbered. Optional:
    // if the folder is absent the CMS runs fine with no first-party citizens.
    var libraryDir = Path.Combine(app.Environment.ContentRootPath, "library");
    if (Directory.Exists(libraryDir))
    {
        var seeder = sp.GetRequiredService<MindAttic.Ideas.Core.Services.IPackageInstallService>();
        foreach (var file in Directory.EnumerateFiles(libraryDir, "*.idea").OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                await using var bytes = File.OpenRead(file);
                var plan = await seeder.InstallAsync(bytes, allowOverride: false);
                Console.WriteLine($"[library] {Path.GetFileName(file)} -> {plan.Action}");
            }
            catch (Exception ex) { Console.Error.WriteLine($"[library] {Path.GetFileName(file)} FAILED: {ex.Message}"); }
        }
    }

    // DEV convenience: auto-install every .idea dropped in the IDEAS_DROPBOX folder through the REAL
    // install path (IPackageInstallService = the admin-upload code path), idempotent + allowOverride.
    // Lets you stage a folder of packages and see them compose on a live page with no manual upload.
    if (app.Environment.IsDevelopment()
        && Environment.GetEnvironmentVariable("IDEAS_DROPBOX") is { Length: > 0 } dropbox
        && Directory.Exists(dropbox))
    {
        var installer = sp.GetRequiredService<MindAttic.Ideas.Core.Services.IPackageInstallService>();
        foreach (var file in Directory.EnumerateFiles(dropbox, "*.idea").OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                await using var bytes = File.OpenRead(file);
                var plan = await installer.InstallAsync(bytes, allowOverride: true);
                Console.WriteLine($"[dropbox] {Path.GetFileName(file)} -> {plan.Action}");
            }
            catch (Exception ex) { Console.Error.WriteLine($"[dropbox] {Path.GetFileName(file)} FAILED: {ex.Message}"); }
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Honor the reverse proxy's forwarded scheme/IP (secure cookie + real client IP) before auth.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

// authn + authz + forced-step (MustChangePassword → /account/change-password) + scoped CSP on the auth surface.
app.UseMindAtticAuthentication();
app.UseAntiforgery();

app.MapStaticAssets();
// Runtime package assets: /_ideas/{category}/{key}/{version}/{**path} -> the package's extracted wwwroot
// (category-qualified so a Page and a Component can share a key). ResolveAsset guards path traversal.
var assetContentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
app.MapGet("/_ideas/{category}/{key}/{version:int}/{**path}",
    IResult (string category, string key, int version, string path, IPackageExtractor extractor) =>
    {
        var file = extractor.ResolveAsset(category, key, version, path);
        if (file is null) return Results.NotFound();
        var contentType = assetContentTypes.TryGetContentType(file, out var ct) ? ct : "application/octet-stream";
        return Results.File(File.OpenRead(file), contentType);
    });
// A SITE-OWNED package's assets: /_ideas/sites/{siteId}/{category}/{key}/{version}/{**path} (MAI-A36).
// Deliberately a SIBLING of the route MAI-LAW-4 locks, never a change to it: the shared route above still
// answers exactly what it did, and a site's own copy of the same identity mounts one level in. There is no
// ambiguity between them — a request to the shared route would have to present "sites" as a category and a
// category name as an {version:int}, which cannot match.
app.MapGet("/_ideas/sites/{siteId:int}/{category}/{key}/{version:int}/{**path}",
    IResult (int siteId, string category, string key, int version, string path, IPackageExtractor extractor) =>
    {
        var file = extractor.ResolveAsset(category, key, version, path, siteId);
        if (file is null) return Results.NotFound();
        var contentType = assetContentTypes.TryGetContentType(file, out var ct) ? ct : "application/octet-stream";
        return Results.File(File.OpenRead(file), contentType);
    });
// Admin-only: download the raw .idea blob (for rollback / re-share). Auth guard is enforced by RequireAuthorization.
app.MapGet("/_ideas/packages/{category}/{key}/{version:int}",
    async (string category, string key, int version, IPackageBlobStore blobs, CancellationToken ct) =>
    {
        var blobPath = LocalFilePackageBlobStore.BlobPathFor(category, key, version);
        var stream = await blobs.OpenAsync(blobPath, ct);
        if (stream is null) return Results.NotFound();
        var filename = $"{key}.V{version}.idea";
        return Results.File(stream, "application/octet-stream", filename);
    }).RequireAuthorization("Admin");
// The same download for a site-owned package. Admin-guarded like its shared sibling.
app.MapGet("/_ideas/sites/{siteId:int}/packages/{category}/{key}/{version:int}",
    async (int siteId, string category, string key, int version, IPackageBlobStore blobs, CancellationToken ct) =>
    {
        var blobPath = LocalFilePackageBlobStore.BlobPathFor(category, key, version, siteId);
        var stream = await blobs.OpenAsync(blobPath, ct);
        if (stream is null) return Results.NotFound();
        return Results.File(stream, "application/octet-stream", $"{key}.V{version}.idea");
    }).RequireAuthorization("Admin");
app.MapGet("/_ideas/{*path}", () => Results.NotFound());   // anything else under /_ideas

// Media assets: /_media/{uid:guid} redirects to a signed blob URL when the backend can mint one,
// otherwise streams the payload with Range support (inline for image/text/video/audio/PDF).
app.MapMediaEndpoints();

// Liveness probe for the App Service health check. Deliberately does NOT touch the database: App
// Service restarts an instance that fails this, and a transient SQL blip must not turn into a
// restart loop. Under `/_` like every other reserved route, so it can never shadow a page slug.
app.MapGet("/_health", () => Results.Text("healthy", "text/plain")).AllowAnonymous();
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   // PageHost (the catch-all "/{*Slug}" content route) lives in the MindAttic.Ideas.Rendering RCL.
   // .NET 8+ endpoint-based routing only discovers routable components from App's assembly unless the
   // RCL is registered here — the <Router AdditionalAssemblies> alone does NOT register server endpoints,
   // so without this every content page 404s.
   .AddAdditionalAssemblies(typeof(MindAttic.Ideas.Rendering.PageHost).Assembly);

// MindAttic.Authentication HTTP endpoints — /_ma-auth/{login,mfa-challenge,logout,change-password,reset/*}.
app.MapMindAtticAuthEndpoints();

// ---- CLI mode: --install <file.idea> [--site <key>] -----------------------------------------
// dotnet run --project src/MindAttic.Ideas.Blazor -- --install path/to/Foo.V1.idea [--site showroom]
// Without --site the package installs SHARED, exactly as this verb always did. With it the package is
// owned by that site alone (MAI-A37) — the scripted equivalent of a showroom visitor's upload, and how
// a sandbox baseline gets loaded without a browser.
var installIdx = Array.IndexOf(args, "--install");
if (installIdx >= 0)
{
    var ideaPath = installIdx + 1 < args.Length ? args[installIdx + 1] : null;
    if (string.IsNullOrEmpty(ideaPath) || !File.Exists(ideaPath))
    {
        Console.Error.WriteLine($"[install] File not found: {ideaPath ?? "(none)"}");
        Environment.Exit(1);
    }
    using var cliScope = app.Services.CreateScope();
    var cliSp = cliScope.ServiceProvider;

    int? owningSiteId = null;
    var siteIdx = Array.IndexOf(args, "--site");
    if (siteIdx >= 0)
    {
        var siteKey = siteIdx + 1 < args.Length ? args[siteIdx + 1] : null;
        await using var cliDb = await cliSp.GetRequiredService<IDbContextFactory<CmsDbContext>>().CreateDbContextAsync();
        var target = siteKey is { Length: > 0 }
            ? await cliDb.Sites.FirstOrDefaultAsync(x => x.Key == siteKey)
            : null;
        if (target is null)
        {
            var known = string.Join(", ", await cliDb.Sites.Select(x => x.Key).ToListAsync());
            Console.Error.WriteLine($"[install] No site with key \"{siteKey ?? "(none)"}\". Known keys: {known}");
            Environment.Exit(1);
        }
        owningSiteId = target!.Id;
    }

    var installer = cliSp.GetRequiredService<MindAttic.Ideas.Core.Services.IPackageInstallService>();
    await using var bytes = File.OpenRead(ideaPath);
    var plan = await installer.InstallAsync(bytes, allowOverride: true, owningSiteId);
    var scope = owningSiteId is int sid ? $" (site {sid} only)" : " (shared)";
    Console.WriteLine($"[install] {Path.GetFileName(ideaPath)} -> {plan.Action}{scope}");
    Environment.Exit(0);
}

// ---- CLI mode: --reset-sandbox <site key> ---------------------------------------------------
// dotnet run --project src/MindAttic.Ideas.Blazor -- --reset-sandbox showroom
// The operator-facing form of what the idle sweep does. It decides nothing: SandboxResetService asks
// the gate again, so this refuses the default site exactly as the sweep would (MAI-A38).
var resetIdx = Array.IndexOf(args, "--reset-sandbox");
if (resetIdx >= 0)
{
    var siteKey = resetIdx + 1 < args.Length && !args[resetIdx + 1].StartsWith("--") ? args[resetIdx + 1] : null;
    using var resetScope = app.Services.CreateScope();
    var resetSp = resetScope.ServiceProvider;

    await using var resetDb = await resetSp.GetRequiredService<IDbContextFactory<CmsDbContext>>().CreateDbContextAsync();
    var target = siteKey is { Length: > 0 } ? await resetDb.Sites.FirstOrDefaultAsync(x => x.Key == siteKey) : null;
    if (target is null)
    {
        var known = string.Join(", ", await resetDb.Sites.Select(x => x.Key).ToListAsync());
        Console.Error.WriteLine($"[reset-sandbox] No site with key \"{siteKey ?? "(none)"}\". Known keys: {known}");
        Environment.Exit(1);
    }

    var outcome = await resetSp.GetRequiredService<ISandboxResetService>().ResetAsync(target!.Id, DateTime.UtcNow);
    Console.WriteLine($"[reset-sandbox] {outcome.Explanation}");
    if (outcome.Ok)
        Console.WriteLine($"[reset-sandbox] dropped {outcome.PagesRemoved} page(s), {outcome.PackagesRemoved} package(s)"
                        + (outcome.Restored is { } r ? $"; restored {r.PagesCreated} page(s)." : "."));
    Environment.Exit(outcome.Ok ? 0 : 1);
}

// ---- CLI mode: --extract-media -------------------------------------------------------------
// Lift inline base64 images out of page bodies into the managed media store.
// dotnet run --project src/MindAttic.Ideas.Blazor -- --extract-media [--slug frontpage] [--folder site] [--dry-run]
if (args.Contains("--extract-media"))
{
    var exit = await ExtractMediaCli.RunAsync(args, app.Services);
    Environment.Exit(exit);
}

// ---- CLI mode: --upload-media ---------------------------------------------------------------
// Stream local files straight into the configured media store — the path for anything too large to
// push through the Admin panel's browser circuit (video above all).
// dotnet run --project src/MindAttic.Ideas.Blazor -- --upload-media <file…> [--folder site] [--media-type video] [--dry-run]
if (args.Contains("--upload-media"))
{
    var exit = await UploadMediaCli.RunAsync(args, app.Services);
    Environment.Exit(exit);
}

// ---- CLI mode: --export-content / --import-content ------------------------------------------
// Move AUTHORED CONTENT between environments. A .idea package carries a citizen; a bundle carries
// what an author built with citizens — pages, settings, per-component metadata, and media. This is
// the only path that reproduces hand-curation, which --seed regenerates the shape of but not the
// substance of.
// dotnet run --project src/MindAttic.Ideas.Blazor -- --export-content site.ideabundle [--slug projects/] [--no-media] [--dry-run]
// dotnet run --project src/MindAttic.Ideas.Blazor -- --import-content site.ideabundle [--dry-run] [--untrusted] [--prune]
if (args.Contains("--export-content"))
{
    var exit = await ExportContentCli.RunAsync(args, app.Services);
    Environment.Exit(exit);
}
if (args.Contains("--import-content"))
{
    var exit = await ImportContentCli.RunAsync(args, app.Services);
    Environment.Exit(exit);
}

// ---- CLI mode: --seed <target> --------------------------------------------------------------
// dotnet run --project src/MindAttic.Ideas.Blazor -- --seed core        (re-run SeedService migrations)
// dotnet run --project src/MindAttic.Ideas.Blazor -- --seed from-html [--dry-run]
// dotnet run --project src/MindAttic.Ideas.Blazor -- --seed from-md   [--dry-run]
// dotnet run --project src/MindAttic.Ideas.Blazor -- --seed repos [--org mindattic] [--local-root <dir>] [--dry-run]
var seedIdx = Array.IndexOf(args, "--seed");
if (seedIdx >= 0)
{
    var seedTarget = seedIdx + 1 < args.Length ? args[seedIdx + 1] : null;
    using var cliScope = app.Services.CreateScope();
    int exitCode;
    if (seedTarget == "core")
    {
        await cliScope.ServiceProvider.GetRequiredService<SeedService>().SeedAsync();
        Console.WriteLine("[seed] core seed completed.");
        exitCode = 0;
    }
    else if (seedTarget == "from-html")
        exitCode = await SeedFromHtmlCli.RunAsync(args, app.Services);
    else if (seedTarget == "from-md")
        exitCode = await SeedReadmesCli.RunAsync(args, app.Services);
    else if (seedTarget == "repos")
        exitCode = await SeedReposCli.RunAsync(args, app.Services);
    else
    {
        Console.Error.WriteLine($"[seed] Unknown target: '{seedTarget}'. Use 'core', 'from-html', 'from-md', or 'repos'.");
        exitCode = 1;
    }
    Environment.Exit(exitCode);
}

app.Run();
