using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.DependencyInjection;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Entities;
using MindAttic.Ideas.Core.Services;
using MindAttic.Ideas.Web.Components;
using MindAttic.Legion;
using MindAttic.Vault.Configuration;
using MindAttic.Vault.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// --- MindAttic.Vault: all credentials/config flow through the Vault chain (A6). No User Secrets. ---
builder.Configuration.AddMindAtticVaultFiles().AddEnvironmentVariables();
builder.Services.AddMindAtticVault(builder.Configuration);

// Connection string resolves through config/env (Vault-compatible); LocalDB fallback for dev.
var connectionString =
    builder.Configuration.GetConnectionString("Ideas")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Ideas")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=MindAtticIdeas;Trusted_Connection=True;TrustServerCertificate=True";

// --- CMS Core: EF, discovery over this assembly's citizens + referenced Idea RCLs, catalog, gate,
//     auth/seed. The MindAttic front page ships as a compiled Page Idea (an RCL NuGet). ---
builder.Services.AddIdeasCore(
    connectionString,
    typeof(Program).Assembly,
    typeof(MindAttic.Ideas.Page.MindAtticFrontpage.V1).Assembly);

// --- MindAttic.Legion: LLM + voting (A7). Zero-config; keys resolve via Vault when used. ---
builder.Services.AddLegionClient();

// --- Blazor (global InteractiveServer). ---
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// --- Auth: cookie (ported from MindAttic.Frontend). Login endpoint + SecurityStamp revalidation
//     arrive with the Phase-2 admin UI; the scheme + policies are wired now so they never change. ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "Ideas.Auth";
        o.LoginPath = "/admin/login";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.ExpireTimeSpan = TimeSpan.FromDays(30);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("Admin", p => p.RequireRole(UserRoles.Admin));
    o.AddPolicy("AuthorRawMarkup", p => p.RequireClaim(CmsClaims.AuthorRawMarkup));
});

var app = builder.Build();

// --- Startup: migrate -> discover citizens -> idempotent seed. ---
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    await using (var db = await sp.GetRequiredService<IDbContextFactory<CmsDbContext>>().CreateDbContextAsync())
        await db.Database.MigrateAsync();
    await sp.GetRequiredService<DiscoveryService>().RunAsync();
    var adminUser = builder.Configuration["Ideas:AdminUsername"] ?? "admin";
    var adminPass = builder.Configuration["Ideas:AdminPassword"] ?? "ChangeMe!2026";
    await sp.GetRequiredService<SeedService>().SeedAsync(adminUser, adminPass);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
// Reserved runtime-asset route (Phase 5 fills the runtime arm via a PhysicalFileProvider).
app.MapGet("/_ideas/{*path}", () => Results.NotFound());
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
