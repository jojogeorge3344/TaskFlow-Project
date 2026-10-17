namespace TaskFlow.Domain.Enums;

/// <summary>How the SMTP connection is secured.</summary>
public enum SmtpSecurity
{
    None,
    StartTls,
    SslOnConnect,
    Auto
}
