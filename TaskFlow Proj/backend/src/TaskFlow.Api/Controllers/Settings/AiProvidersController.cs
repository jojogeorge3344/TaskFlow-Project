using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Services.Settings.TfaSettings.ConnectionTest;
using TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;

namespace TaskFlow.Api.Controllers.Settings;

/// <summary>TFA Settings: the organisation's AI provider configuration. Every endpoint needs its own TFA Settings
/// permission; the chat model selector's list of providers stays with the assistant.</summary>
[ApiController]
[Route("api/v1/assistant/providers")]
[Authorize]
public class AiProvidersController : ControllerBase
{
    private readonly IAiProviderConfigService _providers;
    private readonly IAiProviderConnectionTester _connectionTester;

    public AiProvidersController(IAiProviderConfigService providers, IAiProviderConnectionTester connectionTester)
    {
        _providers = providers;
        _connectionTester = connectionTester;
    }

    /// <summary>Every provider with its status for the administrator. The API key is never returned.</summary>
    [HttpGet("config")]
    [RequirePermission(PermissionKeys.SettingsAiView)]
    public async Task<ActionResult<IReadOnlyList<AiProviderConfigDto>>> GetProviderConfig(CancellationToken cancellationToken)
    {
        return Ok(await _providers.GetAdminConfigAsync(cancellationToken));
    }

    /// <summary>Saves provider settings. An empty key keeps the stored key; <c>clearKey</c> removes it.</summary>
    [HttpPut("config")]
    [RequirePermission(PermissionKeys.SettingsAiEdit)]
    public async Task<ActionResult<IReadOnlyList<AiProviderConfigDto>>> UpdateProviderConfig(
        [FromBody] UpdateAiProvidersRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        return Ok(await _providers.SaveAsync(userId, request, cancellationToken));
    }

    /// <summary>Removes a model an administrator added. Built-in models and the one saved for its server stay.</summary>
    [HttpDelete("{kind}/models")]
    [RequirePermission(PermissionKeys.SettingsAiRemoveModel)]
    public async Task<ActionResult<IReadOnlyList<AiProviderConfigDto>>> RemoveProviderModel(
        AiProviderKind kind,
        [FromQuery] string? name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["name"] = ["The model name is required."] }));
        }

        return Ok(await _providers.RemoveModelAsync(kind, name.Trim(), cancellationToken));
    }

    /// <summary>Sends one tiny request to the provider so an administrator can see whether it works.</summary>
    [HttpPost("{kind}/test")]
    [RequirePermission(PermissionKeys.SettingsAiTest)]
    public async Task<ActionResult<AiProviderTestResultDto>> TestProvider(AiProviderKind kind, CancellationToken cancellationToken)
    {
        return Ok(await _connectionTester.TestAsync(kind, cancellationToken));
    }
}
