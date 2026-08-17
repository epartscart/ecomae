using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Erp;

public sealed record ErpTaxAmounts(
    decimal AmountExVat,
    decimal VatAmount,
    decimal TotalAmount,
    decimal TaxRate,
    string TaxCategory,
    string TaxLabel,
    string KitCode,
    string CountryCode,
    string Reason);

public sealed record ErpPurchaseTaxAmounts(
    decimal AmountExVat,
    decimal VatAmount,
    decimal TotalAmount,
    decimal TaxRate,
    bool VatApplicable,
    decimal ImportDuty,
    decimal LandedCostExVat,
    decimal TotalWithDuty,
    string KitCode,
    string CountryCode,
    string TaxContext);

/// <summary>
/// Port of PHP <c>epc_tax_toolkit_calc_amounts</c> / <c>epc_tax_toolkit_resolve</c>.
/// The rate always comes from the tenant's registered jurisdiction kit
/// (<c>epc_tax_toolkit_tenant_profile</c> → <c>epc_tax_toolkits.rules_json</c>) — never a hard-coded country.
/// </summary>
public interface IErpTaxAmountCalculator
{
    Task<ErpTaxAmounts> CalcAsync(
        DbConnection connection,
        DbTransaction? transaction,
        decimal amountExVat,
        int customerUserId,
        int contactId,
        bool export,
        CancellationToken cancellationToken = default);

    /// <summary>Port of PHP <c>epc_tax_toolkit_purchase_amounts</c> (input VAT is only charged when the kit says it is recoverable).</summary>
    Task<ErpPurchaseTaxAmounts> CalcPurchaseAsync(
        DbConnection connection,
        DbTransaction? transaction,
        decimal amountExVat,
        int supplierId,
        bool import,
        CancellationToken cancellationToken = default);
}

