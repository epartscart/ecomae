namespace EcomAE.Platform.Mail;

/// <summary>
/// One Purelymail account for Super CP, Super ERP, BOS, and every tenant domain.
/// Token: <c>ECOMAE_PURELYMAIL_API_TOKEN</c> or <c>EcomAE:Purelymail:ApiToken</c>. Never echoed.
/// </summary>
public sealed class PurelymailOptions
{
    public const string SectionName = "Purelymail";
    public const string EnvTokenName = "ECOMAE_PURELYMAIL_API_TOKEN";

    public string ApiBaseUrl { get; set; } = PurelymailDns.ApiBaseUrl;
    public string ApiToken { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = PurelymailDns.SmtpHost;
    public int SmtpPort { get; set; } = PurelymailDns.SmtpPort;
    public string ImapHost { get; set; } = PurelymailDns.ImapHost;
    public int ImapPort { get; set; } = PurelymailDns.ImapPort;
}

public static class PurelymailDns
{
    public const string ApiBaseUrl = "https://purelymail.com/api/v0/";
    public const string ApiTokenHeader = "Purelymail-Api-Token";
    public const string SmtpHost = "smtp.purelymail.com";
    public const int SmtpPort = 587;
    public const string SmtpEncryption = "tls";
    public const string ImapHost = "imap.purelymail.com";
    public const int ImapPort = 993;
    public const string MxHost = "mailserver.purelymail.com";
    public const string Spf = "v=spf1 include:_spf.purelymail.com ~all";
    public const string DmarcCname = "dmarcroot.purelymail.com";
    public const string FirstMailbox = "yawer@ecomae.com";
    public const string FirstDomain = "ecomae.com";
    public const string PlatformSiteKey = "platform";
    public const string OwnershipTxtPrefix = "purelymail_ownership_proof=";

    public static readonly IReadOnlyList<string> BusinessLocals =
    [
        "info", "hello", "support", "sales", "accounts", "orders", "billing"
    ];

    public static readonly IReadOnlyList<string> AliasLocals =
    [
        "contact", "office", "admin", "noreply"
    ];

    /// <summary>Platform business / operator locals on ecomae.com — ERP customers cannot claim these.</summary>
    public static bool IsReservedPlatformLocal(string? local)
    {
        var value = PlatformMailNormalize.LocalPart(local);
        if (value.Length == 0 || string.Equals(value, "yawer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return BusinessLocals.Any(l => string.Equals(l, value, StringComparison.OrdinalIgnoreCase))
            || AliasLocals.Any(l => string.Equals(l, value, StringComparison.OrdinalIgnoreCase));
    }

    public static readonly IReadOnlyList<(string Host, string Target, string Kind, string Hint)> Records =
    [
        ("@", MxHost, "MX", "Mail exchanger"),
        ("@", Spf, "TXT", "SPF"),
        ("purelymail1._domainkey", "key1.dkimroot.purelymail.com", "CNAME", "DKIM 1"),
        ("purelymail2._domainkey", "key2.dkimroot.purelymail.com", "CNAME", "DKIM 2"),
        ("purelymail3._domainkey", "key3.dkimroot.purelymail.com", "CNAME", "DKIM 3"),
        ("_dmarc", DmarcCname, "CNAME", "DMARC"),
        ("@", "(from Get ownership code)", "TXT", "Domain ownership"),
    ];
}
