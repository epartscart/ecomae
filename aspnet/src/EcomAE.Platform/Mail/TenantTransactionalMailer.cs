using EcomAE.Platform.Cp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Mail;

/// <summary>
/// Customer / supplier / order mail uses the tenant's real domain mailbox
/// (<c>orders@</c>, <c>info@</c>, <c>sales@</c>) on Purelymail, never a Gmail From.
/// Falls back to <see cref="CpPlatformMailer"/> only when that mailbox password is not stored.
/// </summary>
public static class TenantTransactionalMail
{
    public const string NotifyNameItem = "epc_notify_name";

    private static readonly AsyncLocal<string?> Current = new();

    public static string? CurrentNotification => Current.Value;

    public static IDisposable UseNotification(string? name)
    {
        var prior = Current.Value;
        Current.Value = name;
        return new Reset(prior);
    }

    public static string LocalFor(string? notification)
    {
        var name = (notification ?? string.Empty).Trim().ToLowerInvariant();
        if (name.Contains("lpo", StringComparison.Ordinal)
            || name.Contains("supplier", StringComparison.Ordinal)
            || name.Contains("manager", StringComparison.Ordinal)
            || name.Contains("admin", StringComparison.Ordinal)
            || name.Contains("purchase", StringComparison.Ordinal))
        {
            return "orders";
        }

        if (name.Contains("market", StringComparison.Ordinal)
            || name.Contains("broadcast", StringComparison.Ordinal)
            || name.Contains("promo", StringComparison.Ordinal))
        {
            return "sales";
        }

        if (name.Contains("otp", StringComparison.Ordinal)
            || name.Contains("auth", StringComparison.Ordinal)
            || name.Contains("password", StringComparison.Ordinal)
            || name.Contains("recover", StringComparison.Ordinal))
        {
            return "support";
        }

        return "info";
    }

    public static bool LooksLikePublicWebmail(string? email)
    {
        var value = (email ?? string.Empty).Trim().ToLowerInvariant();
        return value.Contains("@gmail.", StringComparison.Ordinal)
            || value.Contains("@googlemail.", StringComparison.Ordinal)
            || value.Contains("@yahoo.", StringComparison.Ordinal)
            || value.Contains("@hotmail.", StringComparison.Ordinal)
            || value.Contains("@outlook.", StringComparison.Ordinal);
    }

    public static string DomainInbox(string local, string? host)
    {
        var address = PlatformMailNormalize.Address(local, host);
        return address.Length > 0 ? address : string.Empty;
    }

    public static string PreferDomainInbox(string email, string? host, string local = "orders")
    {
        if (!LooksLikePublicWebmail(email))
        {
            return email;
        }

        var next = DomainInbox(local, host);
        return next.Length > 0 ? next : email;
    }

    private sealed class Reset(string? prior) : IDisposable
    {
        public void Dispose() => Current.Value = prior;
    }
}

public sealed class TenantTransactionalMailer : ICpPlatformMailer
{
    private readonly ICpPlatformMailer _inner;
    private readonly IPlatformMailDirectory _directory;
    private readonly IMailboxSmtpClient _smtp;
    private readonly IHttpContextAccessor _http;

    public TenantTransactionalMailer(
        ICpPlatformMailer inner,
        IPlatformMailDirectory directory,
        IMailboxSmtpClient smtp,
        IHttpContextAccessor http)
    {
        _inner = inner;
        _directory = directory;
        _smtp = smtp;
        _http = http;
    }

    public IReadOnlyDictionary<string, string> ReadConfig()
    {
        var config = new Dictionary<string, string>(_inner.ReadConfig(), StringComparer.Ordinal);
        var host = RequestHost();
        var notify = NotifyName();
        var inbox = TenantTransactionalMail.DomainInbox(TenantTransactionalMail.LocalFor(notify), host);
        if (inbox.Length == 0)
        {
            return config;
        }

        if (!config.TryGetValue("from_email", out var from)
            || string.IsNullOrWhiteSpace(from)
            || TenantTransactionalMail.LooksLikePublicWebmail(from))
        {
            config["from_email"] = inbox;
        }

        var box = PickMailbox(host, notify);
        if (box is not null)
        {
            config["smtp_host"] = PurelymailDns.SmtpHost;
            config["smtp_port"] = PurelymailDns.SmtpPort.ToString();
            config["smtp_encryption"] = PurelymailDns.SmtpEncryption;
            config["smtp_username"] = box.Address;
            config["from_email"] = box.Address;
            if (!config.TryGetValue("from_name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                config["from_name"] = PlatformMailNormalize.Domain(host);
            }
        }

        return config;
    }

    public async Task<CpSmsSendOutcome> SendHtmlAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var dest = (to ?? string.Empty).Trim();
        if (!PlatformMailNormalize.IsEmail(dest))
        {
            return await _inner.SendHtmlAsync(dest, subject, htmlBody, cancellationToken).ConfigureAwait(false);
        }

        var box = PickMailbox(RequestHost(), NotifyName());
        if (box is null || !box.HasPassword)
        {
            return await _inner.SendHtmlAsync(dest, subject, htmlBody, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await _smtp.SendAsync(box.Address, box.Password, dest, subject, htmlBody ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            return new CpSmsSendOutcome(true, string.Empty);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or System.Net.Mail.SmtpException)
        {
            return await _inner.SendHtmlAsync(dest, subject, htmlBody, cancellationToken).ConfigureAwait(false);
        }
    }

    private PlatformMailbox? PickMailbox(string host, string? notifyName)
    {
        var domain = PlatformMailNormalize.Domain(host);
        if (domain.Length == 0)
        {
            return null;
        }

        var siteKey = RequestSiteKey();
        var local = TenantTransactionalMail.LocalFor(notifyName);
        foreach (var candidate in new[]
                 {
                     PlatformMailNormalize.Address(local, domain),
                     PlatformMailNormalize.Address("orders", domain),
                     PlatformMailNormalize.Address("info", domain),
                     PlatformMailNormalize.Address("hello", domain),
                     PlatformMailNormalize.Address("sales", domain),
                 })
        {
            var box = _directory.FindMailbox(candidate);
            if (box is { HasPassword: true } && SiteAllowed(box, siteKey, host))
            {
                return box;
            }
        }

        return null;
    }

    private static bool SiteAllowed(PlatformMailbox box, string? siteKey, string host)
    {
        if (PlatformHostPolicy.IsSuperCpHost(host) || PlatformHostPolicy.IsSuperCpHost(box.Domain))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(siteKey))
        {
            return string.Equals(box.Domain, PlatformMailNormalize.Domain(host), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(box.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase);
    }

    private string RequestHost()
    {
        if (_http.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant
            && !string.IsNullOrWhiteSpace(tenant.Host))
        {
            return tenant.Host;
        }

        return _http.HttpContext?.Request.Host.Host ?? string.Empty;
    }

    private string? RequestSiteKey()
    {
        if (_http.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant
            && !string.IsNullOrWhiteSpace(tenant.SiteKey))
        {
            return tenant.SiteKey;
        }

        return null;
    }

    private string? NotifyName()
    {
        if (!string.IsNullOrWhiteSpace(TenantTransactionalMail.CurrentNotification))
        {
            return TenantTransactionalMail.CurrentNotification;
        }

        if (_http.HttpContext?.Items[TenantTransactionalMail.NotifyNameItem] is string named
            && !string.IsNullOrWhiteSpace(named))
        {
            return named;
        }

        return null;
    }
}
