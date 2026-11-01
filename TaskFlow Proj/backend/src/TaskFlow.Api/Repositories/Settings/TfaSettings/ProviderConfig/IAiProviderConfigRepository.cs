using TaskFlow.Api.Models.Entities;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Base;

namespace TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;

/// <summary>The AI providers: the built-in ones and those an administrator added.</summary>
public interface IAiProviderConfigRepository : IBaseRepository<AiProviderConfig>
{
    Task<IReadOnlyList<AiProviderConfig>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Every row, tracked, for saving changes to them.</summary>
    Task<List<AiProviderConfig>> GetAllForUpdateAsync(CancellationToken cancellationToken = default);

    Task<AiProviderConfig?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>The built-in provider of an API type (Google Gemini, OpenAI GPT or Ollama).</summary>
    Task<AiProviderConfig?> GetBuiltInAsync(AiApiType apiType, CancellationToken cancellationToken = default);

    /// <summary>The provider with this name, ignoring case.</summary>
    Task<AiProviderConfig?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}
