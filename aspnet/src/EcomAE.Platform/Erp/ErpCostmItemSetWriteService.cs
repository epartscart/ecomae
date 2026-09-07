using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_costm_item_set</c> / ajax <c>costm_item_set</c> twin.
/// UPSERT <c>epc_costm_item</c> on <c>company_id</c>+<c>item_id</c>.
/// Transaction add, closing run, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpCostmItemSetWriteService
{
    Task<ErpSimpleWriteResult> SetAsync(
        ErpCostmItemSetWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCostmItemSetWriteRequest(
    long CompanyId = 0,
    long ItemId = 0,
    string? Model = null,
    decimal StdCost = 0);

public sealed class ErpCostmItemSetWriteService : IErpCostmItemSetWriteService
{
    public static readonly HashSet<string> Models = new(StringComparer.Ordinal)
    {
        "standard", "fifo", "lifo", "moving_avg",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpCostmItemSetWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetAsync(
        ErpCostmItemSetWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var model = request.Model ?? "moving_avg";
        var invalid = Validate(model);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var stdCost = decimal.Round(request.StdCost, 4, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_costm_item", "model", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_costm_item", "std_cost", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Costing model table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_costm_item` (`company_id`,`item_id`,`model`,`std_cost`,`time_updated`) VALUES (?,?,?,?,?) "
                + "ON DUPLICATE KEY UPDATE `model`=VALUES(`model`), `std_cost`=VALUES(`std_cost`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            companyId,
            request.ItemId,
            model,
            stdCost,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_costm_item` WHERE `company_id`=? AND `item_id`=? LIMIT 1"),
                cancellationToken,
                companyId,
                request.ItemId).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Costing model saved", id);
    }

    public static string? Validate(string model)
        => Models.Contains(model) ? null : "Invalid costing model";

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
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
