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

        // Settings
        services.AddScoped<TaskFlow.Api.Repositories.Interfaces.Settings.ApplicationSettings.IApplicationSettingsRepository, TaskFlow.Api.Repositories.Settings.ApplicationSettings.ApplicationSettingsRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Interfaces.Settings.RoleConfiguration.IRoleRepository, TaskFlow.Api.Repositories.Settings.RoleConfiguration.RoleRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Interfaces.Settings.RoleConfiguration.IPermissionRepository, TaskFlow.Api.Repositories.Settings.RoleConfiguration.PermissionRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Interfaces.Settings.TfaSettings.IAiProviderConfigRepository, TaskFlow.Api.Repositories.Settings.TfaSettings.AiProviderConfigRepository>();
        services.AddScoped<TaskFlow.Api.Repositories.Interfaces.Settings.TfaSettings.IAiModelRepository, TaskFlow.Api.Repositories.Settings.TfaSettings.AiModelRepository>();

        // Management
        services.AddScoped<TaskFlow.Api.Repositories.Interfaces.Management.EmployeeManagement.IEmployeeRepository, TaskFlow.Api.Repositories.Management.EmployeeManagement.EmployeeRepository>();

        // Auth
        services.AddScoped<TaskFlow.Api.Repositories.Interfaces.Auth.IRefreshTokenRepository, TaskFlow.Api.Repositories.Auth.RefreshTokenRepository>();

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<AccessControlSeeder>();
        services.AddScoped<TaskFlow.Api.Persistence.Seeders.AiProviderSeeder>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<TaskFlow.Api.Persistence.Seeders.SettingsInitializer>();

        return services;
    }
}
