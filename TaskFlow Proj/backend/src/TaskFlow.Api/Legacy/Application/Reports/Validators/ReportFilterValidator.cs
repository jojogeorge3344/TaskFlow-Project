using FluentValidation;

namespace TaskFlow.Application.Reports.Validators;

public class ReportFilterValidator : AbstractValidator<ReportFilter>
{
    public ReportFilterValidator()
    {
        RuleFor(filter => filter.DeadlineTo)
            .GreaterThanOrEqualTo(filter => filter.DeadlineFrom!.Value)
            .WithMessage("The report 'to' deadline must be on or after the 'from' deadline.")
            .When(filter => filter.DeadlineFrom is not null && filter.DeadlineTo is not null);

        RuleFor(filter => filter.Status)
            .IsInEnum()
            .When(filter => filter.Status is not null);

        RuleFor(filter => filter.Priority)
            .IsInEnum()
            .When(filter => filter.Priority is not null);
    }
}
