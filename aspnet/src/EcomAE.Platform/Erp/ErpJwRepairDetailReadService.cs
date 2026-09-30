using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwRepairDetailReadService
{
    Task<ErpJwRepairDetailResult> ReadAsync(
        int companyId, long repairId, CancellationToken cancellationToken = default);
}

public sealed record ErpJwRepairDetailItem(
    int LineNumber, string Division, string StockCode, string Description,
    string ItemRemarks, string BagNumber, int Pieces, decimal GrossWeight,
    string RepairType, string ItemType, string RepairStatus, string DeliveryDate,
    string StatusDetail);

public sealed record ErpJwRepairDetail(
    long Id, int CompanyId, string Branch, string VoucherType, string VoucherDate,
    int VoucherNumber, long CustomerId, string CustomerName, string Mobile,
    string Salesman, string Currency, string DeliveryDate, string RepairNarration,
    string Status, bool Authorized, IReadOnlyList<ErpJwRepairDetailItem> Items);

public sealed record ErpJwRepairDetailResult(
    ErpJwRepairDetail? Repair, string Source, string Message);

public sealed class ErpJwRepairDetailReadService : IErpJwRepairDetailReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwRepairDetailReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public async Task<ErpJwRepairDetailResult> ReadAsync(
        int companyId, long repairId, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0 || repairId <= 0)
            return Empty("Company and repair id are required.", "invalid");
        if (!_connections.IsConfigured)
            return Empty("TenantRegistry DB is not configured.", "migration");

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var header = connection.CreateCommand();
        header.CommandText = ErpDb.Positional("""
            SELECT `id`,`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,
                   `customer_id`,`customer_name`,`mobile`,`salesman`,`currency`,
                   `delivery_date`,`repair_narration`,`status`,`authorized`
            FROM `epc_jewel_repair`
            WHERE `company_id`=? AND `id`=?
            LIMIT 1
            """);
        ErpDb.AddParameters(header, companyId, repairId);

        try
        {
            await using var reader = await header.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return Empty("Repair receipt was not found.", "database");

            var repair = new ErpJwRepairDetail(
                reader.GetInt64(0), reader.GetInt32(1), Text(reader, 2), Text(reader, 3),
                Text(reader, 4), reader.GetInt32(5), reader.GetInt64(6), Text(reader, 7),
                Text(reader, 8), Text(reader, 9), Text(reader, 10), Text(reader, 11),
                Text(reader, 12), Text(reader, 13), reader.GetBoolean(14), []);

            await reader.DisposeAsync().ConfigureAwait(false);
            await using var items = connection.CreateCommand();
            items.CommandText = ErpDb.Positional("""
                SELECT `line_no`,`division`,`stock_code`,`description`,`item_remarks`,
                       `bag_no`,`pcs`,`gr_wt`,`repair_type`,`item_type`,`repair_status`,
                       `delivery_date`,`status_detail`
                FROM `epc_jewel_repair_items`
                WHERE `repair_id`=?
                ORDER BY `line_no`
                """);
            ErpDb.AddParameters(items, repair.Id);
            var lines = new List<ErpJwRepairDetailItem>();
            await using var itemReader = await items.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await itemReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new(
                    itemReader.GetInt32(0), Text(itemReader, 1), Text(itemReader, 2),
                    Text(itemReader, 3), Text(itemReader, 4), Text(itemReader, 5),
                    itemReader.GetInt32(6), itemReader.GetDecimal(7), Text(itemReader, 8),
                    Text(itemReader, 9), Text(itemReader, 10), Text(itemReader, 11),
                    Text(itemReader, 12)));
            }

            return new(repair with { Items = lines }, "database", string.Empty);
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }
    }

    private static ErpJwRepairDetailResult Empty(string message, string source)
        => new(null, source, message);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
