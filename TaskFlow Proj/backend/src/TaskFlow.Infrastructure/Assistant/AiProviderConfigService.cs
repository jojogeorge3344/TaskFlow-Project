using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Assistant;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Assistant;

public class AiProviderConfigService : IAiProviderConfigService
{
    private readonly AppDbContext _dbContext;
    private readonly IAiKeyProtector _keyProtector;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AiProviderConfigService>? _logger;

    public AiProviderConfigService(
        AppDbContext dbContext,
        IAiKeyProtector keyProtector,
        TimeProvider timeProvider,
        ILogger<AiProviderConfigService>? logger = null)
    {
        _logger = logger;
        _dbContext = dbContext;
        _keyProtector = keyProtector;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<AiProviderOptionDto>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        var configs = await GetAdminConfigAsync(cancellationToken);
        return configs
            .Where(config => config.IsConfigured)
            .Select(config => new AiProviderOptionDto(config.Kind, config.DisplayName, config.ModelName, config.Locality))
            .ToList();
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> GetAdminConfigAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.AiProviderConfigs.AsNoTracking().ToListAsync(cancellationToken);
        return AiProviderCatalog.All.Select(kind => ToDto(kind, rows.FirstOrDefault(row => row.Kind == kind))).ToList();
    }

    public async Task<IReadOnlyList<AiProviderConfigDto>> SaveAsync(
        int userId,
        UpdateAiProvidersRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var rows = await _dbContext.AiProviderConfigs.ToListAsync(cancellationToken);

        foreach (var update in request.Providers ?? [])
        {
            if (update.Kind is not { } kind || update.IsEnabled is not { } enabled || update.ModelName is null)
            {
                throw new ArgumentException("Every provider setting must be provided.", nameof(request));
            }

            var row = rows.FirstOrDefault(existing => existing.Kind == kind);
            if (row is null)
            {
                row = new AiProviderConfig { Kind = kind };
                _dbContext.AiProviderConfigs.Add(row);
                rows.Add(row);
            }

            row.IsEnabled = enabled;
            row.ModelName = update.ModelName.Trim();
            row.BaseUrl = kind == AiProviderKind.Ollama
                ? NormalizeBaseUrl(update.BaseUrl) ?? AiProviderCatalog.DefaultOllamaBaseUrl
                : null;

            if (!string.IsNullOrEmpty(update.ApiKey))
            {
                if (!AiProviderCatalog.RequiresKey(kind))
                {
                    throw new ArgumentException($"{AiProviderCatalog.DisplayName(kind)} does not use an API key.", nameof(request));
                }

                var key = update.ApiKey.Trim();
                row.EncryptedApiKey = _keyProtector.Protect(key);
                row.KeyLast4 = key.Length <= 4 ? key : key[^4..];
            }
            else if (update.ClearKey == true)
            {
                row.EncryptedApiKey = null;
                row.KeyLast4 = null;
            }

            row.UpdatedByUserId = userId;
            row.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AiProviderCatalog.All.Select(kind => ToDto(kind, rows.FirstOrDefault(row => row.Kind == kind))).ToList();
    }

    public async Task<AiProviderRuntime?> GetRuntimeAsync(AiProviderKind kind, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.AiProviderConfigs.AsNoTracking().FirstOrDefaultAsync(config => config.Kind == kind, cancellationToken);
        if (row is null || !IsConfigured(row))
        {
            if (row is { IsEnabled: true } && !string.IsNullOrEmpty(row.EncryptedApiKey) && _keyProtector.Unprotect(row.EncryptedApiKey) is null)
            {
                _logger?.LogWarning("The saved API key for {Provider} cannot be decrypted (key ring lost or changed); it must be entered again", kind);
            }

            return null;
        }

        var apiKey = AiProviderCatalog.RequiresKey(kind) ? _keyProtector.Unprotect(row.EncryptedApiKey!) : null;
        return new AiProviderRuntime(kind, row.ModelName, row.BaseUrl, apiKey);
    }

    private AiProviderConfigDto ToDto(AiProviderKind kind, AiProviderConfig? row)
    {
        var baseUrl = kind == AiProviderKind.Ollama ? row?.BaseUrl ?? AiProviderCatalog.DefaultOllamaBaseUrl : null;
        return new AiProviderConfigDto(
            kind,
            AiProviderCatalog.DisplayName(kind),
            AiProviderCatalog.Locality(kind),
            AiProviderCatalog.RequiresKey(kind),
            row?.IsEnabled ?? false,
            string.IsNullOrWhiteSpace(row?.ModelName) ? AiProviderCatalog.DefaultModel(kind) : row!.ModelName,
            baseUrl,
            !string.IsNullOrEmpty(row?.EncryptedApiKey),
            row?.KeyLast4,
            row is not null && IsConfigured(row),
            // A key that is stored but cannot be decrypted (for example the key ring was lost or restored under another
            // account) has to be entered again; the administrator is told so.
            string.IsNullOrEmpty(row?.EncryptedApiKey) || _keyProtector.Unprotect(row.EncryptedApiKey) is not null);
    }

    /// <summary>Enabled, has a model, and either is local with an address or has a key that can still be decrypted.</summary>
    private bool IsConfigured(AiProviderConfig row)
    {
        if (!row.IsEnabled || string.IsNullOrWhiteSpace(row.ModelName))
        {
            return false;
        }

        if (!AiProviderCatalog.RequiresKey(row.Kind))
        {
            return !string.IsNullOrWhiteSpace(row.BaseUrl);
        }

        return !string.IsNullOrEmpty(row.EncryptedApiKey) && _keyProtector.Unprotect(row.EncryptedApiKey) is not null;
    }

    private static string? NormalizeBaseUrl(string? baseUrl) =>
        string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim().TrimEnd('/');
}
