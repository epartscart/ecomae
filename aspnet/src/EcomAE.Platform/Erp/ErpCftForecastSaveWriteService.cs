using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cft_forecast_save</c> / ajax <c>cft_forecast_save</c> twin.
/// UPDATE <c>epc_cft_forecast</c> when <c>id</c> &gt; 0, else INSERT.
/// Line add, instruments, projection, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpCftForecastSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpCftForecastSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCftForecastSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Name = null,
    decimal OpeningBalance = 0,
    string? Currency = null,
    string? Notes = null);

public sealed class ErpCftForecastSaveWriteService : IErpCftForecastSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCftForecastSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpCftForecastSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Forecast name is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var currency = request.Currency ?? string.Empty;
        var notes = request.Notes ?? string.Empty;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_cft_forecast", "name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cash forecast table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_cft_forecast` SET `name`=?, `opening_balance`=?, `currency`=?, `notes`=? WHERE `id`=?"),
                cancellationToken,
                name,
                request.OpeningBalance,
                currency,
                notes,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Forecast saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_cft_forecast` (`company_id`,`name`,`opening_balance`,`currency`,`notes`,`time_created`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            name,
            request.OpeningBalance,
            currency,
            notes,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Forecast saved", id);
    }

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
