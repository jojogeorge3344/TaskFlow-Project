using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using TaskFlow.Api.Persistence;
using TaskFlow.Api.Persistence.Seeders;

var builder = WebApplication.CreateBuilder(args);

// Per-environment values (CORS, mail links, seeding) live in this one file; environment variables still override it.
builder.Configuration.AddJsonFile("appsettings.Environment.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Each layer registers its own types: see the Config folder.
builder.Services
    .AddCrossCutting(builder.Configuration)
    .AddRepositories(builder.Configuration)
    .AddServices(builder.Configuration)
    .AddIntegrations(builder.Configuration);

var app = builder.Build();


// The data folder must exist and be writable before the database, key ring or attachments are touched.
var storageRoot = StorageRoot.Resolve(app.Services.GetRequiredService<IOptions<StorageOptions>>().Value.Root);
StorageRoot.EnsureAvailable(storageRoot);

// Checked before the migration below creates the new database, so an upgraded install is told why it starts empty.
if (StorageRoot.FindLegacyDatabase(storageRoot, DatabasePathResolver.FindSolutionDirectory()) is { } legacyDatabase)
{
    app.Logger.LogWarning(
        "Found an earlier database at {LegacyDatabase}, but TaskFlow now keeps its data in {StorageRoot} and has no database there yet, "
        + "so it will start empty. The old data is not migrated: stop the API and copy taskflow.db (and the dp-keys and "
        + "mail-attachments folders next to it) into {StorageRoot}, or set Storage:Root to the old folder.",
        legacyDatabase,
        storageRoot);
}

if (OperatingSystem.IsWindows())
{
    var keyRingFolder = StorageRoot.ResolveUnder(
        storageRoot,
        app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TaskFlow.Api.Config.AiProviderOptions>>().Value.KeyRingPath);
    var keyRingCheck = TaskFlow.Api.Integrations.Ai.KeyProtection.KeyRingProtectionCheck.Check(keyRingFolder);
    if (keyRingCheck.UnprotectedKeys > 0)
    {
        app.Logger.LogWarning(
            "The AI key ring in {KeyRingFolder} has {Count} key(s) stored unprotected, from before DPAPI protection was turned on. "
            + "To protect it: stop the API, delete that folder, start the API and enter the AI provider API keys again.",
            keyRingFolder,
            keyRingCheck.UnprotectedKeys);
    }

    if (keyRingCheck.CouldNotCheckReason is { } reason)
    {
        app.Logger.LogInformation(
            "The AI key ring in {KeyRingFolder} could not be fully checked for unprotected keys: {Reason}. "
            + "If it was created before DPAPI protection was turned on, it may still hold an unprotected key.",
            keyRingFolder,
            reason);
    }
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<TaskFlow.Api.Persistence.Seeders.SettingsInitializer>().InitializeAsync();

    await scope.ServiceProvider.GetRequiredService<AccessControlSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<TaskFlow.Api.Persistence.Seeders.AiProviderSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapGet("/api/v1/diagnostics/throw", (Func<IResult>)(() => throw new InvalidOperationException("Diagnostic failure.")));
}

app.UseForwardedHeaders();
// Outside the exception handler, so the entry records the final status of a request that failed.
app.UseMiddleware<TaskFlow.Api.Middleware.RequestLoggingMiddleware>();
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseCors(CorsPolicies.SpaClient);

app.UseAuthentication();
app.UseMiddleware<TaskFlow.Api.Middleware.MfaSetupGateMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>The entry point, visible to the integration tests' WebApplicationFactory.</summary>
public partial class Program;
