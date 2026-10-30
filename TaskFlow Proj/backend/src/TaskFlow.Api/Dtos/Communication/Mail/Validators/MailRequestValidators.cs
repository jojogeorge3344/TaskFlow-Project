using TaskFlow.Api.Dtos.Communication.Mail;
using TaskFlow.Api.Common.Rules;
using FluentValidation;

namespace TaskFlow.Api.Dtos.Communication.Mail.Validators;

public class SendMailRequestValidator : AbstractValidator<SendMailRequest>
{
    public SendMailRequestValidator()
    {
        RuleFor(request => request.Subject).MaximumLength(MailRules.MaxSubjectLength);
        RuleFor(request => request.Body).MaximumLength(MailRules.MaxBodyLength);

        RuleFor(request => request)
            .Must(request => !string.IsNullOrWhiteSpace(request.Subject) || !string.IsNullOrWhiteSpace(request.Body))
            .WithName("Body")
            .WithMessage("Write a subject or a message before sending.")
            .When(request => request.DraftId is null);

        RuleFor(request => MailRules.NormalizeAddresses(request.To).Count + MailRules.NormalizeAddresses(request.Cc).Count)
            .InclusiveBetween(1, MailRules.MaxRecipients)
            .WithName("To")
            .WithMessage($"Add between 1 and {MailRules.MaxRecipients} recipients.")
            .When(request => request.DraftId is null);

        RuleForEach(request => request.To).Must(BeAnAddress).WithMessage("'{PropertyValue}' is not an email address.");
        RuleForEach(request => request.Cc).Must(BeAnAddress).WithMessage("'{PropertyValue}' is not an email address.");

        RuleFor(request => request.AttachmentIds)
            .Must(ids => ids is null || ids.Distinct().Count() <= MailRules.MaxAttachments)
            .WithMessage($"A mail can have at most {MailRules.MaxAttachments} attachments.");

        RuleFor(request => request.Mode).NotNull().IsInEnum().When(request => request.ParentItemId is not null);
    }

    /// <summary>A loose shape check (something@something, not too long). Whether it is a TaskFlow user is decided
    /// by the service.</summary>
    internal static bool BeAnAddress(string? address)
    {
        var value = address?.Trim() ?? string.Empty;
        var at = value.IndexOf('@');
        return value.Length is > 0 and <= MailRules.MaxAddressLength && at > 0 && at < value.Length - 1 && !value.Contains(' ');
    }
}

public class SaveDraftRequestValidator : AbstractValidator<SaveDraftRequest>
{
    public SaveDraftRequestValidator()
    {
        RuleFor(request => request.Subject).MaximumLength(MailRules.MaxSubjectLength);
        RuleFor(request => request.Body).MaximumLength(MailRules.MaxBodyLength);
        RuleFor(request => request.To).Must(list => list is null || list.Count <= MailRules.MaxRecipients);
        RuleFor(request => request.Cc).Must(list => list is null || list.Count <= MailRules.MaxRecipients);
        RuleForEach(request => request.To).MaximumLength(MailRules.MaxAddressLength);
        RuleForEach(request => request.Cc).MaximumLength(MailRules.MaxAddressLength);

        RuleFor(request => request.AttachmentIds)
            .Must(ids => ids is null || ids.Distinct().Count() <= MailRules.MaxAttachments)
            .WithMessage($"A mail can have at most {MailRules.MaxAttachments} attachments.");

        RuleFor(request => request.Mode).NotNull().IsInEnum().When(request => request.ParentItemId is not null);
    }
}
