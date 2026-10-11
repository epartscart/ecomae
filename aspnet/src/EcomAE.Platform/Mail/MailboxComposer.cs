using System.Net;
using System.Net.Mail;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EcomAE.Platform.Mail;

public interface IMailboxSmtpClient
{
    Task SendAsync(string from, string password, string to, string subject, string body, CancellationToken cancellationToken);
}

public interface IMailboxComposer
{
    Task<(bool Ok, string Code, string Message)> SendAsync(
        string siteKey,
        bool superCp,
        string? from,
        string? to,
        string? subject,
        string? body,
        CancellationToken cancellationToken,
        string? loginEmail = null,
        bool ownInboxOnly = false);
}

public sealed class MailboxComposer : IMailboxComposer
{
    private readonly IPlatformMailDirectory _directory;
    private readonly IMailboxSmtpClient _smtp;

    public MailboxComposer(IPlatformMailDirectory directory, IMailboxSmtpClient smtp)
    {
        _directory = directory;
        _smtp = smtp;
    }

    public async Task<(bool Ok, string Code, string Message)> SendAsync(
        string siteKey,
        bool superCp,
        string? from,
        string? to,
        string? subject,
        string? body,
        CancellationToken cancellationToken,
        string? loginEmail = null,
        bool ownInboxOnly = false)
    {
        var box = _directory.FindMailbox(from);
        if (box is null)
        {
            return (false, "unknown_mailbox", "That mailbox is not on this platform.");
        }

        if (!superCp && !string.Equals(box.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "isolated", "This Control Panel can only send from mailboxes for its own site_key.");
        }

        if (ownInboxOnly && !string.Equals(box.Address, (loginEmail ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return (false, "own_inbox", "You can only send from your own mailbox.");
        }

        var dest = (to ?? string.Empty).Trim();
        if (!PlatformMailNormalize.IsEmail(dest))
        {
            return (false, "invalid_to", "Enter a destination address.");
        }

        var title = (subject ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            return (false, "subject_required", "Enter a subject.");
        }

        if (!box.HasPassword)
        {
            return (false, "password_missing", "Store the mailbox password before sending.");
        }

        try
        {
            await _smtp.SendAsync(box.Address, box.Password, dest, title, body ?? string.Empty, cancellationToken).ConfigureAwait(false);
            return (true, "ok", "Message sent from " + box.Address + ".");
        }
        catch (Exception ex) when (ex is MailKit.Security.AuthenticationException or MailKit.Net.Smtp.SmtpCommandException or HttpRequestException or InvalidOperationException or SmtpException)
        {
            return (false, "smtp", ex.Message);
        }
    }
}

public sealed class MailKitSmtpClient : IMailboxSmtpClient
{
    private readonly PurelymailOptions _options;

    public MailKitSmtpClient(IOptions<PurelymailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string from, string password, string to, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body ?? string.Empty };
        using var client = new MailKit.Net.Smtp.SmtpClient { Timeout = 20_000 };
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, SecureSocketOptions.StartTls, cancellationToken).ConfigureAwait(false);
        await client.AuthenticateAsync(new NetworkCredential(from, password), cancellationToken).ConfigureAwait(false);
        await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class InMemoryMailboxSmtpClient : IMailboxSmtpClient
{
    public List<(string From, string To, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string from, string password, string to, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("SMTP password missing.");
        }

        Sent.Add((from, to, subject, body));
        return Task.CompletedTask;
    }
}