public sealed class ErpTaxAmountCalculator : IErpTaxAmountCalculator
{
    private const string FallbackKitCode = "AE-UAE-VAT";
    private const decimal FallbackStandardRate = 5.0m;

    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ErpTaxAmountCalculator(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ErpTaxAmounts> CalcAsync(
        DbConnection connection,
        DbTransaction? transaction,
        decimal amountExVat,
        int customerUserId,
        int contactId,
        bool export,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var amountEx = Round2(decimal.Max(0m, amountExVat));

        var profile = await LoadTenantProfileAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var kitCode = string.IsNullOrWhiteSpace(profile.KitCode) ? FallbackKitCode : profile.KitCode;
        var kit = await LoadKitAsync(connection, transaction, kitCode, cancellationToken).ConfigureAwait(false);
        if (kit is null && !string.Equals(kitCode, FallbackKitCode, StringComparison.Ordinal))
        {
            kitCode = FallbackKitCode;
            kit = await LoadKitAsync(connection, transaction, kitCode, cancellationToken).ConfigureAwait(false);
        }

        var standardRate = kit?.StandardRate ?? FallbackStandardRate;
        var taxLabel = kit is null || string.IsNullOrWhiteSpace(kit.TaxLabel) ? "VAT" : kit.TaxLabel;
        var taxRate = standardRate;
        var taxCategory = "S";
        var reason = "tenant_jurisdiction";

        var zeroRated = export || await IsZeroRatedCustomerAsync(connection, transaction, customerUserId, contactId, cancellationToken).ConfigureAwait(false);
        if (zeroRated)
        {
            taxRate = 0m;
            taxCategory = "Z";
            reason = export ? "export" : "zero_rated_flag";
        }

        var vat = taxRate > 0m ? Round2(amountEx * taxRate / 100m) : 0m;
        return new ErpTaxAmounts(
            amountEx,
            vat,
            Round2(amountEx + vat),
            decimal.Round(taxRate, 3, MidpointRounding.AwayFromZero),
            taxCategory,
            taxLabel,
            kitCode,
            profile.CountryCode,
            reason);
    }

    public async Task<ErpPurchaseTaxAmounts> CalcPurchaseAsync(
        DbConnection connection,
        DbTransaction? transaction,
        decimal amountExVat,
        int supplierId,
        bool import,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var amountEx = Round2(decimal.Max(0m, amountExVat));

        var profile = await LoadTenantProfileAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        var kitCode = string.IsNullOrWhiteSpace(profile.KitCode) ? FallbackKitCode : profile.KitCode;
        var kit = await LoadKitAsync(connection, transaction, kitCode, cancellationToken).ConfigureAwait(false);
        if (kit is null && !string.Equals(kitCode, FallbackKitCode, StringComparison.Ordinal))
        {
            kitCode = FallbackKitCode;
            kit = await LoadKitAsync(connection, transaction, kitCode, cancellationToken).ConfigureAwait(false);
        }

        var importDutyRate = kit?.ImportDutyRate ?? 0m;
        var importDuty = import && (kit?.ImportDutyOnCost ?? false) && importDutyRate > 0m
            ? Round2(amountEx * importDutyRate / 100m)
            : 0m;

        if ((kit?.DelegateUaeVat ?? false) && supplierId > 0)
        {
            var delegated = await CalcDelegatedUaeInputVatAsync(connection, transaction, amountEx, supplierId, cancellationToken).ConfigureAwait(false);
            return new ErpPurchaseTaxAmounts(
                amountEx,
                delegated.VatAmount,
                delegated.TotalAmount,
                delegated.TaxRate,
                delegated.VatApplicable,
                importDuty,
                Round2(amountEx + importDuty),
                Round2(delegated.TotalAmount + importDuty),
                kitCode,
                profile.CountryCode,
                "uae_delegate_purchase");
        }

        var rate = kit?.StandardRate ?? FallbackStandardRate;
        var recoverable = string.Equals(kit?.PurchaseInventoryHook, "vat_on_purchase_recoverable", StringComparison.Ordinal);
        var vat = rate > 0m && recoverable ? Round2(amountEx * rate / 100m) : 0m;
        return new ErpPurchaseTaxAmounts(
            amountEx,
            vat,
            Round2(amountEx + vat),
            decimal.Round(rate, 3, MidpointRounding.AwayFromZero),
            recoverable && rate > 0m,
            importDuty,
            Round2(amountEx + importDuty),
            Round2(amountEx + vat + importDuty),
            kitCode,
            profile.CountryCode,
            "tenant_toolkit_purchase");
    }

    public static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>PHP <c>epc_uae_vat_purchase_amounts</c>: input VAT only for an FTA-ready tenant buying from a TRN-valid UAE supplier.</summary>
    private static async Task<(decimal VatAmount, decimal TotalAmount, decimal TaxRate, bool VatApplicable)> CalcDelegatedUaeInputVatAsync(
        DbConnection connection,
        DbTransaction? transaction,
        decimal amountEx,
        int supplierId,
        CancellationToken cancellationToken)
    {
        var companyCountry = (await SettingAsync(connection, transaction, "company_country_code", "AE", cancellationToken).ConfigureAwait(false)).ToUpperInvariant();
        var companyTrn = DigitsOnly(await SettingAsync(connection, transaction, "company_trn", string.Empty, cancellationToken).ConfigureAwait(false));
        var vatRegisteredRaw = await SettingAsync(connection, transaction, "company_vat_registered", "1", cancellationToken).ConfigureAwait(false);
        var vatRegistered = vatRegisteredRaw.Trim() is "1" or "true" or "yes";
        var ftaReady = companyCountry is "AE" or "ARE" && vatRegistered && companyTrn.Length == 15;
        if (!ftaReady || amountEx <= 0m)
        {
            return (0m, amountEx, 0m, false);
        }

        var supplier = await LoadSupplierTaxRowAsync(connection, transaction, supplierId, cancellationToken).ConfigureAwait(false);
        if (supplier is null
            || !supplier.Value.VatRegistered
            || supplier.Value.CountryCode is not ("AE" or "ARE")
            || DigitsOnly(supplier.Value.Trn).Length != 15)
        {
            return (0m, amountEx, 0m, false);
        }

        var rate = decimal.TryParse(
            await SettingAsync(connection, transaction, "vat_percent", "5.00", cancellationToken).ConfigureAwait(false),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? decimal.Clamp(parsed, 0m, 100m)
            : 5m;
        var vat = Round2(amountEx * rate / 100m);
        return (vat, Round2(amountEx + vat), decimal.Round(rate, 3, MidpointRounding.AwayFromZero), true);
    }

    private static async Task<(bool VatRegistered, string CountryCode, string Trn)?> LoadSupplierTaxRowAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int supplierId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = ErpDb.Positional(
                "SELECT `vat_registered`, `country_code`, `trn` FROM `epc_erp_suppliers` WHERE `id` = ? AND `active` = 1 LIMIT 1");
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p0";
            parameter.Value = supplierId;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return (
                !reader.IsDBNull(0) && reader.GetInt32(0) == 1,
                ReadString(reader.IsDBNull(1) ? null : reader.GetValue(1)).ToUpperInvariant(),
                ReadString(reader.IsDBNull(2) ? null : reader.GetValue(2)));
        }
        catch (DbException)
        {
            return null;
        }
    }

