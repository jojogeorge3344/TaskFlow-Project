using TaskFlow.Api.Dtos.Monitoring.TaskManagement;
using FluentValidation;

namespace TaskFlow.Api.Dtos.Monitoring.TaskManagement.Validators;

public class ReassignTaskRequestValidator : AbstractValidator<ReassignTaskRequest>
{
    public ReassignTaskRequestValidator()
    {
        RuleFor(request => request.NewAssigneeUserId)
            .GreaterThan(0);

        RuleFor(request => request.Reason)
            .NotEmpty()
            .MaximumLength(500);
    }
}