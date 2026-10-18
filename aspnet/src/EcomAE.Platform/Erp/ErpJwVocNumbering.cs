using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// PHP <c>epc_jewel_voc_next_no</c> / repair-next-no twin: MAX(voc_no)+1 per
/// company, type, and branch. Callers that already have a positive voc_no keep it.
/// </summary>
public static class ErpJwVocNumbering
{
    private static readonly HashSet<string> AllowedTables = new(StringComparer.Ordinal)
    {
        "epc_jewel_voucher",
        "epc_jewel_repair",
        "epc_jewel_repair_transfer",
        "epc_jewel_repair_workshop_receive",
        "epc_jewel_repair_delivery",
        "epc_jewel_stock_verification",
        "epc_jewel_tourist_vat_refund",
    };

    public static async Task<int> NextNoAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string table,
        int companyId,
        string vocType,
        string branch,
        int requested,
        CancellationToken cancellationToken = default)
    {
        if (requested > 0)
        {
            return requested;
        }

        if (!AllowedTables.Contains(table))
        {
            throw new ArgumentOutOfRangeException(nameof(table), table, "Unknown jewellery voucher table.");
        }

        var max = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "SELECT COALESCE(MAX(`voc_no`),0) FROM `" + table + "` WHERE `company_id`=? AND `voc_type`=? AND `branch`=?"),
            cancellationToken,
            companyId < 0 ? 0 : companyId,
            vocType,
            string.IsNullOrWhiteSpace(branch) ? "HQ" : branch).ConfigureAwait(false);
        return (int)max + 1;
    }
}
