using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Email;

namespace TaskFlow.UnitTests;

public class MailKitEmailSenderTests
{
    private const string Password = "Hunter2-Secret";

    private static readonly SmtpSettings Settings =
        new("smtp.example.com", 587, SmtpSecurity.StartTls, "taskflow@example.com", "TaskFlow", "mailer", Password);

    [Fact]
    public void Authentication_failure_has_a_readable_message()
    {
        var message = MailKitEmailSender.DescribeFailure(new AuthenticationException("535 bad credentials"), Settings);

        Assert.Equal("Authentication failed: the SMTP server rejected the username or password.", message);
    }

    [Fact]
    public void Connection_failure_names_the_server()
    {
        var message = MailKitEmailSender.DescribeFailure(new SocketException((int)SocketError.ConnectionRefused), Settings);

        Assert.Equal("Could not connect to smtp.example.com:587.", message);
    }

    [Fact]
    public void Tls_failure_points_at_security_mode()
    {
        var message = MailKitEmailSender.DescribeFailure(new SslHandshakeException("handshake"), Settings);

        Assert.Equal("Secure connection failed; check the security mode and port.", message);
    }

    [Fact]
    public void Rejected_command_reports_the_status_code_only()
    {
        var exception = new SmtpCommandException(
            SmtpErrorCode.MessageNotAccepted,
            SmtpStatusCode.MailboxUnavailable,
            $"550 mailbox unavailable {Password}");

        var message = MailKitEmailSender.DescribeFailure(exception, Settings);

        Assert.Equal("Server rejected the message (code 550).", message);
    }

    [Fact]
    public void Invalid_address_is_described()
    {
        var message = MailKitEmailSender.DescribeFailure(new ParseException("bad", 0, 0), Settings);

        Assert.Equal("The sender or recipient address is not a valid email address.", message);
    }

    [Fact]
    public void Unknown_failure_never_echoes_exception_text()
    {
        var message = MailKitEmailSender.DescribeFailure(new InvalidOperationException($"login {Password} failed"), Settings);

        Assert.Equal("Email delivery failed (InvalidOperationException).", message);
        Assert.DoesNotContain(Password, message);
    }

    [Fact]
    public void Password_in_a_server_name_is_scrubbed()
    {
        var odd = Settings with { Host = $"{Password}.example.com" };

        var message = MailKitEmailSender.DescribeFailure(new SocketException(), odd);

        Assert.DoesNotContain(Password, message);
        Assert.True(message.Length <= MailKitEmailSender.MaxErrorLength);
    }
}
