using TaskFlow.Api.Dtos.Management.EmployeeManagement;
using FluentValidation;

namespace TaskFlow.Api.Dtos.Management.EmployeeManagement.Validators;

public class UserStatusRequestValidator : AbstractValidator<UserStatusRequest>
{
    public UserStatusRequestValidator()
    {
    }
}
