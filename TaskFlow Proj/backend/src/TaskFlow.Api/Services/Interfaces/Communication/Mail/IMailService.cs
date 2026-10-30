using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Models;
using TaskFlow.Api.Common.Exceptions;

namespace TaskFlow.Api.Services.Interfaces.Communication.Mail;

/// <summary>Internal mail between TaskFlow users. Every method works on the caller's own mailbox: an item or draft
/// that is not the caller's is reported as not found (null), never as forbidden. Rule failures throw
/// <see cref="MailValidationException"/>.</summary>
public interface IMailService
{
    /// <summary>Sends a new mail, a reply or forward, or a saved draft. Returns the sender's Sent item, or null when
    /// the draft or parent item is not the caller's.</summary>
    Task<MailItemDetailDto?> SendAsync(int callerId, SendMailRequest request, CancellationToken cancellationToken = default);

    Task<MailItemDetailDto?> CreateDraftAsync(int callerId, SaveDraftRequest request, CancellationToken cancellationToken = default);

    Task<MailItemDetailDto?> UpdateDraftAsync(int callerId, int draftId, SaveDraftRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<MailListItemDto>> ListAsync(int callerId, MailListQuery query, CancellationToken cancellationToken = default);

    Task<MailSummaryDto> GetSummaryAsync(int callerId, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int callerId, CancellationToken cancellationToken = default);

    /// <summary>Opens an item with its conversation and marks it read.</summary>
    Task<MailItemDetailDto?> OpenAsync(int callerId, int itemId, CancellationToken cancellationToken = default);

    Task<bool> UpdateFlagsAsync(int callerId, int itemId, UpdateMailFlagsRequest request, CancellationToken cancellationToken = default);

    Task<bool> TrashAsync(int callerId, int itemId, CancellationToken cancellationToken = default);

    Task<bool> RestoreAsync(int callerId, int itemId, CancellationToken cancellationToken = default);

    /// <summary>Deletes an item forever (from Trash) or discards a draft. Throws <see cref="InvalidOperationException"/>
    /// for an item that is neither trashed nor a draft.</summary>
    Task<bool> DeleteForeverAsync(int callerId, int itemId, CancellationToken cancellationToken = default);

    Task<MailComposeDto?> GetComposeAsync(int callerId, int itemId, MailComposeMode mode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MailContactDto>> GetContactsAsync(string? search, CancellationToken cancellationToken = default);
}
