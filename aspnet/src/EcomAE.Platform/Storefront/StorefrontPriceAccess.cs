using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP twin: content/shop/docpart/epc_storefront_prices_helpers.php
/// Guests on warehouse_supplier / epartscart: hide price/qty/term/warehouse.
/// Wholesale pending/rejected: hide until CP approval; retail auto-approved.
/// </summary>
public enum StorefrontPriceAccessState
{
    Ok,
    Guest,
    Pending,
    Rejected
}

public sealed record StorefrontPriceAccessResult(
    StorefrontPriceAccessState State,
    bool PricesVisible,
    string SensitiveMask,
    string LoginCtaHtml,
    string LoginCtaPlain,
    bool ShowPurchaseCost = false)
{
    public static StorefrontPriceAccessResult Visible { get; } = new(
        StorefrontPriceAccessState.Ok,
        true,
        "**",
        string.Empty,
        string.Empty);

    public string StateToken => State switch
    {
        StorefrontPriceAccessState.Guest => "guest",
        StorefrontPriceAccessState.Pending => "pending",
        StorefrontPriceAccessState.Rejected => "rejected",
        _ => "ok"
    };
}

public interface IStorefrontPriceAccess
{
    ValueTask<StorefrontPriceAccessResult> ResolveAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);

    IReadOnlyList<StorefrontPartOfferDigest> RedactOffers(IReadOnlyList<StorefrontPartOfferDigest> offers);
}

public sealed class StorefrontPriceAccess : IStorefrontPriceAccess
{
    public const string SensitiveMask = "**";

    private readonly ILegacySessionValidator _sessions;
    private readonly ITenantDbConnectionFactory _connections;

    public StorefrontPriceAccess(
        ILegacySessionValidator sessions,
        ITenantDbConnectionFactory connections)
    {
        _sessions = sessions;
        _connections = connections;
    }

    public async ValueTask<StorefrontPriceAccessResult> ResolveAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var tenant = httpContext.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var host = httpContext.Request.Host.Host ?? string.Empty;
        var hideForGuests = HideStorefrontPricesForGuests(tenant, host);

        // Prefer customer cookies even when stale/valid admin cookies are present (PHP DP_User::getUserId).
        // If no retail session, fall back so Admin browsing the storefront still unlocks prices/terms.
        var session = await _sessions.ValidateCustomerAsync(httpContext, cancellationToken).ConfigureAwait(false);
        if (session.Kind != LegacySessionKind.Customer)
        {
            var adminOrOther = await _sessions.ValidateAsync(httpContext, cancellationToken).ConfigureAwait(false);
            if (adminOrOther.Kind == LegacySessionKind.Admin)
            {
                session = adminOrOther;
            }
        }

        var userId = session.Kind is LegacySessionKind.Customer or LegacySessionKind.Admin
            ? session.UserId
            : 0;

        if (userId <= 0)
        {
            if (!hideForGuests)
            {
                return StorefrontPriceAccessResult.Visible;
            }

            return Build(StorefrontPriceAccessState.Guest);
        }

        // Admin CP sessions are not wholesale trade accounts — show prices/terms on storefront.
        if (session.Kind == LegacySessionKind.Admin)
        {
            return StorefrontPriceAccessResult.Visible with { ShowPurchaseCost = true };
        }

        if (!_connections.IsConfigured)
        {
            // Fail open when trade module DB unavailable (PHP twin).
            return StorefrontPriceAccessResult.Visible;
        }

        var status = await ReadTradeApprovalStatusAsync(tenant, userId, cancellationToken).ConfigureAwait(false);
        if (string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
        {
            return Build(StorefrontPriceAccessState.Pending);
        }

        if (string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            return Build(StorefrontPriceAccessState.Rejected);
        }

        var showPurchase = await ReadGroupForPercentageAsync(tenant, userId, cancellationToken).ConfigureAwait(false);
        return StorefrontPriceAccessResult.Visible with { ShowPurchaseCost = showPurchase };
    }

