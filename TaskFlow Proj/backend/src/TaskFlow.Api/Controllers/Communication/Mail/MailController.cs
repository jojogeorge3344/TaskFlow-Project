using TaskFlow.Api.Services.Interfaces.Communication.Mail.Attachments;
using TaskFlow.Api.Services.Interfaces.Communication.Mail;
using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Rules;
using TaskFlow.Api.Common.Models;
using System.Security.Claims;
using TaskFlow.Api.Common.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Api.Controllers.Communication.Mail;

/// <summary>Internal mail between TaskFlow users. Every endpoint works on the caller's own mailbox; a mail or
/// attachment the caller cannot see is reported as not found.</summary>
[ApiController]
[Route("api/v1/mail")]
[Authorize]
public class MailController : ControllerBase
{
    // Room for one 10 MB file plus multipart overhead.
    private const long UploadRequestLimit = MailRules.MaxAttachmentBytes + 1024 * 1024;

    private readonly IMailService _mailService;
    private readonly IMailAttachmentService _attachmentService;

    public MailController(
        IMailService mailService,
        IMailAttachmentService attachmentService)
    {
        _mailService = mailService;
        _attachmentService = attachmentService;
    }

    /// <summary>One page of a folder, optionally filtered by a search term.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailRead)]
    public async Task<ActionResult<PagedResult<MailListItemDto>>> List(
        [FromQuery] MailView folder = MailView.Inbox,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = MailRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        return await WithCaller(callerId => _mailService.ListAsync(callerId, new MailListQuery(folder, search, page, pageSize), cancellationToken));
    }

    [HttpGet("summary")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailRead)]
    public async Task<ActionResult<MailSummaryDto>> Summary(CancellationToken cancellationToken)
    {
        return await WithCaller(callerId => _mailService.GetSummaryAsync(callerId, cancellationToken));
    }

    /// <summary>Unread Inbox mails, for the menu badge.</summary>
    [HttpGet("unread-count")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailRead)]
    public async Task<ActionResult<MailUnreadCountDto>> UnreadCount(CancellationToken cancellationToken)
    {
        return await WithCaller(async callerId => new MailUnreadCountDto(await _mailService.GetUnreadCountAsync(callerId, cancellationToken)));
    }

    /// <summary>Opens a mail with its conversation and marks it read.</summary>
    [HttpGet("items/{itemId:int}")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailRead)]
    public async Task<ActionResult<MailItemDetailDto>> Open(int itemId, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        var item = await _mailService.OpenAsync(callerId, itemId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPatch("items/{itemId:int}")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailOrganize)]
    public async Task<IActionResult> UpdateFlags(int itemId, [FromBody] UpdateMailFlagsRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        return await _mailService.UpdateFlagsAsync(callerId, itemId, request, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPost("items/{itemId:int}/trash")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailOrganize)]
    public async Task<IActionResult> Trash(int itemId, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        return await _mailService.TrashAsync(callerId, itemId, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPost("items/{itemId:int}/restore")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailOrganize)]
    public async Task<IActionResult> Restore(int itemId, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        return await _mailService.RestoreAsync(callerId, itemId, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>Deletes a mail forever from Trash, or discards a draft.</summary>
    [HttpDelete("items/{itemId:int}")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailOrganize)]
    public async Task<IActionResult> DeleteForever(int itemId, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        return await _mailService.DeleteForeverAsync(callerId, itemId, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>Prefilled recipients, subject and quoted text for a reply, reply all or forward.</summary>
    [HttpGet("items/{itemId:int}/compose")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailCompose)]
    public async Task<ActionResult<MailComposeDto>> Compose(int itemId, [FromQuery] MailComposeMode mode, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        var compose = await _mailService.GetComposeAsync(callerId, itemId, mode, cancellationToken);
        return compose is null ? NotFound() : Ok(compose);
    }

    /// <summary>Sends a new mail, a reply or forward, or a saved draft to TaskFlow users.</summary>
    [HttpPost("send")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailCompose)]
    public async Task<ActionResult<MailItemDetailDto>> Send([FromBody] SendMailRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        var sent = await _mailService.SendAsync(callerId, request, cancellationToken);
        return sent is null ? NotFound() : Ok(sent);
    }

    [HttpPost("drafts")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailCompose)]
    public async Task<ActionResult<MailItemDetailDto>> CreateDraft([FromBody] SaveDraftRequest request, CancellationToken cancellationToken)
    {
        return await SaveDraft(request, (callerId, body) => _mailService.CreateDraftAsync(callerId, body, cancellationToken), cancellationToken);
    }

    [HttpPut("drafts/{messageId:int}")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailCompose)]
    public async Task<ActionResult<MailItemDetailDto>> UpdateDraft(int messageId, [FromBody] SaveDraftRequest request, CancellationToken cancellationToken)
    {
        return await SaveDraft(request, (callerId, body) => _mailService.UpdateDraftAsync(callerId, messageId, body, cancellationToken), cancellationToken);
    }

    /// <summary>Active TaskFlow users matching a name or email, for the recipient picker.</summary>
    [HttpGet("contacts")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailCompose)]
    public async Task<ActionResult<IReadOnlyList<MailContactDto>>> Contacts([FromQuery] string? search, CancellationToken cancellationToken)
    {
        return Ok(await _mailService.GetContactsAsync(search, cancellationToken));
    }

    /// <summary>Uploads one file to attach to a mail or draft.</summary>
    [HttpPost("attachments")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailAttachments)]
    [RequestSizeLimit(UploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadRequestLimit)]
    public async Task<ActionResult<MailAttachmentDto>> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        if (file is null)
        {
            ModelState.AddModelError("File", "Choose a file to upload.");
            return ValidationProblem(ModelState);
        }

        await using var content = file.OpenReadStream();
        return Ok(await _attachmentService.UploadAsync(callerId, content, file.FileName, cancellationToken));
    }

    /// <summary>Downloads an attachment as a file; never rendered inline.</summary>
    [HttpGet("attachments/{attachmentId:int}")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailAttachments)]
    public async Task<IActionResult> Download(int attachmentId, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        var download = await _attachmentService.OpenAsync(callerId, attachmentId, cancellationToken);
        if (download is null)
        {
            return NotFound();
        }

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(download.Content, download.ContentType, download.FileName);
    }

    /// <summary>Removes an upload that is not on a mail or draft.</summary>
    [HttpDelete("attachments/{attachmentId:int}")]
    [Authorize(Policy = PermissionPolicy.Prefix + PermissionKeys.MailAttachments)]
    public async Task<IActionResult> DeleteAttachment(int attachmentId, CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        return await _attachmentService.DeleteAsync(callerId, attachmentId, cancellationToken) ? NoContent() : NotFound();
    }

    private async Task<ActionResult<MailItemDetailDto>> SaveDraft(
        SaveDraftRequest request,
        Func<int, SaveDraftRequest, Task<MailItemDetailDto?>> save,
        CancellationToken cancellationToken)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        var draft = await save(callerId, request);
        return draft is null ? NotFound() : Ok(draft);
    }

    private async Task<ActionResult<T>> WithCaller<T>(Func<int, Task<T>> action)
    {
        if (!TryGetCallerId(out var callerId))
        {
            return Unauthorized();
        }

        return Ok(await action(callerId));
    }

    private bool TryGetCallerId(out int callerId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out callerId);
}
