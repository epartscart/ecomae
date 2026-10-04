using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>PHP <c>epc_erp_active_company_id</c> equivalent for finance-advanced writers: first active legal entity, or the hint when it is active.</summary>
internal static class ErpFinAdvancedCompany
{
    public static async Task<long> ResolveAsync(DbConnection connection, long hint, CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, "epc_erp_pm_legal_entities", "id", cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var ids = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `active`=1 ORDER BY `id`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        if (ids.Count == 0)
        {
            return 0;
        }

        return hint > 0 && ids.Contains(hint) ? hint : ids[0];
    }

    public static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
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
