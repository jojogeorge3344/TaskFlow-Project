using TaskFlow.Api.Common.Exceptions;

namespace TaskFlow.Application.Mail;

/// <summary>A mail rule failed (unknown recipients, attachment limits, replying to a system mail). Carries
/// field errors in the same shape as validation problem details.</summary>
public class MailValidationException : RequestValidationException
{
    public MailValidationException(string field, string message)
        : base(field, message)
    {
    }

    public MailValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(errors)
    {
    }
}
