using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpLandedCostWriteService
{
    Task<ErpSimpleWriteResult> CalculateAsync(long sheetId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PostAsync(long sheetId, CancellationToken cancellationToken = default);
}

public sealed class ErpLandedCostWriteService : IErpLandedCostWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpLandedCostWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CalculateAsync(long sheetId, CancellationToken cancellationToken = default)
    {
        if (sheetId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A landed-cost sheet id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ErpLazySchema.EnsureLandedCostAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var status = await ErpDb.StringAsync(connection, transaction,
            ErpDb.Positional("SELECT `status` FROM `epc_landed_cost_sheets` WHERE `id`=? FOR UPDATE"),
            cancellationToken, sheetId).ConfigureAwait(false);
        if (status is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Landed-cost sheet not found.");
        }

        if (status is not ("draft" or "calculated"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Only draft or calculated sheets can be recalculated.");
        }

        var method = await ErpDb.StringAsync(connection, transaction,
            ErpDb.Positional("SELECT `distribution_method` FROM `epc_landed_cost_sheets` WHERE `id`=?"),
            cancellationToken, sheetId).ConfigureAwait(false) ?? "value";
        var expenses = await ErpDb.DecimalAsync(connection, transaction,
            ErpDb.Positional("SELECT COALESCE(SUM(`amount_local`),0) FROM `epc_landed_cost_expenses` WHERE `sheet_id`=?"),
            cancellationToken, sheetId).ConfigureAwait(false);

        var lines = new List<LandedCostLine>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT `id`,`qty`,`unit_cost`,`line_value`,`weight`,`volume`
                FROM `epc_landed_cost_lines`
                WHERE `sheet_id`=?
                ORDER BY `id`
                """;
            var parameter = command.CreateParameter();
            parameter.Value = sheetId;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new LandedCostLine(
                    Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(2), System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader.GetValue(5), System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        if (lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cannot calculate a sheet without allocation lines.");
        }

        var basis = lines.Sum(line => Basis(method, line));
        if (basis <= 0m)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cannot distribute landed cost because the total basis is zero.");
        }

        foreach (var line in lines)
        {
            var share = Basis(method, line) / basis * expenses;
            var allocated = line.Qty > 0m ? decimal.Round(share / line.Qty, 4) : 0m;
            var newCost = decimal.Round(line.UnitCost + allocated, 4);
            await ErpDb.ExecuteAsync(connection, transaction,
                ErpDb.Positional("UPDATE `epc_landed_cost_lines` SET `allocated_cost`=?, `new_unit_cost`=? WHERE `id`=?"),
                cancellationToken, allocated, newCost, line.Id).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(connection, transaction,
            ErpDb.Positional("UPDATE `epc_landed_cost_sheets` SET `total_expenses`=?, `status`='calculated' WHERE `id`=?"),
            cancellationToken, expenses, sheetId).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok($"Landed cost calculated for {lines.Count} lines.", sheetId);
    }

    public async Task<ErpSimpleWriteResult> PostAsync(long sheetId, CancellationToken cancellationToken = default)
    {
        if (sheetId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A landed-cost sheet id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var updated = await ErpDb.ExecuteAsync(connection, null,
            ErpDb.Positional("UPDATE `epc_landed_cost_sheets` SET `status`='posted', `posted_at`=NOW() WHERE `id`=? AND `status`='calculated'"),
            cancellationToken, sheetId).ConfigureAwait(false);
        return updated == 0
            ? ErpSimpleWriteResult.Fail("invalid", "Only a calculated landed-cost sheet can be posted.")
            : ErpSimpleWriteResult.Ok("Landed costs posted to inventory.", sheetId);
    }

    public static decimal Basis(string method, LandedCostLine line) => method switch
    {
        "weight" => line.Weight * line.Qty,
        "volume" => line.Volume * line.Qty,
        "quantity" or "qty" => line.Qty,
        "equal" => 1m,
        _ => line.LineValue,
    };

    public sealed record LandedCostLine(long Id, decimal Qty, decimal UnitCost, decimal LineValue, decimal Weight, decimal Volume);
}
