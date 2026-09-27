using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpOpeningPostBatchWriteService
{
    Task<ErpSimpleWriteResult> PostAsync(
        ErpOpeningPostBatchWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOpeningPostBatchWriteRequest(long BatchId = 0, int AdminId = 0);

public sealed class ErpOpeningPostBatchWriteService : IErpOpeningPostBatchWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    public ErpOpeningPostBatchWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> PostAsync(
        ErpOpeningPostBatchWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BatchId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A valid opening batch is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var batch = await ReadBatchAsync(connection, request.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Opening batch not found or already posted.");
        }

        if (batch.Lines.Any(line => line.Type is not ("coa" or "cash_bank")))
        {
            return ErpSimpleWriteResult.Fail("unsupported_line", "This batch contains an opening line type that ASP.NET has not implemented for live posting.");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var line in batch.Lines)
            {
                var balance = Math.Round(line.Debit - line.Credit, 2, MidpointRounding.AwayFromZero);
                if (line.Type == "coa" && line.EntityId > 0)
                {
                    await ErpDb.ExecuteAsync(connection, transaction,
                        ErpDb.Positional("UPDATE `epc_erp_coa_accounts` SET `opening_balance` = ? WHERE `id` = ?"),
                        cancellationToken, balance, line.EntityId).ConfigureAwait(false);
                }
                else if (line.Type == "cash_bank" && line.EntityId > 0
                    && await ColumnExistsAsync(connection, "epc_erp_cash_bank_accounts", "opening_balance", cancellationToken).ConfigureAwait(false))
                {
                    await ErpDb.ExecuteAsync(connection, transaction,
                        ErpDb.Positional("UPDATE `epc_erp_cash_bank_accounts` SET `opening_balance` = ? WHERE `id` = ?"),
                        cancellationToken, balance, line.EntityId).ConfigureAwait(false);
                }
            }

            await ErpDb.ExecuteAsync(connection, transaction,
                ErpDb.Positional("UPDATE `epc_erp_opening_batches` SET `status` = 'posted', `time_posted` = ? WHERE `id` = ? AND `status` = 'draft'"),
                cancellationToken, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), request.BatchId).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Opening batch posted", request.BatchId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<OpeningBatch?> ReadBatchAsync(
        DbConnection connection,
        long batchId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `id`, IFNULL(`as_of_date`, ''), IFNULL(`reference`, '')
            FROM `epc_erp_opening_batches`
            WHERE `id` = ? AND `status` = 'draft'
            LIMIT 1
            """;
        Add(command, batchId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var batch = new OpeningBatch(
            Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "",
            Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "",
            []);
        await reader.CloseAsync();

        await using var lines = connection.CreateCommand();
        lines.CommandText = """
            SELECT IFNULL(`line_type`, ''), IFNULL(`entity_id`, 0), IFNULL(`debit`, 0),
                   IFNULL(`credit`, 0), IFNULL(`qty`, 0), IFNULL(`unit_cost`, 0),
                   IFNULL(JSON_UNQUOTE(JSON_EXTRACT(`meta_json`, '$.warehouse_id')), 0),
                   IFNULL(JSON_UNQUOTE(JSON_EXTRACT(`meta_json`, '$.batch_no')), ''),
                   IFNULL(JSON_UNQUOTE(JSON_EXTRACT(`meta_json`, '$.expiry_date')), '')
            FROM `epc_erp_opening_lines`
            WHERE `batch_id` = ?
            ORDER BY `id` ASC
            """;
        Add(lines, batchId);
        var result = new List<OpeningLine>();
        await using var lineReader = await lines.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await lineReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new(
                Convert.ToString(lineReader.GetValue(0), CultureInfo.InvariantCulture) ?? "",
                Convert.ToInt64(lineReader.GetValue(1), CultureInfo.InvariantCulture),
                Convert.ToDecimal(lineReader.GetValue(2), CultureInfo.InvariantCulture),
                Convert.ToDecimal(lineReader.GetValue(3), CultureInfo.InvariantCulture),
                Convert.ToDecimal(lineReader.GetValue(4), CultureInfo.InvariantCulture),
                Convert.ToDecimal(lineReader.GetValue(5), CultureInfo.InvariantCulture),
                Convert.ToInt64(lineReader.GetValue(6), CultureInfo.InvariantCulture),
                Convert.ToString(lineReader.GetValue(7), CultureInfo.InvariantCulture) ?? "",
                Convert.ToString(lineReader.GetValue(8), CultureInfo.InvariantCulture) ?? ""));
        }
        return batch with { Lines = result };
    }

    private static void Add(DbCommand command, object value)
    {
        var parameter = command.CreateParameter();
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(connection, null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken, table, column).ConfigureAwait(false) > 0;

    private sealed record OpeningBatch(long Id, string AsOfDate, string Reference, IReadOnlyList<OpeningLine> Lines);
    private sealed record OpeningLine(string Type, long EntityId, decimal Debit, decimal Credit, decimal Quantity, decimal UnitCost, long WarehouseId, string BatchNo, string ExpiryDate);
}
