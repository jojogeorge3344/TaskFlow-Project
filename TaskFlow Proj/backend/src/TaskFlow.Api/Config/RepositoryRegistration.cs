using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Persistence.Seeders;
using TaskFlow.Api.Repositories;
using TaskFlow.Api.Repositories.Interfaces;
using TaskFlow.Application.Abstractions;

namespace TaskFlow.Api.Config;

/// <summary>The data-access layer: the database context, the unit of work, the module repositories and startup seeding.</summary>
public static class RepositoryRegistration
{
    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        // Resolved when the context is first built (not while the app starts) so configuration added later, such as by the
        // integration tests, decides where the database goes.
        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseSqlite(DatabasePathResolver.Resolve(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Default"),
                StorageRoot.Resolve(provider.GetRequiredService<IOptions<StorageOptions>>().Value.Root))));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<AccessControlSeeder>();
        services.AddScoped<TaskFlow.Infrastructure.Assistant.AiProviderSeeder>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<TaskFlow.Infrastructure.Settings.SettingsInitializer>();

        return services;
    }
}
