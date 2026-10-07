using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using EcomAE.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Cp;

/// <summary>
/// PHP <c>DocpartMailer</c> twin: HTML e-mail through the platform SMTP group of <c>config.php</c>
/// (<c>smtp_host</c>/<c>smtp_port</c>/<c>smtp_encryption</c>/<c>smtp_username</c>/<c>smtp_password</c>, <c>from_email</c>/<c>from_name</c>).
/// </summary>
public interface ICpPlatformMailer
{
    IReadOnlyDictionary<string, string> ReadConfig();

    Task<CpSmsSendOutcome> SendHtmlAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public sealed class CpPlatformMailer : ICpPlatformMailer
{
    private readonly PhpReferenceOptions _reference;

    public CpPlatformMailer(IOptions<PhpReferenceOptions> reference)
    {
        _reference = reference.Value;
    }

    public IReadOnlyDictionary<string, string> ReadConfig()
    {
        var root = (_reference.PhpDocRoot ?? Environment.GetEnvironmentVariable("ECOMAE_PHP_DOCROOT") ?? string.Empty).Trim();
        if (root.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var path = Path.Combine(root, "config.php");
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return PhpConfigFile.Values(PhpConfigFile.Parse(File.ReadAllText(path)));
        }
        catch (IOException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (UnauthorizedAccessException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    public async Task<CpSmsSendOutcome> SendHtmlAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var config = ReadConfig();
        if (!CpCommunicationsDeskService.SmtpComplete(config))
        {
            return CpSmsSendOutcome.Fail("SMTP is not fully configured — fill the e-mail group in Configuration first.");
        }

        var host = config["smtp_host"].Trim();
        if (!int.TryParse(config["smtp_port"].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
        {
            return CpSmsSendOutcome.Fail("SMTP port is invalid.");
        }

        var fromEmail = config["from_email"].Trim();
        if (!CpTenantEmailWriteService.IsEmail(fromEmail))
        {
            return CpSmsSendOutcome.Fail("From e-mail in Configuration is not a valid address.");
        }

        try
        {
            var encryption = CpTenantEmailWriteService.NormalizeEncryption(config["smtp_encryption"]);
            if (encryption == "ssl" && port == 465)
            {
                await SendImplicitTlsAsync(
                    host,
                    port,
                    fromEmail,
                    config["from_name"].Trim(),
                    config["smtp_username"].Trim(),
                    config["smtp_password"],
                    to,
                    subject,
                    htmlBody,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using var mail = new MailMessage
                {
                    From = new MailAddress(fromEmail, config["from_name"].Trim()),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                };
                mail.To.Add(to);

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = encryption is "tls" or "ssl",
                    Timeout = 20_000,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Credentials = new NetworkCredential(config["smtp_username"].Trim(), config["smtp_password"]),
                };
                await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
            }

            return new CpSmsSendOutcome(true, string.Empty);
        }
        catch (SmtpException ex)
        {
            return CpSmsSendOutcome.Fail(ex.Message);
        }
        catch (FormatException ex)
        {
            return CpSmsSendOutcome.Fail(ex.Message);
        }
    }

    private static async Task SendImplicitTlsAsync(
        string host,
        int port,
        string fromEmail,
        string fromName,
        string username,
        string password,
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        await using var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, cancellationToken).ConfigureAwait(false);

        using var reader = new StreamReader(tls, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        using var writer = new StreamWriter(tls, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true,
        };

        await ExpectSmtpAsync(reader, 220, cancellationToken).ConfigureAwait(false);
        await writer.WriteLineAsync("EHLO ecomae".AsMemory(), cancellationToken).ConfigureAwait(false);
        await ExpectSmtpAsync(reader, 250, cancellationToken).ConfigureAwait(false);
        if (username.Length > 0)
        {
            await writer.WriteLineAsync("AUTH LOGIN".AsMemory(), cancellationToken).ConfigureAwait(false);
            await ExpectSmtpAsync(reader, 334, cancellationToken).ConfigureAwait(false);
            await writer.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(username)).AsMemory(), cancellationToken).ConfigureAwait(false);
            await ExpectSmtpAsync(reader, 334, cancellationToken).ConfigureAwait(false);
            await writer.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(password)).AsMemory(), cancellationToken).ConfigureAwait(false);
            await ExpectSmtpAsync(reader, 235, cancellationToken).ConfigureAwait(false);
        }

        await writer.WriteLineAsync(("MAIL FROM:<" + fromEmail + ">").AsMemory(), cancellationToken).ConfigureAwait(false);
        await ExpectSmtpAsync(reader, 250, cancellationToken).ConfigureAwait(false);
        await writer.WriteLineAsync(("RCPT TO:<" + to + ">").AsMemory(), cancellationToken).ConfigureAwait(false);
        await ExpectSmtpAsync(reader, 250, cancellationToken).ConfigureAwait(false);
        await writer.WriteLineAsync("DATA".AsMemory(), cancellationToken).ConfigureAwait(false);
        await ExpectSmtpAsync(reader, 354, cancellationToken).ConfigureAwait(false);
        var safeSubject = subject.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        var safeFromName = fromName.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
        await writer.WriteAsync((
            "From: " + safeFromName + " <" + fromEmail + ">\r\n"
            + "To: " + to + "\r\n"
            + "Subject: " + safeSubject + "\r\n"
            + "MIME-Version: 1.0\r\n"
            + "Content-Type: text/html; charset=utf-8\r\n"
            + "\r\n"
            + body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal)
            + "\r\n.\r\n").AsMemory(), cancellationToken).ConfigureAwait(false);
        await ExpectSmtpAsync(reader, 250, cancellationToken).ConfigureAwait(false);
        await writer.WriteLineAsync("QUIT".AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExpectSmtpAsync(StreamReader reader, int expected, CancellationToken cancellationToken)
    {
        var response = string.Empty;
        while (true)
        {
            response = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
            if (response.Length < 3 || !int.TryParse(response[..3], NumberStyles.None, CultureInfo.InvariantCulture, out var code))
            {
                throw new SmtpException("SMTP returned an invalid response.");
            }

            if (response.Length < 4 || response[3] == ' ')
            {
                if (code != expected)
                {
                    throw new SmtpException("SMTP returned " + code.ToString(CultureInfo.InvariantCulture) + "; expected " + expected.ToString(CultureInfo.InvariantCulture) + ".");
                }

                return;
            }
        }
    }

}
