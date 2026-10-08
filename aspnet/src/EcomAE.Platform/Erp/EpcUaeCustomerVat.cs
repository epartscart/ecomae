using System.Data.Common;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Erp;

/// <summary>The country, trade type and tax-exempt flag PHP <c>epc_uae_customer_vat_context()</c> decides the VAT type from.</summary>
public sealed record EpcUaeCustomerVatContext(string CountryCode, string CustomerType, bool TaxExempt);

/// <summary>
/// The <c>customer_vat_type</c> sync of the PHP UAE customer VAT library: the context from the
/// customer's profile and e-invoice buyer profile, the resolved type and its save.
/// </summary>
public static class EpcUaeCustomerVat
{
    public const string ProfileKey = "customer_vat_type";

    private static readonly string[] GccCodes = ["SA", "BH", "OM", "KW", "QA"];

    /// <summary>PHP <c>epc_uae_customer_vat_is_gcc()</c>.</summary>
    public static bool IsGcc(string? code)
    {
        var c = EpcEinvoiceBuyer.AsciiUpper(EpcEinvoiceBuyer.PhpTrim(code));
        return c.Length > 0 && c != "AE" && GccCodes.Contains(c);
    }

    /// <summary>PHP <c>epc_uae_customer_vat_context()</c>: the buyer profile country wins over <c>epc_reg_country</c>.</summary>
    public static async Task<EpcUaeCustomerVatContext> ContextAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
    {
        var country = "AE";
        var customerType = "retail";
        var taxExempt = false;
        if (userId > 0)
        {
            customerType = await EpcCustomerTrade.ProfileGetAsync(connection, transaction, userId, "epc_customer_type", cancellationToken, "retail").ConfigureAwait(false);
            if (customerType.Length == 0)
            {
                customerType = "retail";
            }

            country = EpcEinvoiceBuyer.AsciiUpper(EpcEinvoiceBuyer.PhpTrim(await EpcCustomerTrade.ProfileGetAsync(connection, transaction, userId, "epc_reg_country", cancellationToken, "AE").ConfigureAwait(false)));
            if (country.Length == 0)
            {
                country = "AE";
            }

            var taxStatus = await EpcCustomerTrade.ProfileGetAsync(connection, transaction, userId, "epc_tax_exempt_cert_status", cancellationToken).ConfigureAwait(false);
            taxExempt = customerType == "wholesale" && taxStatus == "approved";

            var buyer = await EpcEinvoiceBuyer.BuyerProfileAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
            if (!EpcEinvoiceBuyer.PhpEmpty(buyer.GetValueOrDefault("country_code")))
            {
                country = EpcEinvoiceBuyer.NormalizeCountry(buyer["country_code"]);
            }
        }

        return new EpcUaeCustomerVatContext(EpcEinvoiceBuyer.NormalizeCountry(country), customerType, taxExempt);
    }

    /// <summary>PHP <c>epc_uae_customer_vat_resolve_type()</c>.</summary>
    public static async Task<string> ResolveTypeAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
    {
        var context = await ContextAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
        if (context.TaxExempt)
        {
            return "tax_exempt";
        }

        if (EpcEinvoiceBuyer.NormalizeCountry(context.CountryCode) != "AE")
        {
            return IsGcc(context.CountryCode) ? "gcc" : "export";
        }

        return context.CustomerType == "wholesale" ? "local_b2b" : "local_b2c";
    }

    /// <summary>PHP <c>epc_uae_customer_vat_sync()</c>: resolve the type and store it for a customer.</summary>
    public static async Task<string> SyncAsync(DbConnection connection, DbTransaction? transaction, long userId, CancellationToken cancellationToken)
    {
        var type = await ResolveTypeAsync(connection, transaction, userId, cancellationToken).ConfigureAwait(false);
        if (userId > 0)
        {
            await EpcCustomerTrade.ProfileSetAsync(connection, transaction, userId, ProfileKey, type, cancellationToken).ConfigureAwait(false);
        }

        return type;
    }
}
