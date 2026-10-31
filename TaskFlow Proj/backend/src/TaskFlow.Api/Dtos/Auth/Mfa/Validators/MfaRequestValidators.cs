using FluentValidation;

namespace TaskFlow.Api.Dtos.Auth.Mfa.Validators;

public class MfaSetupRequestValidator : AbstractValidator<MfaSetupRequest>
{
    public MfaSetupRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
    }
}

public class MfaEnableRequestValidator : AbstractValidator<MfaEnableRequest>
{
    public MfaEnableRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

public class MfaPasswordCodeRequestValidator : AbstractValidator<MfaPasswordCodeRequest>
{
    public MfaPasswordCodeRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}

public class MfaVerifyRequestValidator : AbstractValidator<MfaVerifyRequest>
{
    public MfaVerifyRequestValidator()
    {
        RuleFor(x => x.ChallengeToken).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
    }
}