    /// <summary>PHP <c>epc_pricing_get_setting</c> over <c>epc_price_settings</c>.</summary>
    private static async Task<string> SettingAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string key,
        string fallback,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"),
                cancellationToken,
                key).ConfigureAwait(false);
            return value ?? fallback;
        }
        catch (DbException)
        {
            return fallback;
        }
    }

    private static string DigitsOnly(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    private async Task<(string KitCode, string CountryCode)> LoadTenantProfileAsync(
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var siteKey = ResolveSiteKey();
        try
        {
            if (siteKey.Length > 0)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = ErpDb.Positional(
                    "SELECT `kit_code`, `country_code` FROM `epc_tax_toolkit_tenant_profile` WHERE `site_key` = ? LIMIT 1");
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@p0";
                parameter.Value = siteKey;
                command.Parameters.Add(parameter);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return (ReadString(reader["kit_code"]), ReadString(reader["country_code"]).ToUpperInvariant());
                }
            }

            await using var latest = connection.CreateCommand();
            latest.Transaction = transaction;
            latest.CommandText =
                "SELECT `kit_code`, `country_code` FROM `epc_tax_toolkit_tenant_profile` ORDER BY `time_updated` DESC LIMIT 1";
            await using var latestReader = await latest.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await latestReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return (ReadString(latestReader["kit_code"]), ReadString(latestReader["country_code"]).ToUpperInvariant());
            }
        }
        catch (DbException)
        {
            // Tenant has not installed the tax toolkit yet — fall through to the kit default.
        }

        return (string.Empty, string.Empty);
    }

    private sealed record ErpTaxKitRules(
        decimal StandardRate,
        string TaxLabel,
        bool DelegateUaeVat,
        string PurchaseInventoryHook,
        bool ImportDutyOnCost,
        decimal ImportDutyRate);

    private static async Task<ErpTaxKitRules?> LoadKitAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string kitCode,
        CancellationToken cancellationToken)
    {
        string? rulesJson;
        try
        {
            rulesJson = await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `rules_json` FROM `epc_tax_toolkits` WHERE `kit_code` = ? AND `active` = 1 LIMIT 1"),
                cancellationToken,
                kitCode).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(rulesJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rulesJson);
            var root = document.RootElement;
            var rate = root.TryGetProperty("standard_rate", out var rateElement) && TryReadDecimal(rateElement, out var parsed)
                ? parsed
                : FallbackStandardRate;
            var label = root.TryGetProperty("tax_label", out var labelElement) && labelElement.ValueKind == JsonValueKind.String
                ? labelElement.GetString() ?? "VAT"
                : "VAT";
            var delegateUaeVat = root.TryGetProperty("delegate_uae_vat", out var delegateElement)
                && (delegateElement.ValueKind == JsonValueKind.True
                    || (delegateElement.ValueKind == JsonValueKind.Number && TryReadDecimal(delegateElement, out var flag) && flag != 0m)
                    || (delegateElement.ValueKind == JsonValueKind.String && delegateElement.GetString() is "1" or "true"));
            var hooks = root.TryGetProperty("erp_hooks", out var hooksElement) && hooksElement.ValueKind == JsonValueKind.Object
                ? hooksElement
                : default;
            var purchaseHook = hooks.ValueKind == JsonValueKind.Object
                && hooks.TryGetProperty("purchase_inventory", out var purchaseElement)
                && purchaseElement.ValueKind == JsonValueKind.String
                ? purchaseElement.GetString() ?? string.Empty
                : string.Empty;
            var importDutyOnCost = hooks.ValueKind == JsonValueKind.Object
                && hooks.TryGetProperty("import_duty_on_cost", out var dutyHook)
                && (dutyHook.ValueKind == JsonValueKind.True
                    || (dutyHook.ValueKind == JsonValueKind.Number && TryReadDecimal(dutyHook, out var dutyFlag) && dutyFlag != 0m)
                    || (dutyHook.ValueKind == JsonValueKind.String && dutyHook.GetString() is "1" or "true"));
            var importDutyRate = ReadNestedDecimal(root, "trade", "import_duty_default")
                ?? ReadNestedDecimal(root, "import_rules", "import_duty_rate")
                ?? 0m;
            return new ErpTaxKitRules(rate, label, delegateUaeVat, purchaseHook, importDutyOnCost, importDutyRate);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<bool> IsZeroRatedCustomerAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int customerUserId,
        int contactId,
        CancellationToken cancellationToken)
    {
        if (customerUserId <= 0 && contactId <= 0)
        {
            return false;
        }

        try
        {
            var sql = customerUserId > 0
                ? "SELECT `zero_rated` FROM `epc_customer_tax_profile` WHERE `user_id` = ? LIMIT 1"
                : "SELECT `zero_rated` FROM `epc_customer_tax_profile` WHERE `contact_id` = ? LIMIT 1";
            var value = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional(sql),
                cancellationToken,
                customerUserId > 0 ? customerUserId : contactId).ConfigureAwait(false);
            return value == 1;
        }
        catch (DbException)
        {
            return false;
        }
    }

    private static decimal? ReadNestedDecimal(JsonElement root, string section, string property)
    {
        if (!root.TryGetProperty(section, out var sectionElement)
            || sectionElement.ValueKind != JsonValueKind.Object
            || !sectionElement.TryGetProperty(property, out var value)
            || !TryReadDecimal(value, out var parsed))
        {
            return null;
        }

        return parsed;
    }

    private static bool TryReadDecimal(JsonElement element, out decimal value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDecimal(out value);
            case JsonValueKind.String:
                return decimal.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            default:
                value = 0m;
                return false;
        }
    }

    private static string ReadString(object? value)
        => value is null or DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private string ResolveSiteKey()
    {
        var tenant = _httpContextAccessor?.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var siteKey = tenant?.SiteKey ?? string.Empty;
        var normalized = new string(siteKey.ToLowerInvariant().Where(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_').ToArray());
        return normalized;
    }
}
