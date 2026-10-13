using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_create_supplier</c> twin (ajax_erp.php <c>create_supplier</c>).
/// PHP <c>epc_erp_create_supplier</c> calls <c>epc_erp_ensure_schema</c> first so a fresh
/// tenant (no prior AP write) still gets the supplier master table.
/// </summary>
public interface IErpSupplierWriteService
{
    Task<long> CreateAsync(ErpSupplierCreateInput input, CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_erp_sync_suppliers_from_storages</c>: one supplier per <c>shop_storages</c> row without an active supplier. Returns created count.</summary>
    Task<int> SyncFromStoragesAsync(CancellationToken cancellationToken = default);

    Task ApproveAsync(long supplierId, int adminId, CancellationToken cancellationToken = default);
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
    public string SupplierCode { get; init; } = string.Empty;
    public long ApCoaId { get; init; }
    public string SupplierGroupType { get; init; } = string.Empty;
    public string SupplierCategory { get; init; } = string.Empty;
    public string LegalName { get; init; } = string.Empty;
    public string Building { get; init; } = string.Empty;
    public string Street { get; init; } = string.Empty;
    public string Emirate { get; init; } = string.Empty;
    public IReadOnlyList<ErpSupplierOnboardOwner> Owners { get; init; } = [];
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
        await ErpLazySchema.EnsureSuppliersAsync(c, cancellationToken).ConfigureAwait(false);
        var code = await NextCodeAsync(c, input.SupplierCode, input.VendorAccount, cancellationToken).ConfigureAwait(false);
        var vendorAccount = string.IsNullOrWhiteSpace(input.VendorAccount) ? code : input.VendorAccount.Trim();
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
                ErpSupplierOnboard.NormalizeTrn(input.Trn),
                (string.IsNullOrWhiteSpace(input.CurrencyCode) ? "AED" : input.CurrencyCode).Trim(),
                ErpEinvoiceProfileWriteService.NormalizeCountry(input.CountryCode),
                VatRegistered(input.VatRegistered) ? 1 : 0,
                vendorAccount,
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
                (string.IsNullOrWhiteSpace(input.Address) ? input.Street : input.Address).Trim(),
                input.City.Trim(),
                input.StateRegion.Trim(),
                input.PostalCode.Trim(),
                input.Notes.Trim(),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or ArgumentException)
        {
            throw new ErpWriteException("epc_erp_suppliers is not writable with the PHP schema: " + ex.Message);
        }

