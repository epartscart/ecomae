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
        var taxLabel = kit is null || string.IsNullOrWhiteSpace(kit.Value.TaxLabel) ? "VAT" : kit.Value.TaxLabel;
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

    public static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

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

    private static async Task<(decimal StandardRate, string TaxLabel)?> LoadKitAsync(
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
            return (rate, label);
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
