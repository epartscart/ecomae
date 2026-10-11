namespace EcomAE.Platform.Mail;

/// <summary>
/// Tenant CP may create as many mailboxes as it wants — only on its own domain / site_key.
/// Super CP hosts keep fleet-wide writes on <c>/cp/super-mail/write</c>.
/// </summary>
public static class TenantMailboxGuard
{
    public static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_mailbox",
        "setup_business",
        "set_mailbox_password",
        "register_login",
        "create_staff"
    };

    public static bool TryBind(
        string? action,
        string? postedDomain,
        string? postedAddress,
        string? postedLocal,
        string requestHost,
        out string domain,
        out string siteKey,
        out string address,
        out string errorCode,
        out string errorMessage)
    {
        domain = string.Empty;
        siteKey = string.Empty;
        address = string.Empty;
        errorCode = string.Empty;
        errorMessage = string.Empty;

        var act = (action ?? string.Empty).Trim();
        if (!AllowedActions.Contains(act))
        {
            errorCode = "tenant_forbidden";
            errorMessage = "This Control Panel can only create mailboxes and staff on its own domain.";
            return false;
        }

        var tenantDomain = PlatformMailNormalize.Domain(requestHost);
        if (tenantDomain.Length == 0 || !tenantDomain.Contains('.', StringComparison.Ordinal))
        {
            errorCode = "invalid_domain";
            errorMessage = "This tenant host has no mail domain.";
            return false;
        }

        if (string.Equals(tenantDomain, PurelymailDns.FirstDomain, StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "platform_domain";
            errorMessage = "Platform ecomae.com mailboxes are Super CP only. Use this tenant’s own domain.";
            return false;
        }

        var requested = PlatformMailNormalize.Domain(postedDomain);
        if (requested.Length > 0 && !string.Equals(requested, tenantDomain, StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "isolated";
            errorMessage = "This Control Panel cannot create mailboxes for another tenant domain.";
            return false;
        }

        if (PlatformMailNormalize.IsEmail(postedAddress))
        {
            var split = PlatformMailNormalize.SplitAddress(postedAddress);
            if (!string.Equals(split.Domain, tenantDomain, StringComparison.OrdinalIgnoreCase))
            {
                errorCode = "isolated";
                errorMessage = "Mailbox addresses must use this tenant’s domain.";
                return false;
            }

            address = PlatformMailNormalize.Address(split.Local, tenantDomain);
        }
        else
        {
            address = PlatformMailNormalize.Address(postedLocal, tenantDomain);
        }

        domain = tenantDomain;
        siteKey = PlatformMailNormalize.SiteKey(null, superCpHost: false, requestHost: tenantDomain);
        return true;
    }
}
