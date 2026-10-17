using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using TaskFlow.Application.Email;
using TaskFlow.Application.Settings;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Settings;

namespace TaskFlow.Infrastructure.Email;

/// <summary>Sends email with MailKit: connect, authenticate only when a username is set, send, disconnect.
/// Failures become fixed messages built from the exception type and SMTP status code, never from the raw
/// exception text, so credentials cannot leak into the delivery log or the logs.</summary>
public class MailKitEmailSender : IEmailSender
{
    public const int MaxErrorLength = 1000;

    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<EmailOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailSendResult> SendAsync(
        EmailMessage message,
        SmtpSettings settings,
        CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(settings.SenderName, settings.SenderAddress));
            mime.To.Add(MailboxAddress.Parse(message.ToAddress));
            mime.Subject = message.Subject;
            mime.Body = new TextPart("plain") { Text = message.Body };

            using var client = new SmtpClient { Timeout = (int)timeout.TotalMilliseconds };
            await client.ConnectAsync(settings.Host, settings.Port, MapSecurity(settings.Security), timeoutSource.Token);
            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, timeoutSource.Token);
            }

            await client.SendAsync(mime, timeoutSource.Token);
            await client.DisconnectAsync(quit: true, timeoutSource.Token);
            return EmailSendResult.Sent;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = exception is OperationCanceledException
                ? $"Timed out talking to {settings.Host}:{settings.Port}."
                : DescribeFailure(exception, settings);
            _logger.LogWarning(
                "Sending email via {Host}:{Port} failed: {Error} ({ExceptionType})",
                settings.Host,
                settings.Port,
                error,
                exception.GetType().Name);
            return EmailSendResult.Failed(error);
        }
    }

    /// <summary>A fixed, readable description of a send failure. Built only from the exception type, the SMTP
    /// status code and non-secret settings; the password is scrubbed as a last line of defense.</summary>
    public static string DescribeFailure(Exception exception, SmtpSettings settings)
    {
        var message = exception switch
        {
            AuthenticationException => "Authentication failed: the SMTP server rejected the username or password.",
            SslHandshakeException => "Secure connection failed; check the security mode and port.",
            SmtpCommandException command => $"Server rejected the message (code {(int)command.StatusCode}).",
            SmtpProtocolException => "The SMTP server returned an unexpected response.",
            ParseException => "The sender or recipient address is not a valid email address.",
            SocketException or TimeoutException or IOException => $"Could not connect to {settings.Host}:{settings.Port}.",
            _ => $"Email delivery failed ({exception.GetType().Name})."
        };

        if (!string.IsNullOrEmpty(settings.Password))
        {
            message = message.Replace(settings.Password, "***", StringComparison.Ordinal);
        }

        return message.Length <= MaxErrorLength ? message : message[..MaxErrorLength];
    }

    private static SecureSocketOptions MapSecurity(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.Auto
    };
}
