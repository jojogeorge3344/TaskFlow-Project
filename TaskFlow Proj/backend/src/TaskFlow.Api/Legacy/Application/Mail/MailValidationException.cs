namespace TaskFlow.Application.Mail;

/// <summary>A mail rule failed (unknown recipients, attachment limits, replying to a system mail). Carries
/// field errors in the same shape as validation problem details.</summary>
public class MailValidationException : Exception
{
    public MailValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public MailValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base(string.Join(" ", errors.SelectMany(error => error.Value)))
    {
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
