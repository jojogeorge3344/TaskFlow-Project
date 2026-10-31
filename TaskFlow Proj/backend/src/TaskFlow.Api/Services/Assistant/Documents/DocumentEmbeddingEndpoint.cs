using Microsoft.Extensions.Options;
using TaskFlow.Api.Common.Constants;
using TaskFlow.Api.Config;
using TaskFlow.Api.Integrations.Ai.Embeddings;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.Api.Services.Assistant.Documents;

/// <summary>Which Ollama server embeds documents and questions (design D2): <c>DocumentKnowledge:OllamaBaseUrl</c> when set,
/// otherwise the address saved for the Ollama provider in TFA Settings, otherwise the default local address. The saved
/// address was checked against link-local and metadata hosts when it was saved; the configured one at startup.</summary>
public class DocumentEmbeddingEndpoint
{
    private readonly IAiProviderConfigRepository _providers;
    private readonly DocumentKnowledgeOptions _options;

    public DocumentEmbeddingEndpoint(IAiProviderConfigRepository providers, IOptions<DocumentKnowledgeOptions> options)
    {
        _providers = providers;
        _options = options.Value;
    }

    public async Task<EmbeddingEndpoint> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.OllamaBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = (await _providers.GetAsync(AiProviderKind.Ollama, cancellationToken))?.BaseUrl;
        }

        return new EmbeddingEndpoint(
            string.IsNullOrWhiteSpace(baseUrl) ? AiProviderCatalog.DefaultOllamaBaseUrl : baseUrl.Trim().TrimEnd('/'),
            _options.EmbeddingModel.Trim());
    }
}
