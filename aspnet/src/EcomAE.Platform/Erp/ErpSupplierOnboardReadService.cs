using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpPayableAccountRow(long Id, string Code, string Name);

public sealed record ErpSupplierOnboardRow(
    long Id,
    string Code,
    string Name,
    string GroupType,
    string Category,
    string Trn,
    string ApprovalStatus,
    long ApCoaId,
    string ApCoaLabel);

public sealed record ErpSupplierOnboardDocumentRow(long Id, string Category, string FileName, long TimeCreated);

public sealed record ErpSupplierOnboardSnapshot(
    ErpSupplierOnboardRow? Header,
    IReadOnlyList<ErpSupplierOnboardOwner> Owners,
    IReadOnlyList<ErpSupplierOnboardDocumentRow> Documents,
    IReadOnlyList<ErpSupplierOnboardCheck> Checks,
    bool CanApprove,
    IReadOnlyList<ErpPayableAccountRow> PayableAccounts,
    IReadOnlyDictionary<long, string> StatusById,
    string Source,
    string Message);

public interface IErpSupplierOnboardReadService
{
    Task<ErpSupplierOnboardSnapshot> LoadAsync(long supplierId, int limit = 200, CancellationToken cancellationToken = default);
}

public sealed class ErpSupplierOnboardReadService : IErpSupplierOnboardReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpSupplierOnboardReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSupplierOnboardSnapshot> LoadAsync(long supplierId, int limit = 200, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(null, [], [], [], false, [], new Dictionary<long, string>(), "migration", "TenantRegistry DB is not configured.");
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureSuppliersAsync(c, cancellationToken).ConfigureAwait(false);
        await ErpGlChartOfAccountsSeeder.EnsureAsync(c, cancellationToken).ConfigureAwait(false);
        var accounts = await LoadPayableAccountsAsync(c, limit, cancellationToken).ConfigureAwait(false);
        var statuses = await LoadStatusesAsync(c, limit, cancellationToken).ConfigureAwait(false);
        if (supplierId <= 0)
        {
            return new(null, [], [], [], false, accounts, statuses, "database", string.Empty);
        }

        var state = await ErpSupplierWriteService.LoadOnboardStateAsync(c, supplierId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return new(null, [], [], [], false, accounts, statuses, "database", "Supplier not found.");
        }

        var header = await LoadHeaderAsync(c, supplierId, accounts, cancellationToken).ConfigureAwait(false);
        var docs = await LoadDocumentsAsync(c, supplierId, cancellationToken).ConfigureAwait(false);
        var checks = ErpSupplierOnboard.Evaluate(state);
        return new(header, state.Owners, docs, checks, ErpSupplierOnboard.CanApprove(checks), accounts, statuses, "database", string.Empty);
    }

    private static async Task<ErpSupplierOnboardRow?> LoadHeaderAsync(
        DbConnection c,
        long supplierId,
        IReadOnlyList<ErpPayableAccountRow> accounts,
        CancellationToken cancellationToken)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT `id`, IFNULL(`supplier_code`,IFNULL(`vendor_account`,'')), IFNULL(`name`,''), IFNULL(`supplier_group_type`,IFNULL(`vendor_group`,'')), IFNULL(`supplier_category`,''), IFNULL(`trn`,''), IFNULL(`approval_status`,'approved'), IFNULL(`ap_coa_id`,0) FROM `epc_erp_suppliers` WHERE `id`=" + supplierId.ToString(CultureInfo.InvariantCulture) + " LIMIT 1";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var apId = Convert.ToInt64(reader.GetValue(7), CultureInfo.InvariantCulture);
        var ap = accounts.FirstOrDefault(a => a.Id == apId);
        var apLabel = ap is null ? (apId > 0 ? "#" + apId.ToString(CultureInfo.InvariantCulture) : "") : ap.Code + " · " + ap.Name;
        return new(
            Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            apId,
            apLabel);
    }

    private static async Task<IReadOnlyList<ErpSupplierOnboardDocumentRow>> LoadDocumentsAsync(
        DbConnection c,
        long supplierId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT `id`, IFNULL(`doc_category`,''), IFNULL(`file_name`,''), IFNULL(`time_created`,0) FROM `epc_erp_documents` WHERE `entity_type`='supplier' AND `entity_id`=" + supplierId.ToString(CultureInfo.InvariantCulture) + " AND IFNULL(`active`,1)=1 ORDER BY `id` DESC";
            var rows = new List<ErpSupplierOnboardDocumentRow>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.GetString(1),
                    reader.GetString(2),
                    Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture)));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<ErpPayableAccountRow>> LoadPayableAccountsAsync(
        DbConnection c,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            var safe = Math.Clamp(limit, 1, 500);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT `id`, IFNULL(`code`,''), IFNULL(`name`,'') FROM `epc_erp_coa_accounts` WHERE IFNULL(`active`,1)=1 AND (`account_type` IN ('liability','expense') OR IFNULL(`name`,'') LIKE '%payable%' OR `code` LIKE '2%') ORDER BY `code` ASC LIMIT " + safe.ToString(CultureInfo.InvariantCulture);
            var rows = new List<ErpPayableAccountRow>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.GetString(1),
                    reader.GetString(2)));
            }

            if (rows.Count == 0)
            {
                await using var all = c.CreateCommand();
                all.CommandText = "SELECT `id`, IFNULL(`code`,''), IFNULL(`name`,'') FROM `epc_erp_coa_accounts` WHERE IFNULL(`active`,1)=1 ORDER BY `code` ASC LIMIT " + safe.ToString(CultureInfo.InvariantCulture);
                await using var reader2 = await all.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader2.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new(
                        Convert.ToInt64(reader2.GetValue(0), CultureInfo.InvariantCulture),
                        reader2.GetString(1),
                        reader2.GetString(2)));
                }
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    private static async Task<IReadOnlyDictionary<long, string>> LoadStatusesAsync(
        DbConnection c,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            var safe = Math.Clamp(limit, 1, 500);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT `id`, IFNULL(`approval_status`,'approved') FROM `epc_erp_suppliers` ORDER BY `id` DESC LIMIT " + safe.ToString(CultureInfo.InvariantCulture);
            var map = new Dictionary<long, string>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                map[Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture)] = reader.GetString(1);
            }

            return map;
        }
        catch (DbException)
        {
            return new Dictionary<long, string>();
        }
    }
}
