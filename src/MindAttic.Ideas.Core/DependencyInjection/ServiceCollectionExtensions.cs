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

        // Discovery + catalog (singletons: one shared catalog snapshot for the app).
        services.AddSingleton<ITypeResolver, DefaultTypeResolver>();
        services.AddSingleton<ContentCatalog>();
        services.AddSingleton<IContentCatalog>(sp => sp.GetRequiredService<ContentCatalog>());
        var assemblies = citizenAssemblies.Length > 0 ? citizenAssemblies : new[] { Assembly.GetEntryAssembly()! };
        services.AddSingleton<ICmsContentSource>(_ => new CompiledContentSource(assemblies));
        services.AddSingleton<DiscoveryService>();

        // Rendering.
        services.AddSingleton<IRawContentGate, RawContentGate>();

        // Auth + seed.
        services.AddScoped<AuthService>();
        services.AddScoped<SeedService>();

        return services;
    }
}
