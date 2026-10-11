using EcomAE.Platform.Services;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Mail;

public interface IPlatformMailService
{
    IReadOnlyList<PlatformMailDomain> ListDomains();
    IReadOnlyList<PlatformMailbox> ListMailboxes(string? siteKey, bool superCp);
    bool HasApiToken();
    string TokenHint();
    SmtpDefaults SmtpFor(string address);
    IReadOnlyList<TenantMailTarget> PlanTenantDomains(IReadOnlyList<(string Hostname, string SiteKey)> tenants);
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
            "setup_tenants" => await SetupTenantsAsync(request, cancellationToken).ConfigureAwait(false),
            "setup_business" => await SetupBusinessAsync(request, cancellationToken).ConfigureAwait(false),
            "register_login" => await RegisterLoginAsync(request, cancellationToken).ConfigureAwait(false),
            "delete_mailbox" => await DeleteMailboxAsync(request, cancellationToken).ConfigureAwait(false),
            _ => new PlatformMailWriteResult(false, 0, "unknown_action", "Unknown platform mail action."),
        };
    }

    public IReadOnlyList<TenantMailTarget> PlanTenantDomains(IReadOnlyList<(string Hostname, string SiteKey)> tenants)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<TenantMailTarget>();
        foreach (var (hostname, siteKey) in tenants)
        {
            var domain = PlatformMailNormalize.Domain(hostname);
            if (domain.Length == 0 || !domain.Contains('.', StringComparison.Ordinal) || !seen.Add(domain))
            {
                continue;
            }

            if (PlatformHostPolicy.IsSuperCpHost(domain) || string.Equals(domain, PurelymailDns.FirstDomain, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = PlatformMailNormalize.SiteKey(siteKey, superCpHost: true, requestHost: domain);
            rows.Add(new TenantMailTarget(domain, key, hostname, _directory.FindDomain(domain) is not null));
        }

        return rows;
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

    private async Task<PlatformMailWriteResult> SetupTenantsAsync(PlatformMailWriteRequest request, CancellationToken cancellationToken)
    {
        var plan = PlanTenantDomains(request.Tenants ?? []);
        if (plan.Count == 0)
        {
            return new PlatformMailWriteResult(true, 0, "empty_fleet", "No extra tenant domains to add. ecomae.com is already the platform domain.");
        }

        var writes = 0;
        var added = 0;
        foreach (var row in plan)
        {
            if (row.DomainReady)
            {
                continue;
            }

            var result = await AddDomainAsync(new PlatformMailWriteRequest("add_domain", Domain: row.Domain, SiteKey: row.SiteKey), cancellationToken).ConfigureAwait(false);
            writes += result.Writes;
            if (result.Succeeded)
            {
                added++;
            }
        }

        return new PlatformMailWriteResult(
            true,
            writes,
            added == 0 ? "ok" : "ok",
            added == 0
                ? "Every tenant domain is already on the platform mail directory."
                : "Added " + added.ToString(System.Globalization.CultureInfo.InvariantCulture) + " tenant domain(s). Publish DNS, then create each login mailbox.");
    }

    private async Task<PlatformMailWriteResult> SetupBusinessAsync(PlatformMailWriteRequest request, CancellationToken cancellationToken)
    {
        var domain = PlatformMailNormalize.Domain(string.IsNullOrWhiteSpace(request.Domain) ? PurelymailDns.FirstDomain : request.Domain);
        var siteKey = PlatformMailNormalize.SiteKey(request.SiteKey, superCpHost: true, requestHost: domain);
        if (_directory.FindDomain(domain) is null)
        {
            _directory.UpsertDomain(new PlatformMailDomain(domain, siteKey, DateTimeOffset.UtcNow, FirstPlatform: string.Equals(domain, PurelymailDns.FirstDomain, StringComparison.OrdinalIgnoreCase)));
        }

        var created = 0;
        var writes = 0;
        foreach (var local in PurelymailDns.BusinessLocals)
        {
            var address = PlatformMailNormalize.Address(local, domain);
            var password = string.IsNullOrWhiteSpace(request.Password)
                ? PlatformMailSecrets.NewMailboxPassword()
                : request.Password;
            var result = await CreateMailboxAsync(
                new PlatformMailWriteRequest("create_mailbox", Domain: domain, SiteKey: siteKey, LocalPart: local, Address: address, Password: password),
                cancellationToken).ConfigureAwait(false);
            writes += result.Writes;
            if (result.Succeeded)
            {
                created++;
            }
        }

        var info = PlatformMailNormalize.Address("info", domain);
        var routed = 0;
        foreach (var alias in PurelymailDns.AliasLocals.Append(""))
        {
            var rule = await _api.CreateRoutingRuleAsync(domain, alias, info, cancellationToken).ConfigureAwait(false);
            writes++;
            if (rule.Ok)
            {
                routed++;
            }
        }

        return new PlatformMailWriteResult(
            true,
            writes,
            "ok",
            "Business mailboxes ready on "
            + domain
            + ": "
            + created.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " boxes, "
            + routed.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " routing aliases to info@"
            + domain
            + ".");
    }

    private async Task<PlatformMailWriteResult> RegisterLoginAsync(PlatformMailWriteRequest request, CancellationToken cancellationToken)
    {
        var address = (request.Address ?? string.Empty).Trim().ToLowerInvariant();
        if (!PlatformMailNormalize.IsEmail(address))
        {
            return new PlatformMailWriteResult(false, 0, "invalid_address", "Enter the tenant login email to register as a mailbox.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            var (local, domain) = PlatformMailNormalize.SplitAddress(address);
            var siteKey = PlatformMailNormalize.SiteKey(request.SiteKey, superCpHost: true, requestHost: domain);
            if (_directory.FindDomain(domain) is null)
            {
                _directory.UpsertDomain(new PlatformMailDomain(domain, siteKey, DateTimeOffset.UtcNow));
            }

            var existing = _directory.FindMailbox(address);
            _directory.UpsertMailbox(new PlatformMailbox(
                address,
                local,
                domain,
                siteKey,
                existing?.CreatedUtc ?? DateTimeOffset.UtcNow,
                FirstWorking: string.Equals(address, PurelymailDns.FirstMailbox, StringComparison.OrdinalIgnoreCase)));
            return new PlatformMailWriteResult(true, 1, "local_only", "Login mailbox " + address + " is listed for site_key " + siteKey + ". Store the password so CP/ERP can read IMAP.");
        }

        return await CreateMailboxAsync(request, cancellationToken).ConfigureAwait(false);
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
