using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TaskFlow.Api.Common.Security;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Services.Settings.TfaSettings.ConnectionTest;
using TaskFlow.Api.Services.Settings.TfaSettings.ModelAddition;
using TaskFlow.Api.Services.Settings.TfaSettings.ProviderConfig;
using TaskFlow.Api.Authorization.Policies;

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
    private readonly IAiModelAdditionService _modelAddition;
    private readonly IAuthorizationService _authorization;

    public AiProvidersController(
        IAiProviderConfigService providers,
        IAiProviderConnectionTester connectionTester,
        IAiModelAdditionService modelAddition,
        IAuthorizationService authorization)
    {
        _providers = providers;
        _connectionTester = connectionTester;
        _modelAddition = modelAddition;
        _authorization = authorization;
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

    /// <summary>Removes a model an administrator added. Built-in models and the one TFA uses stay.</summary>
    [HttpDelete("{providerId:int}/models")]
    [RequirePermission(PermissionKeys.SettingsAiRemoveModel)]
    public async Task<ActionResult<IReadOnlyList<AiProviderConfigDto>>> RemoveProviderModel(
        int providerId,
        [FromQuery] string? name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["name"] = ["The model name is required."] }));
        }

        return Ok(await _providers.RemoveModelAsync(providerId, name.Trim(), cancellationToken));
    }

    /// <summary>The Add AI model page: tests the typed model, key or address on an existing provider (<c>providerId</c>)
    /// or a new one (<c>newProvider</c>) and, only when the test passes, saves the model (creating the provider when it is
    /// new) and makes it the one TFA uses. A failed test returns 200 with <c>ok: false</c> and saves nothing. Needs both
    /// settings.ai.edit and settings.ai.test (the permission attribute takes one key, so the second is checked here).</summary>
    [HttpPost("models")]
    [RequirePermission(PermissionKeys.SettingsAiEdit)]
    public async Task<ActionResult<AddAiModelResultDto>> AddModel(
        [FromBody] AddAiModelRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        if (!(await _authorization.AuthorizeAsync(User, PermissionPolicy.NameFor(PermissionKeys.SettingsAiTest))).Succeeded)
        {
            return Forbid();
        }

        return Ok(await _modelAddition.AddAndActivateAsync(userId, request, cancellationToken));
    }

    /// <summary>Edits a model an administrator added: tests the typed values on the target provider (the same one, another
    /// existing one, or a new one) and, only when the test passes, renames or moves the model and saves the key or address.
    /// A built-in model and the one TFA uses are refused. Like adding, it needs both settings.ai.edit and settings.ai.test.</summary>
    [HttpPut("{providerId:int}/models")]
    [RequirePermission(PermissionKeys.SettingsAiEdit)]
    public async Task<ActionResult<AddAiModelResultDto>> EditModel(
        int providerId,
        [FromQuery] string? name,
        [FromBody] AddAiModelRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        if (!(await _authorization.AuthorizeAsync(User, PermissionPolicy.NameFor(PermissionKeys.SettingsAiTest))).Succeeded)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["name"] = ["The model name is required."] }));
        }

        return Ok(await _modelAddition.EditAsync(userId, providerId, name.Trim(), request, cancellationToken));
    }

    /// <summary>"Use this model": makes a listed model the one TFA uses and switches the other providers off.</summary>
    [HttpPost("{providerId:int}/models/use")]
    [RequirePermission(PermissionKeys.SettingsAiEdit)]
    public async Task<ActionResult<IReadOnlyList<AiProviderConfigDto>>> UseModel(
        int providerId,
        [FromBody] AiModelChoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ModelName))
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["modelName"] = ["The model name is required."] }));
        }

        return Ok(await _providers.UseModelAsync(userId, providerId, request.ModelName, cancellationToken));
    }

    /// <summary>Sends one tiny request to the provider so an administrator can see whether it works: with the saved
    /// settings, or, when a model name is sent, with that listed model and the saved key or address.</summary>
    [HttpPost("{providerId:int}/test")]
    [RequirePermission(PermissionKeys.SettingsAiTest)]
    public async Task<ActionResult<AiProviderTestResultDto>> TestProvider(
        int providerId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AiModelChoiceRequest? request,
        CancellationToken cancellationToken)
    {
        return Ok(string.IsNullOrWhiteSpace(request?.ModelName)
            ? await _connectionTester.TestAsync(providerId, cancellationToken)
            : await _modelAddition.TestSavedModelAsync(providerId, request.ModelName, cancellationToken));
    }
}
