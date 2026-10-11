using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Mail;

public interface IPlatformMailDirectory
{
    IReadOnlyList<PlatformMailDomain> ListDomains();
    IReadOnlyList<PlatformMailbox> ListMailboxes(string? siteKey, bool superCp);
    PlatformMailbox? FindMailbox(string? address);
    PlatformMailDomain? FindDomain(string? domain);
    void UpsertDomain(PlatformMailDomain domain);
    void UpsertMailbox(PlatformMailbox mailbox);
    bool RemoveMailbox(string address);
    bool HasApiToken(PurelymailOptions options);
    string ResolveApiToken(PurelymailOptions options);
    void SaveApiToken(string token);
    string TokenHint(PurelymailOptions options);
}

public sealed class PlatformMailDirectory : IPlatformMailDirectory
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly object _gate = new();
    private readonly string? _persistPath;
    private readonly List<PlatformMailDomain> _domains = [];
    private readonly List<PlatformMailbox> _mailboxes = [];
    private string _apiToken = string.Empty;

    public PlatformMailDirectory(IHostEnvironment? hostEnvironment = null, string? persistPath = null)
    {
        _persistPath = persistPath ?? (hostEnvironment is null
            ? null
            : Path.Combine(hostEnvironment.ContentRootPath, "App_Data", "platform-mail.json"));
        if (!TryLoad())
        {
            SeedFirstMailbox();
            Persist();
        }
        else
        {
            EnsureFirstMailbox();
        }
    }

    public IReadOnlyList<PlatformMailDomain> ListDomains()
    {
        lock (_gate)
        {
            return _domains
                .OrderByDescending(d => d.FirstPlatform)
                .ThenBy(d => d.Domain, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public IReadOnlyList<PlatformMailbox> ListMailboxes(string? siteKey, bool superCp)
    {
        lock (_gate)
        {
            IEnumerable<PlatformMailbox> rows = _mailboxes;
            if (!superCp)
            {
                var key = (siteKey ?? string.Empty).Trim();
                rows = rows.Where(m => string.Equals(m.SiteKey, key, StringComparison.OrdinalIgnoreCase));
            }

            return rows
                .OrderByDescending(m => m.FirstWorking)
                .ThenBy(m => m.Address, StringComparer.Ordinal)
                .Select(m => m.PublicView())
                .ToArray();
        }
    }

    public PlatformMailbox? FindMailbox(string? address)
    {
        var key = (address ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Length == 0)
        {
            return null;
        }

        lock (_gate)
        {
            return _mailboxes.FirstOrDefault(m => string.Equals(m.Address, key, StringComparison.OrdinalIgnoreCase));
        }
    }

    public PlatformMailDomain? FindDomain(string? domain)
    {
        var key = PlatformMailNormalize.Domain(domain);
        if (key.Length == 0)
        {
            return null;
        }

        lock (_gate)
        {
            return _domains.FirstOrDefault(d => string.Equals(d.Domain, key, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void UpsertDomain(PlatformMailDomain domain)
    {
        lock (_gate)
        {
            var idx = _domains.FindIndex(d => string.Equals(d.Domain, domain.Domain, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _domains[idx] = domain;
            }
            else
            {
                _domains.Add(domain);
            }

            Persist();
        }
    }

    public void UpsertMailbox(PlatformMailbox mailbox)
    {
        lock (_gate)
        {
            var idx = _mailboxes.FindIndex(m => string.Equals(m.Address, mailbox.Address, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                var keepPassword = string.IsNullOrWhiteSpace(mailbox.Password)
                    ? _mailboxes[idx].Password
                    : mailbox.Password;
                _mailboxes[idx] = mailbox with
                {
                    Password = keepPassword,
                    PasswordStored = !string.IsNullOrWhiteSpace(keepPassword),
                };
            }
            else
            {
                _mailboxes.Add(mailbox with { PasswordStored = !string.IsNullOrWhiteSpace(mailbox.Password) });
            }

            Persist();
        }
    }

    public bool RemoveMailbox(string address)
    {
        var key = address.Trim().ToLowerInvariant();
        lock (_gate)
        {
            var removed = _mailboxes.RemoveAll(m => string.Equals(m.Address, key, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                Persist();
            }

            return removed;
        }
    }

    public bool HasApiToken(PurelymailOptions options) => !string.IsNullOrWhiteSpace(ResolveApiToken(options));

    public string ResolveApiToken(PurelymailOptions options)
    {
        var env = Environment.GetEnvironmentVariable(PurelymailOptions.EnvTokenName);
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.ApiToken))
        {
            return options.ApiToken.Trim();
        }

        lock (_gate)
        {
            return _apiToken;
        }
    }

    public void SaveApiToken(string token)
    {
        lock (_gate)
        {
            _apiToken = (token ?? string.Empty).Trim();
            Persist();
        }
    }

    public string TokenHint(PurelymailOptions options)
    {
        var token = ResolveApiToken(options);
        if (string.IsNullOrWhiteSpace(token))
        {
            return "not set";
        }

        return token.Length <= 4 ? "set ·••••" : "set ·•••" + token[^4..];
    }

    private void SeedFirstMailbox()
    {
        _domains.Add(new PlatformMailDomain(
            PurelymailDns.FirstDomain,
            PurelymailDns.PlatformSiteKey,
            DateTimeOffset.UtcNow,
            FirstPlatform: true,
            DnsStatus: "first platform domain"));
        _mailboxes.Add(new PlatformMailbox(
            PurelymailDns.FirstMailbox,
            "yawer",
            PurelymailDns.FirstDomain,
            PurelymailDns.PlatformSiteKey,
            DateTimeOffset.UtcNow,
            FirstWorking: true,
            ApiSynced: true));
    }

    private void EnsureFirstMailbox()
    {
        lock (_gate)
        {
            if (FindDomain(PurelymailDns.FirstDomain) is null)
            {
                _domains.Add(new PlatformMailDomain(
                    PurelymailDns.FirstDomain,
                    PurelymailDns.PlatformSiteKey,
                    DateTimeOffset.UtcNow,
                    FirstPlatform: true));
            }

            if (FindMailbox(PurelymailDns.FirstMailbox) is null)
            {
                _mailboxes.Add(new PlatformMailbox(
                    PurelymailDns.FirstMailbox,
                    "yawer",
                    PurelymailDns.FirstDomain,
                    PurelymailDns.PlatformSiteKey,
                    DateTimeOffset.UtcNow,
                    FirstWorking: true,
                    ApiSynced: true));
            }

            Persist();
        }
    }

    private bool TryLoad()
    {
        if (string.IsNullOrWhiteSpace(_persistPath) || !File.Exists(_persistPath))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(_persistPath);
            var state = JsonSerializer.Deserialize<DirectoryState>(json, JsonOpts);
            if (state is null)
            {
                return false;
            }

            _apiToken = state.ApiToken ?? string.Empty;
            _domains.Clear();
            _domains.AddRange(state.Domains ?? []);
            _mailboxes.Clear();
            _mailboxes.AddRange((state.Mailboxes ?? []).Select(FromStored));
            return _domains.Count > 0 || _mailboxes.Count > 0;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return false;
        }
    }

    private void Persist()
    {
        if (string.IsNullOrWhiteSpace(_persistPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_persistPath)!);
        var state = new DirectoryState(
            _apiToken,
            _domains.ToArray(),
            _mailboxes.Select(ToStored).ToArray());
        var tmp = _persistPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, JsonOpts));
        File.Copy(tmp, _persistPath, overwrite: true);
        File.Delete(tmp);
    }

    private static StoredMailbox ToStored(PlatformMailbox mailbox) => new(
        mailbox.Address,
        mailbox.LocalPart,
        mailbox.Domain,
        mailbox.SiteKey,
        mailbox.CreatedUtc,
        mailbox.FirstWorking,
        mailbox.ApiSynced,
        mailbox.LastError,
        mailbox.Password);

    private static PlatformMailbox FromStored(StoredMailbox row) => new(
        row.Address,
        row.LocalPart,
        row.Domain,
        row.SiteKey,
        row.CreatedUtc,
        row.FirstWorking,
        row.ApiSynced,
        row.LastError)
    {
        Password = row.Password ?? string.Empty,
    };

    private sealed record DirectoryState(
        string? ApiToken,
        IReadOnlyList<PlatformMailDomain>? Domains,
        IReadOnlyList<StoredMailbox>? Mailboxes);

    private sealed record StoredMailbox(
        string Address,
        string LocalPart,
        string Domain,
        string SiteKey,
        DateTimeOffset CreatedUtc,
        bool FirstWorking,
        bool ApiSynced,
        string LastError,
        string? Password);
}
