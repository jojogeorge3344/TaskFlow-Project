using TaskFlow.Api.Dtos.Management.DepartmentManagement;
using FluentValidation;

namespace TaskFlow.Api.Dtos.Management.DepartmentManagement.Validators;

public class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .When(x => x.Description is not null);
    }
}