    private async Task<bool> ReadGroupForPercentageAsync(TenantContext? tenant, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0 || !_connections.IsConfigured)
        {
            return false;
        }

        try
        {
            await using var connection = await _connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = LegacySurfaceDashboardSql.SelectStorefrontGroupForPercentage;
            var p = cmd.CreateParameter();
            p.ParameterName = "@userId";
            p.Value = userId;
            cmd.Parameters.Add(p);
            var raw = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(raw ?? 0, CultureInfo.InvariantCulture) != 0;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<StorefrontPartOfferDigest> RedactOffers(IReadOnlyList<StorefrontPartOfferDigest> offers)
    {
        if (offers.Count == 0)
        {
            return offers;
        }

        var list = new List<StorefrontPartOfferDigest>(offers.Count);
        foreach (var o in offers)
        {
            list.Add(o with
            {
                Price = 0m,
                PricePurchase = 0m,
                Exist = o.Exist > 0 ? 1 : 0,
                TimeToExe = string.Empty,
                Storage = string.Empty,
                PriceList = string.Empty,
            });
        }

        return list;
    }

    /// <summary>PHP: epc_storefront_prices_hide_for_guests_enabled — epartscart / warehouse_supplier first.</summary>
    public static bool HideStorefrontPricesForGuests(TenantContext? tenant, string host)
    {
        var siteKey = (tenant?.SiteKey ?? string.Empty).Trim().ToLowerInvariant();
        if (siteKey is "epartscart")
        {
            return true;
        }

        var h = (host ?? string.Empty).Trim().ToLowerInvariant();
        return h.Contains("epartscart", StringComparison.Ordinal);
    }

    private async Task<string> ReadTradeApprovalStatusAsync(
        TenantContext? tenant,
        int userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await _connections.OpenForTenantAsync(tenant, cancellationToken)
                .ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `data_value`
                FROM `users_profiles`
                WHERE `user_id` = @userId AND `data_key` = 'epc_trade_approval_status'
                LIMIT 1
                """;
            var p = command.CreateParameter();
            p.ParameterName = "@userId";
            p.Value = userId;
            command.Parameters.Add(p);
            var raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var status = Convert.ToString(raw is DBNull or null ? string.Empty : raw, CultureInfo.InvariantCulture) ?? string.Empty;
            // PHP: empty status → approved (legacy fail-open).
            return string.IsNullOrWhiteSpace(status) ? "approved" : status.Trim().ToLowerInvariant();
        }
        catch
        {
            return "approved";
        }
    }

    private static StorefrontPriceAccessResult Build(StorefrontPriceAccessState state)
    {
        var login = StorefrontSurfaceLinks.Login;
        var signup = StorefrontSurfaceLinks.Registration;
        return state switch
        {
            StorefrontPriceAccessState.Pending => new(
                state,
                false,
                SensitiveMask,
                "<span class=\"epc-price-login-cta epc-price-login-cta--pending\">"
                + "<span class=\"epc-price-login-cta__hint\">Wholesale account pending manager approval — prices unlock after CP approval</span>"
                + "</span>",
                "Wholesale account pending manager approval — prices unlock after CP approval"),
            StorefrontPriceAccessState.Rejected => new(
                state,
                false,
                SensitiveMask,
                "<span class=\"epc-price-login-cta epc-price-login-cta--rejected\">"
                + "<span class=\"epc-price-login-cta__hint\">Trade account not approved — contact support</span>"
                + "</span>",
                "Trade account not approved — contact support"),
            _ => new(
                state,
                false,
                SensitiveMask,
                "<span class=\"epc-price-login-cta\">"
                + "<a href=\"" + login + "\">Log in</a>"
                + "<span class=\"epc-price-login-cta__sep\"> or </span>"
                + "<a href=\"" + signup + "\">register</a>"
                + "<span class=\"epc-price-login-cta__hint\"> to see prices</span>"
                + "</span>",
                "Log in or register to see prices"),
        };
    }
}
