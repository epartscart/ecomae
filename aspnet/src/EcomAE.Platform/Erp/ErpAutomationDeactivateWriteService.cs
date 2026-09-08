using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_automation_set_enabled(..., false)</c> / ajax <c>automation_deactivate</c> twin.
/// UPSERT <c>epc_price_settings</c> key <c>erp_auto_{setting_key}</c> to <c>0</c>.
/// Schema ensure, activate, install-template, enable-category, and tick stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpAutomationDeactivateWriteService
{
    Task<ErpSimpleWriteResult> DeactivateAsync(
        ErpAutomationDeactivateWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpAutomationDeactivateWriteRequest(string? Id = null);

public sealed class ErpAutomationDeactivateWriteService : IErpAutomationDeactivateWriteService
{
    public const string UnknownAutomation = "Unknown automation";
    public const string DisabledMessage = "Automation disabled";

    /// <summary>PHP <c>epc_erp_automation_catalogue()</c> id → <c>setting_key</c>. Case-sensitive.</summary>
    public static readonly IReadOnlyDictionary<string, string> CatalogueSettingKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["order_to_erp"] = "auto_order_to_erp",
            ["period_close"] = "auto_period_close",
            ["year_end_close"] = "auto_year_end",
            ["bank_recon"] = "auto_bank_recon",
            ["collections_dunning"] = "auto_collections_dunning",
            ["report_scheduler"] = "auto_report_scheduler",
            ["vat_reminder"] = "auto_vat_reminder",
            ["gl_auto_post"] = "auto_gl_post",
            ["payment_reminder"] = "auto_ap_payment_reminder",
            ["depreciation_run"] = "auto_depreciation",
            ["po_approval"] = "auto_po_approval",
            ["invoice_autosend"] = "auto_invoice_send",
            ["low_stock_alert"] = "auto_low_stock",
            ["employee_onboarding"] = "auto_employee_onboarding",
            ["daily_sales_summary"] = "auto_daily_sales",
            ["aml_alert"] = "auto_aml_alert",
            ["process_flow_routing"] = "auto_process_flow",
            ["three_way_match"] = "auto_three_way_match",
            ["subscription_billing"] = "auto_subscription_billing",
            ["rma_warranty"] = "auto_rma_warranty",
            ["credit_check"] = "auto_credit_check",
            ["goods_receipt_notify"] = "auto_grn_notify",
        };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpAutomationDeactivateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DeactivateAsync(
        ErpAutomationDeactivateWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryResolveSettingKey(request.Id, out var settingKey))
        {
            return ErpSimpleWriteResult.Fail("invalid", UnknownAutomation);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_price_settings", "setting_key", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Settings table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
            cancellationToken,
            settingKey,
            "0").ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(DisabledMessage, 0);
    }

    /// <summary>PHP <c>trim($id)</c> then <c>isset($cat[$id])</c>; key is <c>erp_auto_</c> + setting_key.</summary>
    public static bool TryResolveSettingKey(string? id, out string settingKey)
    {
        var trimmed = (id ?? "").Trim();
        if (CatalogueSettingKeys.TryGetValue(trimmed, out var key))
        {
            settingKey = "erp_auto_" + key;
            return true;
        }

        settingKey = "";
        return false;
    }

    public static string JsonText(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }

            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetRawText();
            }
        }

        return "";
    }

    public static bool JsonFlag(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n) && n != 0)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
