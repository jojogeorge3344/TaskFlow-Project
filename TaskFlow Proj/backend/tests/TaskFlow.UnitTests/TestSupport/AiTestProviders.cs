using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Integrations.Ai.Providers;
using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Persistence;

namespace TaskFlow.UnitTests.TestSupport;

/// <summary>Providers are rows identified by id. Tests that only care about the three built-in providers seed them here
/// (switched off, no key, no models; the seeder adds models when a test runs it) and look their ids up by API type.</summary>
internal static class AiTestProviders
{
    /// <summary>Inserts any missing built-in provider row and returns the id of each, by API type.</summary>
    public static async Task<IReadOnlyDictionary<AiApiType, int>> SeedBuiltInsAsync(AppDbContext db)
    {
        foreach (var apiType in AiProviderCatalog.All)
        {
            if (!await db.AiProviderConfigs.AnyAsync(row => row.IsBuiltIn && row.ApiType == apiType))
            {
                db.AiProviderConfigs.Add(new AiProviderConfig
                {
                    Name = AiProviderCatalog.DisplayName(apiType),
                    ApiType = apiType,
                    Locality = AiProviderCatalog.Locality(apiType),
                    IsBuiltIn = true,
                    ModelName = AiProviderCatalog.DefaultModel(apiType),
                    BaseUrl = apiType == AiApiType.Ollama ? AiProviderCatalog.DefaultOllamaBaseUrl : null,
                    UpdatedAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return await db.AiProviderConfigs.AsNoTracking().Where(row => row.IsBuiltIn).ToDictionaryAsync(row => row.ApiType, row => row.Id);
    }

    /// <summary>A runtime for a built-in provider of this API type, for calling a client or the tester directly.</summary>
    public static AiProviderRuntime Runtime(AiApiType apiType, string modelName, string? baseUrl, string? apiKey, int providerId = 1) =>
        new(providerId, apiType, AiProviderCatalog.DisplayName(apiType), apiType == AiApiType.Ollama, modelName, baseUrl, apiKey);
}
