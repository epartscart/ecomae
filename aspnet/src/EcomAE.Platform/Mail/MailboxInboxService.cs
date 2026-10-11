using System.Net;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Mail;

public interface IMailboxImapClient
{
    Task<IReadOnlyList<MailboxMessage>> FetchAsync(string address, string password, int limit, CancellationToken cancellationToken);
}

public interface IMailboxInboxService
{
    IReadOnlyList<PlatformMailbox> ListForCaller(string siteKey, bool superCp);
    Task<(bool Ok, string Code, string Message, IReadOnlyList<MailboxMessage> Messages)> ReadAsync(
        string siteKey,
        bool superCp,
        string? address,
        int limit,
        CancellationToken cancellationToken);
}

public sealed class MailboxInboxService : IMailboxInboxService
{
    private readonly IPlatformMailDirectory _directory;
    private readonly IMailboxImapClient _imap;

    public MailboxInboxService(IPlatformMailDirectory directory, IMailboxImapClient imap)
    {
        _directory = directory;
        _imap = imap;
    }

    public IReadOnlyList<PlatformMailbox> ListForCaller(string siteKey, bool superCp)
        => _directory.ListMailboxes(siteKey, superCp);

    public async Task<(bool Ok, string Code, string Message, IReadOnlyList<MailboxMessage> Messages)> ReadAsync(
        string siteKey,
        bool superCp,
        string? address,
        int limit,
        CancellationToken cancellationToken)
    {
        var box = _directory.FindMailbox(address);
        if (box is null)
        {
            return (false, "unknown_mailbox", "That mailbox is not on this platform.", []);
        }

        if (!superCp && !string.Equals(box.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "isolated", "This Control Panel can only open mailboxes for its own site_key.", []);
        }

        if (!box.HasPassword)
        {
            return (false, "password_missing", "Super CP must store the mailbox password before this inbox can be read.", []);
        }

        var cap = Math.Clamp(limit <= 0 ? 25 : limit, 1, 100);
        try
        {
            var messages = await _imap.FetchAsync(box.Address, box.Password, cap, cancellationToken).ConfigureAwait(false);
            return (true, "ok", messages.Count == 0 ? "Inbox is empty." : "Inbox loaded.", messages);
        }
        catch (Exception ex) when (ex is MailKit.Security.AuthenticationException or MailKit.Net.Imap.ImapProtocolException or HttpRequestException or InvalidOperationException)
        {
            return (false, "imap", ex.Message, []);
        }
    }
}

public sealed class MailKitImapClient : IMailboxImapClient
{
    private readonly PurelymailOptions _options;

    public MailKitImapClient(IOptions<PurelymailOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IReadOnlyList<MailboxMessage>> FetchAsync(string address, string password, int limit, CancellationToken cancellationToken)
    {
        using var client = new ImapClient { Timeout = 20_000 };
        await client.ConnectAsync(_options.ImapHost, _options.ImapPort, SecureSocketOptions.SslOnConnect, cancellationToken).ConfigureAwait(false);
        await client.AuthenticateAsync(new NetworkCredential(address, password), cancellationToken).ConfigureAwait(false);
        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken).ConfigureAwait(false);
        var uids = await inbox.SearchAsync(SearchQuery.All, cancellationToken).ConfigureAwait(false);
        var take = uids.Count <= limit ? uids : uids.Skip(uids.Count - limit).ToArray();
        var rows = new List<MailboxMessage>(take.Count);
        foreach (var uid in take.Reverse())
        {
            var message = await inbox.GetMessageAsync(uid, cancellationToken).ConfigureAwait(false);
            var text = message.TextBody ?? message.HtmlBody ?? string.Empty;
            if (text.Length > 280)
            {
                text = text[..280];
            }

            rows.Add(new MailboxMessage(
                uid.Id.ToString(),
                message.From.ToString(),
                message.To.ToString(),
                message.Subject ?? string.Empty,
                message.Date.ToUniversalTime(),
                text,
                false));
        }

        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
        return rows;
    }
}

public sealed class InMemoryMailboxImapClient : IMailboxImapClient
{
    public Dictionary<string, List<MailboxMessage>> Boxes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryMailboxImapClient()
    {
        Boxes[PurelymailDns.FirstMailbox] =
        [
            new MailboxMessage(
                "1",
                "Purelymail <noreply@purelymail.com>",
                PurelymailDns.FirstMailbox,
                "yawer@ecomae.com is the first working platform mailbox",
                DateTimeOffset.UtcNow,
                "Unlimited domains are ready. Super CP / BOS can add any tenant domain and mailbox. Tenants read only their own site_key inbox.",
                false),
        ];
    }

    public Task<IReadOnlyList<MailboxMessage>> FetchAsync(string address, string password, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("IMAP password missing.");
        }

        if (!Boxes.TryGetValue(address, out var rows))
        {
            return Task.FromResult<IReadOnlyList<MailboxMessage>>([]);
        }

        return Task.FromResult<IReadOnlyList<MailboxMessage>>(rows.Take(limit).ToArray());
    }
}
