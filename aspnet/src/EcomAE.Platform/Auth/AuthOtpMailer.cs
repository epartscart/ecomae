using MailKit.Security;
using MimeKit;

namespace EcomAE.Platform.Auth;

/// <summary>Sends the sign-in code mail through the effective SMTP settings (PHP <c>epc_auth_smtp_send_html()</c> transport).</summary>
public interface IAuthOtpMailer
{
    /// <summary>Returns null when the mail was accepted, else the transport error text.</summary>
    Task<string?> SendAsync(SmtpEffectiveConfig config, string to, string subject, string html, string text, CancellationToken cancellationToken);
}

public sealed class AuthOtpMailer : IAuthOtpMailer
{
    public async Task<string?> SendAsync(
        SmtpEffectiveConfig config,
        string to,
        string subject,
        string html,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            var from = AuthEmailOtp.PhpTrim(config["from_email"]);
            message.From.Add(new MailboxAddress(config["from_name"], from));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = html, TextBody = text }.ToMessageBody();

            using var client = new MailKit.Net.Smtp.SmtpClient { Timeout = 20_000 };
            var port = (int)AuthEmailOtp.PhpInt(config["smtp_port"]);
            var socket = AuthEmailOtp.PhpLower(AuthEmailOtp.PhpTrim(config["smtp_encryption"])) switch
            {
                "ssl" => SecureSocketOptions.SslOnConnect,
                "tls" => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.StartTlsWhenAvailable,
            };
            await client.ConnectAsync(config["smtp_host"], port, socket, cancellationToken).ConfigureAwait(false);
            var user = AuthEmailOtp.PhpTrim(config["smtp_username"]);
            if (user.Length > 0)
            {
                await client.AuthenticateAsync(user, config["smtp_password"], cancellationToken).ConfigureAwait(false);
            }

            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return string.IsNullOrEmpty(ex.Message) ? ex.GetType().Name : ex.Message;
        }
    }
}
