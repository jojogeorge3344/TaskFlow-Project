using TaskFlow.Api.Dtos.Monitoring.TaskManagement;
using FluentValidation;

namespace TaskFlow.Api.Dtos.Monitoring.TaskManagement.Validators;

public class UpdateTaskStatusRequestValidator : AbstractValidator<UpdateTaskStatusRequest>
{
    public UpdateTaskStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .IsInEnum();
    }
}