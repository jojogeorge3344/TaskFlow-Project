using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Authorization;
using TaskFlow.Api.Common.Security;
using TaskFlow.Application.Assistant;
using TaskFlow.Api.Models.Enums;

namespace TaskFlow.Api.Controllers;

/// <summary>TFA, the in-app assistant. Provider configuration is Administrator-only; everything else works on the
/// signed-in user's own conversation and actions.</summary>
[ApiController]
[Route("api/v1/assistant")]
[Authorize]
public class AssistantController : ControllerBase
{
    private readonly IAiProviderConfigService _providers;
    private readonly IChatModelGateway _gateway;
    private readonly IValidator<UpdateAiProvidersRequest> _providersValidator;
    private readonly IAssistantService _assistant;
    private readonly IAssistantActionService _actions;

    public AssistantController(
        IAiProviderConfigService providers,
        IChatModelGateway gateway,
        IValidator<UpdateAiProvidersRequest> providersValidator,
        IAssistantService assistant,
        IAssistantActionService actions)
    {
        _providers = providers;
        _gateway = gateway;
        _providersValidator = providersValidator;
        _assistant = assistant;
        _actions = actions;
    }

    // ---------------------------------------------------------------- conversation

    /// <summary>The caller's current conversation, including the action cards in it.</summary>
    [HttpGet("conversation")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.AssistantChat)]
    public async Task<ActionResult<AssistantConversationDto>> GetConversation(CancellationToken cancellationToken)
    {
        return Ok(await _assistant.GetConversationAsync(new AssistantCaller(User), cancellationToken));
    }

    /// <summary>Sends a message to the chosen provider. A provider failure comes back as HTTP 200 with a
    /// <c>failure</c> object so the panel can show it in the chat; a message that cannot be handled is a 400, and
    /// sending too fast is a 429.</summary>
    [HttpPost("conversation/messages")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.AssistantChat)]
    public async Task<ActionResult<AssistantReplyDto>> SendMessage(
        [FromBody] SendAssistantMessageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _assistant.SendAsync(new AssistantCaller(User), request, cancellationToken));
        }
        catch (AssistantRateLimitedException limited)
        {
            Response.Headers.RetryAfter = ((int)Math.Ceiling(limited.RetryAfter.TotalSeconds)).ToString();
            return Problem(detail: limited.Message, statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests");
        }
        catch (AssistantRequestException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status400BadRequest, title: "TFA could not handle that");
        }
    }

    /// <summary>Starts a new conversation. Earlier messages are no longer used as context.</summary>
    [HttpPost("conversation/new")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.AssistantChat)]
    public async Task<IActionResult> NewConversation(CancellationToken cancellationToken)
    {
        await _assistant.NewConversationAsync(new AssistantCaller(User), cancellationToken);
        return NoContent();
    }

    // ---------------------------------------------------------------- actions

    /// <summary>Edits a prepared mail's recipients, subject and body before it is confirmed.</summary>
    [HttpPut("actions/{id:guid}")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.AssistantActions)]
    public async Task<ActionResult<AssistantPendingActionDto>> EditAction(
        Guid id,
        [FromBody] EditMailActionRequest request,
        CancellationToken cancellationToken)
    {
        return await RunActionAsync(() => _actions.EditMailAsync(new AssistantCaller(User), id, request, cancellationToken));
    }

    /// <summary>Runs a prepared action. This is the only way an action ever runs, and the model cannot call it.</summary>
    [HttpPost("actions/{id:guid}/confirm")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.AssistantActions)]
    public async Task<ActionResult<AssistantActionResultDto>> ConfirmAction(Guid id, CancellationToken cancellationToken)
    {
        return await RunActionAsync(() => _actions.ConfirmAsync(new AssistantCaller(User), id, cancellationToken));
    }

    [HttpPost("actions/{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.AssistantActions)]
    public async Task<ActionResult<AssistantPendingActionDto>> CancelAction(Guid id, CancellationToken cancellationToken)
    {
        return await RunActionAsync(() => _actions.CancelAsync(new AssistantCaller(User), id, cancellationToken));
    }

    private async Task<ActionResult<T>> RunActionAsync<T>(Func<Task<T?>> run)
        where T : class
    {
        try
        {
            var result = await run();
            return result is null ? NotFound() : Ok(result);
        }
        catch (AssistantActionConflictException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status409Conflict, title: "This action can no longer be done");
        }
        catch (AssistantRequestException exception)
        {
            return Problem(detail: exception.Message, statusCode: StatusCodes.Status400BadRequest, title: "TFA could not handle that");
        }
    }

    // ---------------------------------------------------------------- providers

    // ---------------------------------------------------------------- providers

    /// <summary>The providers a user can pick in the chat model selector: configured ones only, with no key details.</summary>
    [HttpGet("providers")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.AssistantChat)]
    public async Task<ActionResult<IReadOnlyList<AiProviderOptionDto>>> GetProviders(CancellationToken cancellationToken)
    {
        return Ok(await _providers.GetAvailableAsync(cancellationToken));
    }

    /// <summary>Every provider with its status for the administrator. The API key is never returned.</summary>
    [HttpGet("providers/config")]
    [RequirePermission(PermissionKeys.SettingsAiView)]
    public async Task<ActionResult<IReadOnlyList<AiProviderConfigDto>>> GetProviderConfig(CancellationToken cancellationToken)
    {
        return Ok(await _providers.GetAdminConfigAsync(cancellationToken));
    }

    /// <summary>Saves provider settings. An empty key keeps the stored key; <c>clearKey</c> removes it.</summary>
    [HttpPut("providers/config")]
    [RequirePermission(PermissionKeys.SettingsAiEdit)]
    public async Task<ActionResult<IReadOnlyList<AiProviderConfigDto>>> UpdateProviderConfig(
        [FromBody] UpdateAiProvidersRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _providersValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        if (!TryGetCallerId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _providers.SaveAsync(userId, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["providers"] = [exception.Message] }));
        }
    }

    /// <summary>Removes a model an administrator added. Built-in models and the one saved for its server stay.</summary>
    [HttpDelete("providers/{kind}/models")]
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

        try
        {
            return Ok(await _providers.RemoveModelAsync(kind, name.Trim(), cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["name"] = [exception.Message] }));
        }
    }

    /// <summary>Sends one tiny request to the provider so an administrator can see whether it works.</summary>
    [HttpPost("providers/{kind}/test")]
    [RequirePermission(PermissionKeys.SettingsAiTest)]
    public async Task<ActionResult<AiProviderTestResultDto>> TestProvider(AiProviderKind kind, CancellationToken cancellationToken)
    {
        var request = new ChatModelRequest(
            "This is a connection test. Reply with the single word OK.",
            [new ChatMessage(AssistantMessageRole.User, "Reply with OK.")],
            [],
            string.Empty);

        try
        {
            await _gateway.CompleteAsync(kind, request, cancellationToken);
            return Ok(new AiProviderTestResultDto(true, "The connection works.", null));
        }
        catch (ProviderNotConfiguredException)
        {
            return Ok(new AiProviderTestResultDto(false, ProviderFailureMessages.NotConfigured(kind), null));
        }
        catch (ProviderFailureException failure)
        {
            return Ok(new AiProviderTestResultDto(false, ProviderFailureMessages.For(kind, failure.Kind, failure.RetryAfter), failure.Kind));
        }
    }

    private bool TryGetCallerId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
