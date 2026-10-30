using TaskFlow.Api.Dtos.Auth;
using FluentValidation;

namespace TaskFlow.Api.Dtos.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.EmailOrUsername)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MaximumLength(128);
    }
}
