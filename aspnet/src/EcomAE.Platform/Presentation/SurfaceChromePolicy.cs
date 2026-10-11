using EcomAE.Platform.Auth;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// CP, ERP and Super CP stay in their own chrome. A tenant user may have CP-only, ERP-only
/// or both; the other surface is one explicit switcher, never a dump of that environment's modules.
/// </summary>
public static class SurfaceChromePolicy
{
    private static readonly string[] TenantShopNeedles =
    [
        "shop/orders", "/cp/orders",
        "shop/catalogue", "product-catalogue", "sku_media", "sku-media",
        "shop/prices/", "shop/prices?", "/cp/price-lists", "/cp/prices-upload", "/cp/prices-edit",
        "/cp/price-management",
        "shop/crosses", "/cp/crosses",
        "shop/customer_mgmt", "customer-mgmt",
        "shop/payments", "payment-gateways",
        "shop/returns-manager", "/cp/returns-app",
        "shop/quote", "quote-requests",
        "shop/channels/", "shop/channels?",
        "shop/pos/", "shop/pos?", "/cp/pos-terminal",
        "shop/statistics",
        "shop/logistics", "/cp/carriers-app", "/cp/delivery-methods", "/cp/storages",
        "shop/procurement", "/cp/procurement",
        "shop/marketing/", "/cp/marketing-app", "/cp/seo-app",
        "shop/crm/", "/cp/crm-",
        "shop/document_control",
        "shop/parts_agent", "parts-agent",
        "shop/accessories", "/cp/accessories",
        "shop/manufacturers",
        "shop/cart", "shop/workshop", "shop/bulk",
        "shop/demand", "shop/vehicle", "shop/product-family",
    ];

    public static bool HasCpSurface(LegacySessionContext? session)
        => session is { Kind: LegacySessionKind.Admin } && session.Capabilities.Contains("cp");

    public static bool HasErpSurface(LegacySessionContext? session)
        => session is { Kind: LegacySessionKind.Admin } && session.Capabilities.Contains("erp");

    public static bool HasBosSurface(LegacySessionContext? session)
        => session is { Kind: LegacySessionKind.Admin } && session.Capabilities.Contains("bos");

    public static TenantMode ModeOf(HttpContext? http)
        => http?.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant
            ? tenant.Mode
            : TenantMode.Unknown;

    /// <summary>One header CTA from CP → ERP. Hidden for CP-only users and ERP-only tenants.</summary>
    public static bool ShowErpSwitcher(LegacySessionContext? session, TenantMode mode)
        => mode is not TenantMode.ErpOnlyTenant && HasErpSurface(session);

    /// <summary>One header CTA from ERP → CP. Hidden for ERP-only users and ERP-only tenants.</summary>
    public static bool ShowCpSwitcher(LegacySessionContext? session, TenantMode mode)
        => mode is not TenantMode.ErpOnlyTenant && HasCpSurface(session);

    public static bool IsErpDestination(string? href)
    {
        var raw = (href ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return false;
        }

        // CP-hosted tax kit — not the ERP window.
        if (raw.Contains("uae-tax-compliance", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("/cp/tax-toolkits", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var mapped = PathOnly(PhpSurfaceLinkMap.AspNetPrimaryHref(raw));
        if (mapped.StartsWith("/cp", StringComparison.OrdinalIgnoreCase)
            || mapped.StartsWith("/brochure", StringComparison.OrdinalIgnoreCase)
            || mapped.StartsWith("/bos", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (raw.Contains("epc_erp_shell", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("/shop/finance/erp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rawPath = PathOnly(raw);
        if (rawPath.Equals("/erp", StringComparison.OrdinalIgnoreCase)
            || rawPath.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || rawPath.Equals("/ERP", StringComparison.OrdinalIgnoreCase)
            || rawPath.StartsWith("/ERP/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return mapped.Equals("/erp", StringComparison.OrdinalIgnoreCase)
               || mapped.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase);
    }

    private static string PathOnly(string href)
    {
        var key = TopMenuLinks.DestinationKey(href);
        var q = key.IndexOf('?', StringComparison.Ordinal);
        return q < 0 ? key : key[..q];
    }

    public static bool IsBosDestination(string? href)
    {
        var mapped = TopMenuLinks.DestinationKey(
            PhpSurfaceLinkMap.AspNetPrimaryHref(href));
        var q = mapped.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            mapped = mapped[..q];
        }

        return mapped.Equals("/bos", StringComparison.OrdinalIgnoreCase)
               || mapped.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Tenant shop desks belong on the tenant host CP — never on Super CP chrome.</summary>
    public static bool IsTenantShopDesk(string? href)
    {
        var raw = (href ?? string.Empty).Trim().ToLowerInvariant();
        if (raw.Length == 0 || raw.Contains("tenant_hub", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var needle in TenantShopNeedles)
        {
            if (raw.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsErpNavGroup(CpNavGroup group)
    {
        var key = group.Key ?? string.Empty;
        var caption = group.Caption ?? string.Empty;
        var shortLabel = group.Short ?? string.Empty;
        return key.Contains("erp", StringComparison.OrdinalIgnoreCase)
               || caption.StartsWith("ERP", StringComparison.OrdinalIgnoreCase)
               || shortLabel.Equals("ERP", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Super CP mega-menu area: Super operator tools only — no ERP window, no BOS, no tenant shop desk.</summary>
    public static bool IsSuperCpMenuArea(PhpSuperCpBocNav.BocArea area)
    {
        if (area.Group.Equals("erp", StringComparison.OrdinalIgnoreCase)
            || area.Group.Equals("shop", StringComparison.OrdinalIgnoreCase)
            || area.Group.Equals("catalogue", StringComparison.OrdinalIgnoreCase)
            || area.Group.Equals("logistics", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsErpDestination(area.Href) || IsErpDestination(area.PhpPath))
        {
            return false;
        }

        if (IsBosDestination(area.Href))
        {
            return false;
        }

        return !IsTenantShopDesk(area.PhpPath) && !IsTenantShopDesk(area.Href);
    }

    public static IReadOnlyList<CpNavGroup> KeepCpSurface(IReadOnlyList<CpNavGroup> groups, bool superHost)
    {
        var result = new List<CpNavGroup>(groups.Count);
        foreach (var group in groups)
        {
            if (IsErpNavGroup(group))
            {
                continue;
            }

            var items = group.Items.Where(item =>
            {
                var href = CpNavTree.AspNetHref(item.Url);
                if (IsErpDestination(href) || IsErpDestination(item.Url))
                {
                    return false;
                }

                if (superHost && (IsBosDestination(href) || IsTenantShopDesk(item.Url) || IsTenantShopDesk(href)))
                {
                    return false;
                }

                return true;
            }).ToList();

            if (items.Count > 0)
            {
                result.Add(group with { Items = items });
            }
        }

        return result;
    }

    public static IReadOnlyList<PhpModuleCatalog.ModuleLink> KeepCpBrochure(
        IReadOnlyList<PhpModuleCatalog.ModuleLink> links,
        string? host)
    {
        var super = PlatformHostPolicy.IsSuperCpHost(host);
        return links.Where(link =>
        {
            if (IsErpDestination(link.Href))
            {
                return false;
            }

            if (!super && LegacyDesktopChromeCatalog.IsSuperOnlyCpLink(link.Href, link.Group))
            {
                return false;
            }

            return !super || (!IsTenantShopDesk(link.Href) && !IsBosDestination(link.Href));
        }).ToList();
    }
}
