using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Empties investment-portfolio operational rows so a real-estate tenant can enter live data.
/// Does not drop schema, users, chart of accounts, or tax settings.
/// </summary>
public static class ErpRealEstatePortfolioReset
{
    public static readonly string[] Tables =
    [
        "epc_erp_re_year_lines",
        "epc_erp_re_expense_splits",
        "epc_erp_re_expenses",
        "epc_erp_re_floors",
        "epc_erp_re_communities",
        "epc_erp_re_portfolios",
        "epc_erp_re_imports",
        "epc_erp_re_instalments",
        "epc_erp_re_cost_allocs",
        "epc_erp_re_escrow",
        "epc_erp_re_milestones",
        "epc_erp_re_boq_lines",
        "epc_erp_re_certificates",
        "epc_erp_re_contracts",
        "epc_erp_re_sales",
        "epc_erp_re_rent_invoices",
        "epc_erp_re_cheques",
        "epc_erp_re_deposits",
        "epc_erp_re_alerts",
        "epc_erp_re_documents",
        "epc_erp_re_leases",
        "epc_erp_re_valuations",
        "epc_erp_re_disposals",
        "epc_erp_re_acquisitions",
        "epc_erp_re_owners",
        "epc_erp_re_units",
        "epc_erp_re_projects",
        "epc_erp_re_contractors",
        "epc_erp_re_tenants",
        "epc_erp_re_opportunities",
        "epc_erp_property_maintenance",
        "epc_erp_properties",
    ];

    public static async Task<int> ClearAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var writes = 0;
        writes += await DeleteReJournalsAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var table in Tables)
        {
            try
            {
                writes += await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    "DELETE FROM `" + table + "`",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // Tenant may not have created a later construction table yet.
            }
        }

        return writes;
    }

    private static async Task<int> DeleteReJournalsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var ids = new HashSet<long>();
        foreach (var table in new[]
        {
            "epc_erp_re_acquisitions",
            "epc_erp_re_valuations",
            "epc_erp_re_deposits",
            "epc_erp_re_cheques",
            "epc_erp_re_certificates",
            "epc_erp_re_sales",
            "epc_erp_re_escrow",
            "epc_erp_re_disposals",
            "epc_erp_property_maintenance",
        })
        {
            await CollectIdsAsync(connection, table, "gl_journal_id", ids, cancellationToken).ConfigureAwait(false);
            await CollectIdsAsync(connection, table, "gl_clear_journal_id", ids, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `id` FROM `epc_erp_gl_journals` WHERE `reference` LIKE 'RE-%'";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt64(reader[0], System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        catch (DbException)
        {
        }

        if (ids.Count == 0)
        {
            return 0;
        }

        var list = string.Join(",", ids);
        var lines = 0;
        try
        {
            lines += await ErpDb.ExecuteAsync(
                connection,
                null,
                "DELETE FROM `epc_erp_gl_lines` WHERE `journal_id` IN (" + list + ")",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        var journals = 0;
        try
        {
            journals += await ErpDb.ExecuteAsync(
                connection,
                null,
                "DELETE FROM `epc_erp_gl_journals` WHERE `id` IN (" + list + ")",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        return lines + journals;
    }

    private static async Task CollectIdsAsync(
        DbConnection connection,
        string table,
        string column,
        HashSet<long> ids,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `" + column + "` FROM `" + table + "` WHERE IFNULL(`" + column + "`,0) > 0";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt64(reader[0], System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        catch (DbException)
        {
        }
    }
}
