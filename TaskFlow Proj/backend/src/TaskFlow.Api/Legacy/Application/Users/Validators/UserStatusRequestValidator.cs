using FluentValidation;

namespace TaskFlow.Application.Users.Validators;

public class UserStatusRequestValidator : AbstractValidator<UserStatusRequest>
{
    public UserStatusRequestValidator()
    {
    }
}