        var id = await ErpDb.LastInsertIdAsync(c, null, cancellationToken).ConfigureAwait(false);
        await AssignOnboardAsync(c, id, input, code, cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task ApproveAsync(long supplierId, int adminId, CancellationToken cancellationToken = default)
    {
        if (supplierId <= 0)
        {
            throw new ErpWriteException("Supplier is required");
        }

        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureSuppliersAsync(c, cancellationToken).ConfigureAwait(false);
        var state = await LoadOnboardStateAsync(c, supplierId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Supplier not found");
        var checks = ErpSupplierOnboard.Evaluate(state);
        if (!ErpSupplierOnboard.CanApprove(checks))
        {
            throw new ErpWriteException("Supplier is not ready to approve: " + ErpSupplierOnboard.MissingSummary(checks));
        }

        await ErpDb.ExecuteAsync(
            c,
            null,
            ErpDb.Positional("UPDATE `epc_erp_suppliers` SET `approval_status`=?, `approved_by`=?, `approved_at`=? WHERE `id`=?"),
            cancellationToken,
            ErpSupplierOnboard.StatusApproved,
            adminId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            supplierId).ConfigureAwait(false);
    }

    public static async Task RequireApprovedForPurchaseAsync(DbConnection connection, long supplierId, CancellationToken cancellationToken)
    {
        if (supplierId <= 0)
        {
            throw new ErpWriteException("Supplier is required");
        }

        var has = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_erp_suppliers' AND COLUMN_NAME = 'approval_status'"),
            cancellationToken).ConfigureAwait(false);
        if (has <= 0)
        {
            return;
        }

        var status = (await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`approval_status`,'') FROM `epc_erp_suppliers` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            supplierId).ConfigureAwait(false) ?? "").Trim();
        if (status.Length == 0 || status.Equals(ErpSupplierOnboard.StatusApproved, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new ErpWriteException("Supplier is not approved for purchase. Complete UAE e-invoice, ownership and documents, then approve.");
    }

    public async Task<int> SyncFromStoragesAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureSuppliersAsync(c, cancellationToken).ConfigureAwait(false);
        try
        {
            var storages = new List<(long Id, string Name)>();
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, `name`, `short_name` FROM `shop_storages`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var name = (r.IsDBNull(1) ? string.Empty : r.GetString(1)).Trim();
                    if (name.Length == 0)
                    {
                        name = (r.IsDBNull(2) ? string.Empty : r.GetString(2)).Trim();
                    }

                    if (name.Length > 0)
                    {
                        storages.Add((Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), name));
                    }
                }
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var created = 0;
            foreach (var (id, name) in storages)
            {
                created += await ErpDb.ExecuteAsync(
                    c,
                    null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_suppliers` (`storage_id`, `name`, `time_created`) SELECT ?, ?, ? FROM DUAL"
                        + " WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_suppliers` WHERE `storage_id` = ? AND `active` = 1)"),
                    cancellationToken,
                    id,
                    name,
                    now,
                    id).ConfigureAwait(false);
            }

            if (created > 0)
            {
                await BackfillDraftCodesAsync(c, cancellationToken).ConfigureAwait(false);
            }

            return created;
        }
        catch (DbException ex)
        {
            throw new ErpWriteException("Supplier sync requires the PHP shop_storages/epc_erp_suppliers schema: " + ex.Message);
        }
    }

    private static async Task<string> NextCodeAsync(
        DbConnection c,
        string requested,
        string vendorAccount,
        CancellationToken cancellationToken)
    {
        var prefer = !string.IsNullOrWhiteSpace(requested) ? requested.Trim() : vendorAccount.Trim();
        if (prefer.Length > 0)
        {
            return prefer;
        }

        var existing = new List<string>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT IFNULL(`vendor_account`,''), IFNULL(`supplier_code`,'') FROM `epc_erp_suppliers`";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            existing.Add(reader.GetString(0));
            existing.Add(reader.GetString(1));
        }

        return ErpSupplierOnboard.NextSupplierCode(existing);
    }

    private static async Task AssignOnboardAsync(
        DbConnection c,
        long id,
        ErpSupplierCreateInput input,
        string code,
        CancellationToken cancellationToken)
    {
        var groupType = ErpSupplierOnboard.NormalizeGroupType(input.SupplierGroupType);
        if (groupType.Length == 0)
        {
            groupType = ErpSupplierOnboard.NormalizeGroupType(input.VendorGroup);
        }

        var city = input.City.Trim();
        var emirate = input.Emirate.Trim();
        var region = input.StateRegion.Trim();
        if (region.Length == 0)
        {
            region = emirate;
        }

        try
        {
            await ErpDb.ExecuteAsync(
                c,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_erp_suppliers` SET `supplier_code`=?, `vendor_account`=CASE WHEN IFNULL(`vendor_account`,'')='' THEN ? ELSE `vendor_account` END,"
                    + " `ap_coa_id`=?, `supplier_group_type`=?, `supplier_category`=?, `legal_name`=?, `building`=?, `street`=?,"
                    + " `emirate`=?, `state_region`=?, `scheme_id`=?, `approval_status`=? WHERE `id`=?"),
                cancellationToken,
                code,
                code,
                input.ApCoaId,
                groupType,
                ErpSupplierOnboard.NormalizeCategory(input.SupplierCategory),
                string.IsNullOrWhiteSpace(input.LegalName) ? input.Name.Trim() : input.LegalName.Trim(),
                input.Building.Trim(),
                input.Street.Trim(),
                emirate,
                region,
                ErpSupplierOnboard.UaeSchemeId,
                ErpSupplierOnboard.StatusDraft,
                id).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // onboard columns not present — PHP core insert still succeeded
        }

        foreach (var owner in input.Owners ?? [])
        {
            if (string.IsNullOrWhiteSpace(owner.FullName))
            {
                continue;
            }

            try
            {
                await ErpDb.ExecuteAsync(
                    c,
                    null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_supplier_owners` (`supplier_id`,`full_name`,`nationality`,`id_number`,`role`,`share_pct`,`time_created`) VALUES (?,?,?,?,?,?,?)"),
                    cancellationToken,
                    id,
                    owner.FullName.Trim(),
                    owner.Nationality.Trim(),
                    owner.IdNumber.Trim(),
                    string.IsNullOrWhiteSpace(owner.Role) ? "owner" : owner.Role.Trim(),
                    Math.Round(owner.SharePct, 3, MidpointRounding.AwayFromZero),
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            }
            catch (DbException)
            {
                break;
            }
        }
    }

    private static async Task BackfillDraftCodesAsync(DbConnection c, CancellationToken cancellationToken)
    {
        var existing = new List<string>();
        var missing = new List<long>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, IFNULL(`vendor_account`,''), IFNULL(`supplier_code`,'') FROM `epc_erp_suppliers`";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                var vendor = reader.GetString(1).Trim();
                var code = reader.GetString(2).Trim();
                if (vendor.Length > 0)
                {
                    existing.Add(vendor);
                }

                if (code.Length > 0)
                {
                    existing.Add(code);
                }
                else
                {
                    missing.Add(id);
                }
            }
        }

        foreach (var id in missing)
        {
            var next = ErpSupplierOnboard.NextSupplierCode(existing);
            existing.Add(next);
            try
            {
                await ErpDb.ExecuteAsync(
                    c,
                    null,
                    ErpDb.Positional(
                        "UPDATE `epc_erp_suppliers` SET `supplier_code`=?, `vendor_account`=CASE WHEN IFNULL(`vendor_account`,'')='' THEN ? ELSE `vendor_account` END, `approval_status`=? WHERE `id`=?"),
                    cancellationToken,
                    next,
                    next,
                    ErpSupplierOnboard.StatusDraft,
                    id).ConfigureAwait(false);
            }
            catch (DbException)
            {
                break;
            }
        }
    }

    internal static async Task<ErpSupplierOnboardState?> LoadOnboardStateAsync(
        DbConnection c,
        long supplierId,
        CancellationToken cancellationToken)
    {
        string name = "", code = "", currency = "", address = "", street = "", city = "", person = "", phone = "", group = "", category = "", country = "", trn = "";
        long apCoa = 0;
        var vat = true;
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT IFNULL(`name`,''), IFNULL(`supplier_code`,IFNULL(`vendor_account`,'')), IFNULL(`ap_coa_id`,0), IFNULL(`currency_code`,''), IFNULL(`address`,''), IFNULL(`street`,''), IFNULL(`city`,''), IFNULL(`contact_person`,''), IFNULL(`contact_phone`,''), IFNULL(`supplier_group_type`,IFNULL(`vendor_group`,'')), IFNULL(`supplier_category`,''), IFNULL(`country_code`,''), IFNULL(`vat_registered`,1), IFNULL(`trn`,'') FROM `epc_erp_suppliers` WHERE `id`=" + supplierId.ToString(CultureInfo.InvariantCulture) + " LIMIT 1";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            name = reader.GetString(0);
            code = reader.GetString(1);
            apCoa = Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture);
            currency = reader.GetString(3);
            address = reader.GetString(4);
            street = reader.GetString(5);
            city = reader.GetString(6);
            person = reader.GetString(7);
            phone = reader.GetString(8);
            group = reader.GetString(9);
            category = reader.GetString(10);
            country = reader.GetString(11);
            vat = Convert.ToInt64(reader.GetValue(12), CultureInfo.InvariantCulture) != 0;
            trn = reader.GetString(13);
        }

        var owners = new List<ErpSupplierOnboardOwner>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT IFNULL(`full_name`,''), IFNULL(`share_pct`,0), IFNULL(`nationality`,''), IFNULL(`id_number`,''), IFNULL(`role`,'owner') FROM `epc_erp_supplier_owners` WHERE `supplier_id`=" + supplierId.ToString(CultureInfo.InvariantCulture);
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    owners.Add(new(
                        reader.GetString(0),
                        Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4)));
                }
            }
            catch (DbException)
            {
                // table not present yet
            }
        }

        var docs = new List<string>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT IFNULL(`doc_category`,'') FROM `epc_erp_documents` WHERE `entity_type`='supplier' AND `entity_id`=" + supplierId.ToString(CultureInfo.InvariantCulture) + " AND IFNULL(`active`,1)=1";
            try
            {
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    docs.Add(reader.GetString(0));
                }
            }
            catch (DbException)
            {
                // documents table not present yet
            }
        }

        return new ErpSupplierOnboardState(name, code, apCoa, currency, address, street, city, person, phone, group, category, country, vat, trn, owners, docs);
    }
}
