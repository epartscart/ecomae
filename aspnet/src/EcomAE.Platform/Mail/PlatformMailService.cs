using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Mail;

public interface IPlatformMailService
{
    IReadOnlyList<PlatformMailDomain> ListDomains();
    IReadOnlyList<PlatformMailbox> ListMailboxes(string? siteKey, bool superCp);
    bool HasApiToken();
    string TokenHint();
    SmtpDefaults SmtpFor(string address);
    Task<PlatformMailWriteResult> WriteAsync(PlatformMailWriteRequest request, CancellationToken cancellationToken);
}

public sealed class PlatformMailService : IPlatformMailService
{
    private readonly IPlatformMailDirectory _directory;
    private readonly IPurelymailApi _api;
    private readonly PurelymailOptions _options;

    public PlatformMailService(
        IPlatformMailDirectory directory,
        IPurelymailApi api,
        IOptions<PurelymailOptions> options)
    {
        _directory = directory;
        _api = api;
        _options = options.Value;
    }

    public IReadOnlyList<PlatformMailDomain> ListDomains() => _directory.ListDomains();

    public IReadOnlyList<PlatformMailbox> ListMailboxes(string? siteKey, bool superCp)
        => _directory.ListMailboxes(siteKey, superCp);

    public bool HasApiToken() => _directory.HasApiToken(_options);

    public string TokenHint() => _directory.TokenHint(_options);

    public SmtpDefaults SmtpFor(string address)
        => new(_options.SmtpHost, _options.SmtpPort.ToString(), PurelymailDns.SmtpEncryption, address.Trim().ToLowerInvariant());

    public async Task<PlatformMailWriteResult> WriteAsync(PlatformMailWriteRequest request, CancellationToken cancellationToken)
    {
        var action = (request.Action ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "save_token" => SaveToken(request),
            "add_domain" => await AddDomainAsync(request, cancellationToken).ConfigureAwait(false),
            "refresh_dns" => await RefreshDnsAsync(request, cancellationToken).ConfigureAwait(false),
            "create_mailbox" => await CreateMailboxAsync(request, cancellationToken).ConfigureAwait(false),
            "set_mailbox_password" => SetMailboxPassword(request),
            "delete_mailbox" => await DeleteMailboxAsync(request, cancellationToken).ConfigureAwait(false),
            _ => new PlatformMailWriteResult(false, 0, "unknown_action", "Unknown platform mail action."),
        };
    }

    private PlatformMailWriteResult SaveToken(PlatformMailWriteRequest request)
    {
        var token = (request.ApiToken ?? string.Empty).Trim();
        if (token.Length == 0)
        {
            return new PlatformMailWriteResult(false, 0, "token_required", "Paste the Purelymail API token from purelymail.com/manage/account.");
        }

        _directory.SaveApiToken(token);
        return new PlatformMailWriteResult(true, 1, "ok", "API token saved. It is never shown again.");
    }

    private async Task<PlatformMailWriteResult> AddDomainAsync(PlatformMailWriteRequest request, CancellationToken cancellationToken)
    {
        var domain = PlatformMailNormalize.Domain(request.Domain);
        if (domain.Length == 0 || !domain.Contains('.', StringComparison.Ordinal))
        {
            return new PlatformMailWriteResult(false, 0, "invalid_domain", "Enter a domain such as client.com.");
        }

        var siteKey = PlatformMailNormalize.SiteKey(request.SiteKey, superCpHost: true, requestHost: domain);
        var api = await _api.AddDomainAsync(domain, cancellationToken).ConfigureAwait(false);
        var existing = _directory.FindDomain(domain);
        _directory.UpsertDomain(new PlatformMailDomain(
            domain,
            siteKey,
            existing?.AddedUtc ?? DateTimeOffset.UtcNow,
            FirstPlatform: string.Equals(domain, PurelymailDns.FirstDomain, StringComparison.OrdinalIgnoreCase),
            OwnershipCode: existing?.OwnershipCode ?? string.Empty,
            DnsStatus: api.Ok ? "added" : api.Code,
            ApiSynced: api.Ok));

        if (!api.Ok && api.Code != "token_missing")
        {
            return new PlatformMailWriteResult(false, 1, api.Code, api.Message);
        }

        if (api.Code == "token_missing")
        {
            return new PlatformMailWriteResult(true, 1, "local_only", "Domain saved locally. Set the API token so Super CP can add it on Purelymail.");
        }

        return new PlatformMailWriteResult(true, 1, "ok", "Domain " + domain + " is ready. Add the DNS records, then refresh DNS.");
    }

