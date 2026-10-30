using FluentValidation;

namespace TaskFlow.Application.Tasks.Validators;

public class SetRecipientsRequestValidator : AbstractValidator<SetRecipientsRequest>
{
    public SetRecipientsRequestValidator()
    {
        RuleFor(request => request.RecipientUserIds)
            .NotNull()
            .Must(ids => ids.Count <= 50);

        RuleForEach(request => request.RecipientUserIds)
            .GreaterThan(0);
    }
}