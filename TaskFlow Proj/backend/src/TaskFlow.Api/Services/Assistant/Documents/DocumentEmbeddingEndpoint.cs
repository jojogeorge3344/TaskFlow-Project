using Microsoft.Extensions.Options;
using TaskFlow.Api.Config;
using TaskFlow.Api.Integrations.Ai.Embeddings;
using TaskFlow.Api.Integrations.Ai.KeyProtection;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Repositories.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.Api.Services.Assistant.Documents;

/// <summary>Which Gemini model and key embed documents and questions (design D3): the configured embedding model, with the
/// API key saved for the built-in Google Gemini provider, whichever chat provider is selected. It is resolved for every run
/// and every question, so a key saved in TFA Settings is used without a restart.</summary>
public class DocumentEmbeddingEndpoint
{
    private readonly IAiProviderConfigRepository _providers;
    private readonly IAiKeyProtector _keyProtector;
    private readonly DocumentKnowledgeOptions _options;

    public DocumentEmbeddingEndpoint(IAiProviderConfigRepository providers, IAiKeyProtector keyProtector, IOptions<DocumentKnowledgeOptions> options)
    {
        _providers = providers;
        _keyProtector = keyProtector;
        _options = options.Value;
    }

    /// <exception cref="EmbeddingUnavailableException">No Gemini key is saved, or the saved one cannot be decrypted.</exception>
    public async Task<EmbeddingEndpoint> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var model = _options.EmbeddingModel.Trim();
        var encrypted = (await _providers.GetBuiltInAsync(AiApiType.Gemini, cancellationToken))?.EncryptedApiKey;
        var key = string.IsNullOrEmpty(encrypted) ? null : _keyProtector.Unprotect(encrypted);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new EmbeddingUnavailableException(EmbeddingFailureKind.MissingKey, model);
        }

        return new EmbeddingEndpoint(model, _options.EmbeddingDimensions, key);
    }
}
