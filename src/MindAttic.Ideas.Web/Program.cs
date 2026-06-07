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
using MindAttic.Ideas.Web.Components;
using MindAttic.Ideas.Web.Services;
using MindAttic.Legion;
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
        "LLM", "Brokers", "Tokens", "Subtitles", "Notifications", "AudioStore", "Security",
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
    // Widgets/Controls available to reference by {{tag}} out of the box. Idempotent and allowOverride:false —
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
app.MapGet("/_ideas/{*path}", () => Results.NotFound());   // anything else under /_ideas
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   // PageHost (the catch-all "/{*Slug}" content route) lives in the MindAttic.Ideas.Rendering RCL.
   // .NET 8+ endpoint-based routing only discovers routable components from App's assembly unless the
   // RCL is registered here — the <Router AdditionalAssemblies> alone does NOT register server endpoints,
   // so without this every content page 404s.
   .AddAdditionalAssemblies(typeof(MindAttic.Ideas.Rendering.PageHost).Assembly);

// MindAttic.Authentication HTTP endpoints — /_ma-auth/{login,mfa-challenge,logout,change-password,reset/*}.
app.MapMindAtticAuthEndpoints();

app.Run();
