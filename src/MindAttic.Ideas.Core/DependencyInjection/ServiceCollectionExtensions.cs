using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MindAttic.Ideas.Abstractions;
using MindAttic.Ideas.Core.Data;
using MindAttic.Ideas.Core.Discovery;
using MindAttic.Ideas.Core.Rendering;
using MindAttic.Ideas.Core.Services;

namespace MindAttic.Ideas.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the CMS Core: DbContext factory, the compiled discovery source over the given citizen
    /// assemblies, the catalog + type resolver, the raw-content gate, and auth/seed/discovery services.
    /// </summary>
    public static IServiceCollection AddIdeasCore(
        this IServiceCollection services, string connectionString, params Assembly[] citizenAssemblies)
    {
        services.AddDbContextFactory<CmsDbContext>(o => o.UseSqlServer(connectionString));
        // The MindAttic.Authentication seam resolves AddScoped<IAuthDataContext>(sp => GetRequiredService<CmsDbContext>()),
        // and AuthBootstrapper/IUserStore are scoped — so CmsDbContext needs a SCOPED registration too (the
        // factory alone doesn't provide one). Both share the same connection string.
        services.AddDbContext<CmsDbContext>(o => o.UseSqlServer(connectionString));

        // Discovery + catalog (singletons: one shared catalog snapshot for the app). The ALC-aware resolver
        // loads PACKAGE citizens through a per-package collectible context and delegates every other
        // descriptor to the default resolver — so compiled content is unchanged and package types resolve
        // once their bytes are extracted (otherwise a placeholder).
        services.AddSingleton<DefaultTypeResolver>();
        services.AddSingleton<ITypeResolver>(sp => new AlcAwareTypeResolver(
            sp.GetRequiredService<DefaultTypeResolver>(), sp.GetRequiredService<IPackageExtractor>()));
        services.AddSingleton<ContentCatalog>();
        services.AddSingleton<IContentCatalog>(sp => sp.GetRequiredService<ContentCatalog>());
        var assemblies = citizenAssemblies.Length > 0 ? citizenAssemblies : new[] { Assembly.GetEntryAssembly()! };
        services.AddSingleton<ICmsContentSource>(_ => new CompiledContentSource(assemblies));
        services.AddSingleton<DiscoveryService>();

        // Rendering.
        services.AddSingleton<IRawContentGate, RawContentGate>();

        // Seed (CMS content). Auth seeding is the library's AuthBootstrapper, wired in the Web host.
        services.AddScoped<SeedService>();

        // Phase-2: admin inbox + lifecycle + page authoring + the render-alert sink.
        services.AddScoped<IAdminInboxService, AdminInboxService>();
        services.AddSingleton<IRenderAlertSink, RenderAlertSink>();
        services.AddScoped<IContentLifecycleService, ContentLifecycleService>();
        services.AddScoped<IPageAdminService, PageAdminService>();

        // Phase-5: .idea package install (validate + persist bytes + extract + register rows + ALC resolve).
        // Local file store/extractor by default; the ADR's Azure Blob backing slots in behind IPackageBlobStore.
        services.AddSingleton<IPackageBlobStore>(_ => new LocalFilePackageBlobStore());
        services.AddSingleton<IPackageExtractor>(_ => new PackageExtractor());
        services.AddScoped<IPackageInstallService, PackageInstallService>();

        return services;
    }
}
