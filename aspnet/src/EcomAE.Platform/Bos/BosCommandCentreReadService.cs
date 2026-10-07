using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Read-only attention rows from the ERP and CRM tables Devin already owns.
/// A failing query omits that source. This service does not create schema and does not post.
/// </summary>
public interface IBosCommandCentreReadService
{
    Task<BosCommandCentreLoad> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class BosCommandCentreReadService : IBosCommandCentreReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly TimeProvider _clock;

    public BosCommandCentreReadService(IErpWriteConnectionFactory connections, TimeProvider? clock = null)
    {
        _connections = connections;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<BosCommandCentreLoad> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return BosCommandCentreLoad.NotConfigured;
        }

        var readAt = _clock.GetUtcNow();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var slices = new List<BosSourceSlice>
            {
                await ReadSalesOrdersAsync(connection, cancellationToken).ConfigureAwait(false),
                await ReadPurchaseOrdersAsync(connection, cancellationToken).ConfigureAwait(false),
                await ReadJournalsAsync(connection, cancellationToken).ConfigureAwait(false),
                await ReadStockAsync(connection, cancellationToken).ConfigureAwait(false),
                await ReadOpportunitiesAsync(connection, cancellationToken).ConfigureAwait(false),
            };
            return new BosCommandCentreLoad(true, readAt, slices);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new BosCommandCentreLoad(true, readAt,
            [
                Unavailable("erp.sales_order", "Sales"),
                Unavailable("erp.purchase_order", "Procurement"),
                Unavailable("erp.gl_journal", "Finance"),
                Unavailable("erp.stock", "Inventory"),
                Unavailable("crm.opportunity", "Relationship"),
            ]);
        }
    }

    private static BosSourceSlice Unavailable(string source, string category)
        => new(source, category, false, false, []);

    private static Task<BosSourceSlice> ReadSalesOrdersAsync(DbConnection connection, CancellationToken cancellationToken)
        => ReadDocumentsAsync(
            connection,
            "erp.sales_order",
            "Sales",
            "SELECT `id`, `so_no`, IFNULL(`title`, ''), `status`, `total_amount`, `company_id` FROM `epc_erp_sales_orders` WHERE `status` = 'draft' ORDER BY `id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            "SELECT `id`, `so_no`, IFNULL(`title`, ''), `status`, `total_amount` FROM `epc_erp_sales_orders` WHERE `status` = 'draft' ORDER BY `id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            includeAmount: true,
            includeProbability: false,
            cancellationToken);

    private static async Task<BosSourceSlice> ReadPurchaseOrdersAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var draft = await ReadDocumentsAsync(
            connection,
            "erp.purchase_order",
            "Procurement",
            "SELECT `id`, `po_no`, IFNULL(`title`, ''), `status`, `total_amount`, `company_id` FROM `epc_erp_purchase_orders` WHERE `status` = 'draft' ORDER BY `id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            "SELECT `id`, `po_no`, IFNULL(`title`, ''), `status`, `total_amount` FROM `epc_erp_purchase_orders` WHERE `status` = 'draft' ORDER BY `id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            includeAmount: true,
            includeProbability: false,
            cancellationToken).ConfigureAwait(false);
        if (!draft.Available)
        {
            return draft;
        }

        var pending = await ReadDocumentsAsync(
            connection,
            "erp.purchase_order",
            "Procurement",
            "SELECT `id`, `po_no`, IFNULL(`title`, ''), `status`, `total_amount`, `company_id` FROM `epc_erp_purchase_orders` WHERE `status` = 'pending' ORDER BY `id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            "SELECT `id`, `po_no`, IFNULL(`title`, ''), `status`, `total_amount` FROM `epc_erp_purchase_orders` WHERE `status` = 'pending' ORDER BY `id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            includeAmount: true,
            includeProbability: false,
            cancellationToken).ConfigureAwait(false);

        var rows = draft.Rows.ToList();
        if (pending.Available)
        {
            rows.AddRange(pending.Rows.Where(row => rows.All(existing => existing.Id != row.Id)));
        }

        var complete = rows.Count < BosCommandCentreComposer.RowLimit;
        if (rows.Count > BosCommandCentreComposer.RowLimit)
        {
            rows = rows.Take(BosCommandCentreComposer.RowLimit).ToList();
            complete = false;
        }

        return new BosSourceSlice("erp.purchase_order", "Procurement", true, complete, rows);
    }

    private static Task<BosSourceSlice> ReadJournalsAsync(DbConnection connection, CancellationToken cancellationToken)
        => ReadDocumentsAsync(
            connection,
            "erp.gl_journal",
            "Finance",
            "SELECT `id`, `journal_no`, IFNULL(`description`, ''), `status`, `company_id` FROM `epc_erp_gl_journals` WHERE `active` = 1 AND `status` = 'draft' ORDER BY `id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            "SELECT `id`, `journal_no`, IFNULL(`description`, ''), `status` FROM `epc_erp_gl_journals` WHERE `active` = 1 AND `status` = 'draft' ORDER BY `id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            includeAmount: false,
            includeProbability: false,
            cancellationToken);

    private static Task<BosSourceSlice> ReadStockAsync(DbConnection connection, CancellationToken cancellationToken)
        => ReadDocumentsAsync(
            connection,
            "erp.stock",
            "Inventory",
            "SELECT s.`id`, IFNULL(i.`sku`, ''), IFNULL(i.`name`, ''), CONCAT('on hand ', IFNULL(s.`qty_on_hand`, 0)), s.`company_id` FROM `epc_erp_inv_stock` s INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` AND i.`active` = 1 WHERE i.`reorder_level` > 0 AND s.`qty_on_hand` <= i.`reorder_level` ORDER BY s.`id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            "SELECT s.`id`, IFNULL(i.`sku`, ''), IFNULL(i.`name`, ''), CONCAT('on hand ', IFNULL(s.`qty_on_hand`, 0)) FROM `epc_erp_inv_stock` s INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` AND i.`active` = 1 WHERE i.`reorder_level` > 0 AND s.`qty_on_hand` <= i.`reorder_level` ORDER BY s.`id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            includeAmount: false,
            includeProbability: false,
            cancellationToken);

    private static async Task<BosSourceSlice> ReadOpportunitiesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var slice = await ReadDocumentsAsync(
            connection,
            "crm.opportunity",
            "Relationship",
            "SELECT `id`, IFNULL(`title`, ''), IFNULL(`title`, ''), IFNULL(`stage`, ''), `amount`, `probability`, `company_id` FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` NOT IN ('won', 'lost') ORDER BY `id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            "SELECT `id`, IFNULL(`title`, ''), IFNULL(`title`, ''), IFNULL(`stage`, ''), `amount`, `probability` FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` NOT IN ('won', 'lost') ORDER BY `id` DESC LIMIT "
            + BosCommandCentreComposer.RowLimit.ToString(CultureInfo.InvariantCulture),
            includeAmount: true,
            includeProbability: true,
            cancellationToken).ConfigureAwait(false);
        if (!slice.Available)
        {
            return slice;
        }

        long? count = await TryCountAsync(
            connection,
            "SELECT COUNT(*) FROM `epc_crm_opportunities` WHERE `active` = 1 AND `stage` NOT IN ('won', 'lost')",
            cancellationToken).ConfigureAwait(false);
        var complete = count is long total
            ? total == slice.Rows.Count
            : slice.Rows.Count < BosCommandCentreComposer.RowLimit;
        return slice with { Complete = complete };
    }

    private static async Task<BosSourceSlice> ReadDocumentsAsync(
        DbConnection connection,
        string source,
        string category,
        string sqlWithCompany,
        string sqlWithoutCompany,
        bool includeAmount,
        bool includeProbability,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await QueryAsync(connection, sqlWithCompany, source, category, includeAmount, includeProbability, readCompany: true, cancellationToken).ConfigureAwait(false);
            return new BosSourceSlice(source, category, true, rows.Count < BosCommandCentreComposer.RowLimit, rows);
        }
        catch (DbException)
        {
            try
            {
                var rows = await QueryAsync(connection, sqlWithoutCompany, source, category, includeAmount, includeProbability, readCompany: false, cancellationToken).ConfigureAwait(false);
                return new BosSourceSlice(source, category, true, rows.Count < BosCommandCentreComposer.RowLimit, rows);
            }
            catch (DbException)
            {
                return new BosSourceSlice(source, category, false, false, []);
            }
        }
    }

    private static async Task<IReadOnlyList<BosSourceDocument>> QueryAsync(
        DbConnection connection,
        string sql,
        string source,
        string category,
        bool includeAmount,
        bool includeProbability,
        bool readCompany,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<BosSourceDocument>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var ordinal = 0;
            var id = Convert.ToInt64(reader.GetValue(ordinal++), CultureInfo.InvariantCulture);
            var reference = Cell(reader, ordinal++);
            var title = Cell(reader, ordinal++);
            var status = Cell(reader, ordinal++);
            decimal? amount = null;
            int? probability = null;
            if (includeAmount)
            {
                amount = reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
                ordinal++;
            }

            if (includeProbability)
            {
                probability = reader.IsDBNull(ordinal)
                    ? null
                    : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
                ordinal++;
            }

            int? companyId = null;
            if (readCompany && !reader.IsDBNull(ordinal))
            {
                var company = Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
                if (company > 0 && company <= int.MaxValue)
                {
                    companyId = (int)company;
                }
            }

            rows.Add(new BosSourceDocument(source, category, id, reference, title, status, amount, probability, companyId));
        }

        return rows;
    }

    private static async Task<long?> TryCountAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static string Cell(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return string.Empty;
        }

        return Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
