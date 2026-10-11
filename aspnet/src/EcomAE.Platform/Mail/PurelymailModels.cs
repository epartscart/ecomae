using System.Text.RegularExpressions;

namespace EcomAE.Platform.Mail;

public sealed record PlatformMailDomain(
    string Domain,
    string SiteKey,
    DateTimeOffset AddedUtc,
    bool FirstPlatform = false,
    string OwnershipCode = "",
    string DnsStatus = "",
    bool ApiSynced = false);

public sealed record PlatformMailbox(
    string Address,
    string LocalPart,
    string Domain,
    string SiteKey,
    DateTimeOffset CreatedUtc,
    bool FirstWorking = false,
    bool ApiSynced = false,
    string LastError = "",
    bool PasswordStored = false)
{
    internal string Password { get; init; } = string.Empty;

    public bool HasPassword => PasswordStored || !string.IsNullOrWhiteSpace(Password);

    public PlatformMailbox PublicView() => this with { Password = string.Empty, PasswordStored = HasPassword };
}

public sealed record MailboxMessage(
    string Id,
    string From,
    string To,
    string Subject,
    DateTimeOffset ReceivedUtc,
    string Excerpt,
    bool Unread,
    string Body = "");

public sealed record PurelymailCallResult(
    bool Ok,
    string Code,
    string Message,
    string? Raw = null)
{
    public static PurelymailCallResult Success(string message, string? raw = null)
        => new(true, "ok", message, raw);

    public static PurelymailCallResult Fail(string code, string message, string? raw = null)
        => new(false, code, message, raw);
}

public sealed record PlatformMailWriteRequest(
    string Action,
    string? Domain = null,
    string? SiteKey = null,
    string? LocalPart = null,
    string? Address = null,
    string? Password = null,
    string? ApiToken = null,
    IReadOnlyList<(string Domain, string SiteKey)>? Tenants = null);

public sealed record TenantMailTarget(string Domain, string SiteKey, string Hostname, bool DomainReady);

public sealed record TenantMailFleetRow(
    string Domain,
    string SiteKey,
    bool InDirectory,
    bool OnPurelymail,
    string DnsStatus,
    string OwnershipCode);

public sealed record PlatformMailWriteResult(bool Succeeded, int Writes, string Code, string Message);

public sealed record SmtpDefaults(
    string Host,
    string Port,
    string Encryption,
    string Username);

public static class PlatformMailNormalize
{
    private static readonly Regex DomainSafe = new(@"[^a-z0-9.-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SiteKeySafe = new(@"[^a-z0-9_-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LocalPartSafe = new(@"[^a-z0-9._+-]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Domain(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        if (value.StartsWith("www.", StringComparison.Ordinal))
        {
            value = value[4..];
        }

        value = DomainSafe.Replace(value, string.Empty);
        return value.Length > 253 ? value[..253] : value;
    }

    public static string SiteKey(string? posted, bool superCpHost, string? requestHost)
    {
        if (superCpHost)
        {
            var key = SiteKeySafe.Replace((posted ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);
            return key.Length > 0 ? key : PurelymailDns.PlatformSiteKey;
        }

        var host = Domain(requestHost).Replace('.', '-');
        host = SiteKeySafe.Replace(host, string.Empty);
        return host.Length > 0 ? host : "tenant";
    }

    public static string LocalPart(string? raw)
    {
        var value = LocalPartSafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);
        return value.Length > 64 ? value[..64] : value;
    }

    public static string Address(string? localPart, string? domain)
    {
        var local = LocalPart(localPart);
        var host = Domain(domain);
        return local.Length == 0 || host.Length == 0 ? string.Empty : local + "@" + host;
    }

    public static bool IsEmail(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && value.Count(c => c == '@') == 1 && Domain(value[(at + 1)..]).Length > 0;
    }

    public static (string Local, string Domain) SplitAddress(string? address)
    {
        var value = (address ?? string.Empty).Trim().ToLowerInvariant();
        var at = value.IndexOf('@');
        if (at <= 0 || at == value.Length - 1)
        {
            return (string.Empty, string.Empty);
        }

        return (LocalPart(value[..at]), Domain(value[(at + 1)..]));
    }
}