    private async Task<PlatformMailWriteResult> RefreshDnsAsync(PlatformMailWriteRequest request, CancellationToken cancellationToken)
    {
        var domain = PlatformMailNormalize.Domain(request.Domain);
        var existing = _directory.FindDomain(domain);
        if (existing is null)
        {
            return new PlatformMailWriteResult(false, 0, "unknown_domain", "Add the domain before refreshing DNS.");
        }

        var ownership = await _api.GetOwnershipCodeAsync(cancellationToken).ConfigureAwait(false);
        var recheck = string.IsNullOrWhiteSpace(domain)
            ? PurelymailCallResult.Success("skipped")
            : await _api.RecheckDnsAsync(domain, cancellationToken).ConfigureAwait(false);
        _directory.UpsertDomain(existing with
        {
            OwnershipCode = ownership.Ok ? ownership.Message : existing.OwnershipCode,
            DnsStatus = recheck.Ok ? "rechecked" : recheck.Code,
            ApiSynced = ownership.Ok || recheck.Ok,
        });

        if (!ownership.Ok && ownership.Code == "token_missing")
        {
            return new PlatformMailWriteResult(true, 1, "local_only", "DNS checklist is ready. Set the API token to pull the ownership TXT from Purelymail.");
        }

        if (!ownership.Ok && !recheck.Ok)
        {
            return new PlatformMailWriteResult(false, 1, ownership.Code, ownership.Message);
        }

        return new PlatformMailWriteResult(true, 1, "ok", "Ownership code and DNS recheck updated for " + domain + ".");
    }

    private async Task<PlatformMailWriteResult> CreateMailboxAsync(PlatformMailWriteRequest request, CancellationToken cancellationToken)
    {
        var address = PlatformMailNormalize.IsEmail(request.Address)
            ? request.Address!.Trim().ToLowerInvariant()
            : PlatformMailNormalize.Address(request.LocalPart, request.Domain);
        if (!PlatformMailNormalize.IsEmail(address))
        {
            return new PlatformMailWriteResult(false, 0, "invalid_address", "Enter a mailbox such as ops@client.com.");
        }

        var (local, domain) = PlatformMailNormalize.SplitAddress(address);
        var siteKey = PlatformMailNormalize.SiteKey(request.SiteKey, superCpHost: true, requestHost: domain);
        var password = request.Password ?? string.Empty;
        if (string.IsNullOrWhiteSpace(password) && _directory.FindMailbox(address) is null)
        {
            return new PlatformMailWriteResult(false, 0, "password_required", "Set a mailbox password. It is stored only on this server and never returned.");
        }

        if (_directory.FindDomain(domain) is null)
        {
            _directory.UpsertDomain(new PlatformMailDomain(domain, siteKey, DateTimeOffset.UtcNow));
        }

        var api = PurelymailCallResult.Success("local");
        if (!string.IsNullOrWhiteSpace(password))
        {
            api = await _api.CreateUserAsync(address, password, cancellationToken).ConfigureAwait(false);
        }

        var existing = _directory.FindMailbox(address);
        _directory.UpsertMailbox(new PlatformMailbox(
            address,
            local,
            domain,
            siteKey,
            existing?.CreatedUtc ?? DateTimeOffset.UtcNow,
            FirstWorking: string.Equals(address, PurelymailDns.FirstMailbox, StringComparison.OrdinalIgnoreCase),
            ApiSynced: api.Ok && api.Code != "local",
            LastError: api.Ok ? string.Empty : api.Message)
        {
            Password = password,
        });

        if (!api.Ok && api.Code != "token_missing")
        {
            return new PlatformMailWriteResult(false, 1, api.Code, api.Message);
        }

        if (api.Code == "token_missing")
        {
            return new PlatformMailWriteResult(true, 1, "local_only", "Mailbox saved locally. Set the API token so Super CP can create it on Purelymail.");
        }

        return new PlatformMailWriteResult(true, 1, "ok", "Mailbox " + address + " is ready. Tenant CP and ERP can open it under site_key " + siteKey + ".");
    }

    private PlatformMailWriteResult SetMailboxPassword(PlatformMailWriteRequest request)
    {
        var address = (request.Address ?? string.Empty).Trim().ToLowerInvariant();
        var existing = _directory.FindMailbox(address);
        if (existing is null)
        {
            return new PlatformMailWriteResult(false, 0, "unknown_mailbox", "Unknown mailbox.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return new PlatformMailWriteResult(false, 0, "password_required", "Enter the mailbox password so this tenant can read IMAP.");
        }

        _directory.UpsertMailbox(existing with { LastError = string.Empty, Password = request.Password });
        return new PlatformMailWriteResult(true, 1, "ok", "Password stored for " + address + ". It is never shown again.");
    }

    private async Task<PlatformMailWriteResult> DeleteMailboxAsync(PlatformMailWriteRequest request, CancellationToken cancellationToken)
    {
        var address = (request.Address ?? string.Empty).Trim().ToLowerInvariant();
        if (string.Equals(address, PurelymailDns.FirstMailbox, StringComparison.OrdinalIgnoreCase))
        {
            return new PlatformMailWriteResult(false, 0, "first_protected", "yawer@ecomae.com is the first working platform mailbox and stays.");
        }

        if (_directory.FindMailbox(address) is null)
        {
            return new PlatformMailWriteResult(false, 0, "unknown_mailbox", "Unknown mailbox.");
        }

        var api = await _api.DeleteUserAsync(address, cancellationToken).ConfigureAwait(false);
        _directory.RemoveMailbox(address);
        if (!api.Ok && api.Code != "token_missing")
        {
            return new PlatformMailWriteResult(false, 1, api.Code, api.Message);
        }

        return new PlatformMailWriteResult(true, 1, "ok", "Mailbox " + address + " removed from the platform directory.");
    }
}
