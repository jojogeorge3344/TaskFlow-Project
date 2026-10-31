using TaskFlow.Api.Services.Assistant.Rules;
using TaskFlow.Api.Common.Exceptions;
using TaskFlow.Api.Services.Assistant;
using TaskFlow.Api.Dtos.Assistant;
using TaskFlow.Api.Services.Settings.TfaSettings;
using TaskFlow.Api.Dtos.Settings.TfaSettings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Common.Security;

namespace TaskFlow.Api.Controllers.Assistant;

/// <summary>TFA, the in-app assistant: the signed-in user's own conversation and actions, and the providers they may
/// chat with. Provider configuration is in Settings (AiProvidersController).</summary>
[ApiController]
[Route("api/v1/assistant")]
[Authorize]
public class AssistantController : ControllerBase
{
    private readonly IAiProviderConfigService _providers;
    private readonly IAssistantService _assistant;
    private readonly IAssistantActionService _actions;

    public AssistantController(
        IAiProviderConfigService providers,
        IAssistantService assistant,
        IAssistantActionService actions)
    {
        _providers = providers;
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
        return Ok(await _assistant.SendAsync(new AssistantCaller(User), request, cancellationToken));
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
        var result = await run();
        return result is null ? NotFound() : Ok(result);
    }

    // ---------------------------------------------------------------- providers

    /// <summary>The providers a user can pick in the chat model selector: configured ones only, with no key details.</summary>
    [HttpGet("providers")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.AssistantChat)]
    public async Task<ActionResult<IReadOnlyList<AiProviderOptionDto>>> GetProviders(CancellationToken cancellationToken)
    {
        return Ok(await _providers.GetAvailableAsync(cancellationToken));
    }
}
