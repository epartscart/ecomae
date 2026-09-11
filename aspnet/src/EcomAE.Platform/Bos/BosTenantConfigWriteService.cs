using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>tenant_config</c> <c>set</c> / <c>epc_tenant_config_set</c>.
/// Bulk-set, import, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. This write uses the platform operator PDO.
/// </summary>
public interface IBosTenantConfigWriteService
{
    Task<ErpSimpleWriteResult> SetAsync(
        string? siteKey,
        string? group,
        string? key,
        string? value,
        long updatedBy,
        CancellationToken cancellationToken = default);
}

public sealed class BosTenantConfigWriteService : IBosTenantConfigWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex GroupSafe = new("[^a-z_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex KeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, (string Type, string Label, string Default)>> Groups
        = new Dictionary<string, IReadOnlyDictionary<string, (string Type, string Label, string Default)>>(StringComparer.Ordinal)
        {
            ["branding"] = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
            {
                ["company_name"] = ("string", "Company Name", ""),
                ["logo_url"] = ("string", "Logo URL", ""),
                ["favicon_url"] = ("string", "Favicon URL", ""),
                ["primary_color"] = ("string", "Primary Brand Color", "#0d6efd"),
                ["secondary_color"] = ("string", "Secondary Color", "#6c757d"),
                ["login_message"] = ("string", "Login Welcome Text", ""),
            },
            ["business"] = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
            {
                ["legal_name"] = ("string", "Legal Entity Name", ""),
                ["tax_id"] = ("string", "Tax ID / TRN", ""),
                ["registration_no"] = ("string", "Trade License No.", ""),
                ["address_line1"] = ("string", "Address Line 1", ""),
                ["address_line2"] = ("string", "Address Line 2", ""),
                ["city"] = ("string", "City", ""),
                ["country"] = ("string", "Country", ""),
                ["phone"] = ("string", "Phone", ""),
                ["email"] = ("string", "Contact Email", ""),
                ["website"] = ("string", "Website URL", ""),
            },
            ["tax"] = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
            {
                ["vat_enabled"] = ("bool", "VAT Enabled", "1"),
                ["vat_rate"] = ("float", "Default VAT Rate %", "5"),
                ["vat_inclusive"] = ("bool", "Prices Include VAT", "1"),
                ["tax_report_period"] = ("string", "Tax Report Period", "quarterly"),
            },
            ["shipping"] = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
            {
                ["free_shipping_min"] = ("float", "Free Shipping Min Order", "0"),
                ["default_carrier"] = ("string", "Default Carrier", ""),
                ["shipping_origin"] = ("string", "Ship From Location", ""),
                ["handling_days"] = ("int", "Handling Days", "1"),
            },
            ["payments"] = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
            {
                ["cod_enabled"] = ("bool", "Cash on Delivery", "1"),
                ["card_enabled"] = ("bool", "Card Payments", "0"),
                ["bank_transfer"] = ("bool", "Bank Transfer", "1"),
                ["payment_terms"] = ("string", "Default Net Terms", "Net 30"),
            },
            ["emails"] = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
            {
                ["from_name"] = ("string", "From Name", ""),
                ["from_email"] = ("string", "From Email", ""),
                ["reply_to"] = ("string", "Reply-To Email", ""),
                ["order_confirm"] = ("bool", "Order Confirmation", "1"),
                ["shipping_notify"] = ("bool", "Shipping Notification", "1"),
                ["invoice_email"] = ("bool", "Invoice Email", "1"),
            },
            ["features"] = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
            {
                ["reviews_enabled"] = ("bool", "Product Reviews", "1"),
                ["wishlist_enabled"] = ("bool", "Wishlist", "1"),
                ["compare_enabled"] = ("bool", "Product Compare", "0"),
                ["live_chat"] = ("bool", "Live Chat Widget", "0"),
                ["multilang"] = ("bool", "Multi-Language", "0"),
                ["b2b_mode"] = ("bool", "B2B Mode", "0"),
            },
        };

    private readonly IErpWriteConnectionFactory _connections;

    public BosTenantConfigWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP ajax <c>preg_replace('/[^a-z_]/', '', ...)</c> — no <c>strtolower</c>.</summary>
    public static string PhpGroupKey(string? raw)
        => GroupSafe.Replace(raw ?? "", "");

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', ...)</c> — no <c>strtolower</c>.</summary>
    public static string PhpConfigKey(string? raw)
        => KeySafe.Replace(raw ?? "", "");

    public static bool TryField(string group, string key, out (string Type, string Label, string Default) field)
    {
        field = default;
        return Groups.TryGetValue(group, out var fields) && fields.TryGetValue(key, out field);
    }

    public async Task<ErpSimpleWriteResult> SetAsync(
        string? siteKey,
        string? group,
        string? key,
        string? value,
        long updatedBy,
        CancellationToken cancellationToken = default)
    {
        var site = PhpBosSiteKey(siteKey);
        if (site.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        var groupKey = PhpGroupKey(group);
        var configKey = PhpConfigKey(key);
        if (!Groups.ContainsKey(groupKey))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid config group");
        }

        if (!TryField(groupKey, configKey, out var field))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid config key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        var newValue = value ?? "";
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var stored = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `config_value` FROM `epc_tenant_config` WHERE `site_key` = ? AND `config_group` = ? AND `config_key` = ?"),
                cancellationToken, site, groupKey, configKey).ConfigureAwait(false);
            var oldValue = stored ?? field.Default;
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_tenant_config`
                        (`site_key`, `config_group`, `config_key`, `config_value`, `value_type`, `label`, `updated_by`)
                    VALUES (?, ?, ?, ?, ?, ?, ?)
                    ON DUPLICATE KEY UPDATE
                        `config_value` = VALUES(`config_value`),
                        `updated_by` = VALUES(`updated_by`),
                        `updated_at` = NOW()
                    """),
                cancellationToken, site, groupKey, configKey, newValue, field.Type, field.Label, updatedBy).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_tenant_config_history`
                        (`site_key`, `config_group`, `config_key`, `old_value`, `new_value`, `changed_by`)
                    VALUES (?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken, site, groupKey, configKey, oldValue, newValue, updatedBy).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Tenant config saved", id > 0 ? id : 1);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Tenant config table is missing — schema-ensure stays Classic.");
        }
    }
}
