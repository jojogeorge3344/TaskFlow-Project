namespace TaskFlow.Application.Settings;

/// <summary>Plain-language problems that would stop email from being sent. Shown on the Email Configuration
/// screen and used by the dispatcher to fail an attempt without contacting the server.</summary>
public static class EmailConfigurationCheck
{
    public const string MissingHost = "SMTP server is not set.";
    public const string MissingSender = "Sender address is not set.";
    public const string MissingPassword =
        "A username is set but the SMTP password is not configured on the server. Set Email:SmtpPassword " +
        "with dotnet user-secrets or the Email__SmtpPassword environment variable.";

    public static IReadOnlyList<string> Problems(string? host, string? senderAddress, string? username, bool passwordConfigured)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(host))
        {
            problems.Add(MissingHost);
        }

        if (string.IsNullOrWhiteSpace(senderAddress))
        {
            problems.Add(MissingSender);
        }

        if (!string.IsNullOrWhiteSpace(username) && !passwordConfigured)
        {
            problems.Add(MissingPassword);
        }

        return problems;
    }

    public static IReadOnlyList<string> Problems(SmtpSettings settings) =>
        Problems(settings.Host, settings.SenderAddress, settings.Username, !string.IsNullOrEmpty(settings.Password));
}
