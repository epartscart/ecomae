using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cc_center_save</c> and <c>epc_cc_post_allocation</c> twins.
/// The PHP-owned tables must already exist; this service never creates schema.
/// </summary>
public interface IErpCostCenterWriteService
{
    Task<ErpSimpleWriteResult> SaveCenterAsync(
        long id,
        string? code,
        string? name,
        long branchId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PostAllocationAsync(
        string? runLabel,
        decimal sourceCost,
        IReadOnlyDictionary<long, decimal>? weights,
        CancellationToken cancellationToken = default);
}

public sealed class ErpCostCenterWriteService : IErpCostCenterWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCostCenterWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveCenterAsync(
        long id,
        string? code,
        string? name,
        long branchId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = Clip(code, 30);
        var normalizedName = Clip(name, 160);
        if (id <= 0 && normalizedCode.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A cost-centre code is required.");
        }

        if (branchId < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Branch id cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_cc_centers", "code", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cost-centre table is not provisioned.");
        }

        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_cc_centers` SET `name`=?, `branch_id`=? WHERE `id`=?"),
                cancellationToken,
                normalizedName,
                branchId,
                id);
            return ErpSimpleWriteResult.Ok("Cost centre saved.", id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_cc_centers` (`code`,`name`,`branch_id`,`time_created`) VALUES (?,?,?,?)"),
            cancellationToken,
            normalizedCode,
            normalizedName,
            branchId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Cost centre saved.", inserted);
    }

    public async Task<ErpSimpleWriteResult> PostAllocationAsync(
        string? runLabel,
        decimal sourceCost,
        IReadOnlyDictionary<long, decimal>? weights,
        CancellationToken cancellationToken = default)
    {
        var normalizedLabel = Clip(runLabel, 80);
        if (normalizedLabel.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An allocation run label is required.");
        }

        var allocation = Allocate(sourceCost, weights);
        if (allocation.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one positive cost-centre weight is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_cc_allocations", "cost_center_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cost-allocation table is not provisioned.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var pair in allocation)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    INSERT INTO `epc_cc_allocations`
                    (`run_label`,`source_cost`,`cost_center_id`,`amount`,`time_created`)
                    VALUES (?,?,?,?,?)
                    """),
                cancellationToken,
                normalizedLabel,
                decimal.Round(sourceCost, 2, MidpointRounding.AwayFromZero),
                pair.Key,
                pair.Value,
                now);
        }

        return ErpSimpleWriteResult.Ok(
            "Cost allocation posted (" + allocation.Count.ToString(CultureInfo.InvariantCulture) + " line(s)).",
            allocation.Count);
    }

    public static IReadOnlyDictionary<long, decimal> ParseWeights(string? text)
    {
        var weights = new Dictionary<long, decimal>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return weights;
        }

        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = raw.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                || id <= 0
                || !decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var weight))
            {
                continue;
            }

            weights[id] = weight;
        }

        return weights;
    }

    public static IReadOnlyDictionary<long, decimal> Allocate(
        decimal sourceCost,
        IReadOnlyDictionary<long, decimal>? weights)
    {
        if (weights is null)
        {
            return new Dictionary<long, decimal>();
        }

        var positive = weights
            .Where(pair => pair.Key > 0 && pair.Value > 0)
            .ToArray();
        var totalWeight = positive.Sum(pair => pair.Value);
        if (positive.Length == 0 || totalWeight <= 0)
        {
            return new Dictionary<long, decimal>();
        }

        var output = new Dictionary<long, decimal>();
        decimal allocated = 0;
        var largest = positive[0];
        foreach (var pair in positive)
        {
            if (pair.Value > largest.Value)
            {
                largest = pair;
            }

            var amount = decimal.Round(sourceCost * pair.Value / totalWeight, 2, MidpointRounding.AwayFromZero);
            output[pair.Key] = amount;
            allocated = decimal.Round(allocated + amount, 2, MidpointRounding.AwayFromZero);
        }

        var remainder = decimal.Round(sourceCost - allocated, 2, MidpointRounding.AwayFromZero);
        if (remainder != 0)
        {
            output[largest.Key] = decimal.Round(output[largest.Key] + remainder, 2, MidpointRounding.AwayFromZero);
        }

        return output;
    }

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return count > 0;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
