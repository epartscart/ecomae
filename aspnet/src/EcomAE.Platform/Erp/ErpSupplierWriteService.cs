using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_create_supplier</c> twin (ajax_erp.php <c>create_supplier</c>).
/// Never provisions schema: missing PHP-owned table/columns fail closed.
/// </summary>
public interface IErpSupplierWriteService
{
    Task<long> CreateAsync(ErpSupplierCreateInput input, CancellationToken cancellationToken = default);
}

public sealed record ErpSupplierCreateInput
{
    public long StorageId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string ContactPhone { get; init; } = string.Empty;
    public string Trn { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = "AED";
    public string? CountryCode { get; init; }
    /// <summary>PHP: absent, empty or "1" means registered.</summary>
    public string? VatRegistered { get; init; }
    public string VendorAccount { get; init; } = string.Empty;
    public string VendorGroup { get; init; } = string.Empty;
    public long LegalEntityId { get; init; }
    public long BusinessUnitId { get; init; }
    public string RegistrationNumber { get; init; } = string.Empty;
    public string PaymentTerms { get; init; } = string.Empty;
    public string PaymentMethod { get; init; } = string.Empty;
    public string DeliveryTerms { get; init; } = string.Empty;
    public string DeliveryMode { get; init; } = string.Empty;
    public decimal CreditLimit { get; init; }
    public string OnHold { get; init; } = "no";
    public bool TaxExempt { get; init; }
    public string BankName { get; init; } = string.Empty;
    public string BankAccountNumber { get; init; } = string.Empty;
    public string Iban { get; init; } = string.Empty;
    public string SwiftBic { get; init; } = string.Empty;
    public string ContactPerson { get; init; } = string.Empty;
    public string Website { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string StateRegion { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
}

public sealed class ErpSupplierWriteService : IErpSupplierWriteService
{
    private static readonly string[] OnHoldValues = ["no", "invoice", "payment", "all"];
    private readonly IErpWriteConnectionFactory _connections;

    public ErpSupplierWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static bool VatRegistered(string? raw) => raw is null || raw == "" || raw == "1";

    public static string OnHold(string? raw)
    {
        var v = (raw ?? "no").Trim();
        return OnHoldValues.Contains(v, StringComparer.Ordinal) ? v : "no";
    }

    public async Task<long> CreateAsync(ErpSupplierCreateInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }

        var name = input.Name.Trim();
        if (name.Length == 0)
        {
            throw new ErpWriteException("Supplier name is required");
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                c,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_suppliers` (`storage_id`, `name`, `contact_email`, `contact_phone`, `trn`, `currency_code`, `country_code`, `vat_registered`,"
                    + " `vendor_account`, `vendor_group`, `legal_entity_id`, `business_unit_id`, `registration_number`,"
                    + " `payment_terms`, `payment_method`, `delivery_terms`, `delivery_mode`, `credit_limit`, `on_hold`, `tax_exempt`,"
                    + " `bank_name`, `bank_account_number`, `iban`, `swift_bic`, `contact_person`, `website`,"
                    + " `address`, `city`, `state_region`, `postal_code`, `notes`, `time_created`)"
                    + " VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                input.StorageId > 0 ? input.StorageId : null,
                name,
                input.ContactEmail.Trim(),
                input.ContactPhone.Trim(),
                input.Trn.Trim(),
                (string.IsNullOrWhiteSpace(input.CurrencyCode) ? "AED" : input.CurrencyCode).Trim(),
                ErpEinvoiceProfileWriteService.NormalizeCountry(input.CountryCode),
                VatRegistered(input.VatRegistered) ? 1 : 0,
                input.VendorAccount.Trim(),
                input.VendorGroup.Trim(),
                input.LegalEntityId,
                input.BusinessUnitId,
                input.RegistrationNumber.Trim(),
                input.PaymentTerms.Trim(),
                input.PaymentMethod.Trim(),
                input.DeliveryTerms.Trim(),
                input.DeliveryMode.Trim(),
                Math.Round(input.CreditLimit, 2, MidpointRounding.AwayFromZero),
                OnHold(input.OnHold),
                input.TaxExempt ? 1 : 0,
                input.BankName.Trim(),
                input.BankAccountNumber.Trim(),
                input.Iban.Trim(),
                input.SwiftBic.Trim(),
                input.ContactPerson.Trim(),
                input.Website.Trim(),
                input.Address.Trim(),
                input.City.Trim(),
                input.StateRegion.Trim(),
                input.PostalCode.Trim(),
                input.Notes.Trim(),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            throw new ErpWriteException("epc_erp_suppliers is not writable with the PHP schema: " + ex.Message);
        }

        return await ErpDb.LastInsertIdAsync(c, null, cancellationToken).ConfigureAwait(false);
    }
}
