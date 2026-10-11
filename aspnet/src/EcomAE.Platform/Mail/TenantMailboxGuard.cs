using EcomAE.Platform.Services;

namespace EcomAE.Platform.Mail;

/// <summary>
/// Shop-tenant CP/ERP may create mailboxes only on their own domain / site_key.
/// Only ERP sessions on Super hosts may create customer boxes on ecomae.com.
/// Super CP fleet writes stay on <c>/cp/super-mail/write</c>.
/// </summary>
public static class TenantMailboxGuard
{
    public static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_mailbox",
        "setup_business",
        "set_mailbox_password",
        "register_login",
        "create_staff",
        "send"
    };

    public static readonly HashSet<string> PlatformErpActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "create_mailbox",
        "set_mailbox_password",
        "register_login",
        "create_staff",
        "send"
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
        out string errorMessage,
        bool allowPlatformErp = false)
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

        var platformHost = PlatformHostPolicy.IsSuperCpHost(requestHost)
            || string.Equals(tenantDomain, PurelymailDns.FirstDomain, StringComparison.OrdinalIgnoreCase);
        if (platformHost)
        {
            return BindPlatformErp(
                act,
                postedDomain,
                postedAddress,
                postedLocal,
                allowPlatformErp,
                out domain,
                out siteKey,
                out address,
                out errorCode,
                out errorMessage);
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

    private static bool BindPlatformErp(
        string action,
        string? postedDomain,
        string? postedAddress,
        string? postedLocal,
        bool allowPlatformErp,
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

        if (!allowPlatformErp)
        {
            errorCode = "platform_domain";
            errorMessage = "Only ERP customers can create mailboxes on ecomae.com. Shop tenants use their own domain.";
            return false;
        }

        if (!PlatformErpActions.Contains(action))
        {
            errorCode = "tenant_forbidden";
            errorMessage = "ERP customers can create and send from their own @ecomae.com mailbox only.";
            return false;
        }

        var requested = PlatformMailNormalize.Domain(postedDomain);
        if (requested.Length > 0 && !string.Equals(requested, PurelymailDns.FirstDomain, StringComparison.OrdinalIgnoreCase))
        {
            errorCode = "isolated";
            errorMessage = "ERP customers can only create mailboxes on ecomae.com.";
            return false;
        }

        string local;
        if (PlatformMailNormalize.IsEmail(postedAddress))
        {
            var split = PlatformMailNormalize.SplitAddress(postedAddress);
            if (!string.Equals(split.Domain, PurelymailDns.FirstDomain, StringComparison.OrdinalIgnoreCase))
            {
                errorCode = "isolated";
                errorMessage = "ERP customer mailboxes must use the ecomae.com domain.";
                return false;
            }

            local = split.Local;
        }
        else
        {
            local = PlatformMailNormalize.LocalPart(postedLocal);
        }

        if (!string.Equals(action, "send", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(action, "set_mailbox_password", StringComparison.OrdinalIgnoreCase)
            && PurelymailDns.IsReservedPlatformLocal(local))
        {
            errorCode = "reserved_mailbox";
            errorMessage = "That ecomae.com address is reserved for platform / Super CP. Choose another local part.";
            return false;
        }

        domain = PurelymailDns.FirstDomain;
        siteKey = PurelymailDns.PlatformSiteKey;
        address = PlatformMailNormalize.Address(local, domain);
        return true;
    }
}
