using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cons_figures_save</c> twin. UPSERT <c>epc_cons_figures</c>.
/// Schema ensure and IC save stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpConsFiguresSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpConsFiguresSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpConsFiguresSaveWriteRequest(
    string? EntityCode = null,
    decimal Revenue = 0,
    decimal Expenses = 0,
    decimal Assets = 0,
    decimal Liabilities = 0,
    decimal Equity = 0);

public sealed class ErpConsFiguresSaveWriteService : IErpConsFiguresSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpConsFiguresSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpConsFiguresSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = Clip((request.EntityCode ?? string.Empty).Trim().ToUpperInvariant(), 40);
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Entity is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var revenue = decimal.Round(request.Revenue, 2, MidpointRounding.AwayFromZero);
        var expenses = decimal.Round(request.Expenses, 2, MidpointRounding.AwayFromZero);
        var assets = decimal.Round(request.Assets, 2, MidpointRounding.AwayFromZero);
        var liabilities = decimal.Round(request.Liabilities, 2, MidpointRounding.AwayFromZero);
        var equity = decimal.Round(request.Equity, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_cons_figures", "entity_code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_cons_figures", "revenue", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Consolidation figures table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_cons_figures` (`entity_code`,`revenue`,`expenses`,`assets`,`liabilities`,`equity`,`time_updated`) VALUES (?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `revenue`=VALUES(`revenue`),`expenses`=VALUES(`expenses`),`assets`=VALUES(`assets`),`liabilities`=VALUES(`liabilities`),`equity`=VALUES(`equity`),`time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            code,
            revenue,
            expenses,
            assets,
            liabilities,
            equity,
            now).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_cons_figures` WHERE `entity_code`=? LIMIT 1"),
                cancellationToken,
                code).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Financials saved for " + code, id);
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
