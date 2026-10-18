using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpIndustryPulseReadService
{
    Task<ErpIndustryPulse> ReadAsync(int companyId, CancellationToken cancellationToken = default);
}

public sealed record ErpIndustryPulse(
    decimal GoldSell24k,
    string GoldDate,
    int MetalItems,
    decimal MetalGrams,
    int RepairJobs,
    int FitOutRecords,
    decimal FitOutAmount)
{
    public static ErpIndustryPulse Empty { get; } = new(0, "", 0, 0, 0, 0, 0);
}

/// <summary>Read-only industry figures for the executive summary. Missing tables stay at zero.</summary>
public sealed class ErpIndustryPulseReadService : IErpIndustryPulseReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpIndustryPulseReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpIndustryPulse> ReadAsync(int companyId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpIndustryPulse.Empty;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var company = companyId < 0 ? 0 : companyId;
            var gold = await GoldAsync(connection, company, cancellationToken).ConfigureAwait(false);
            var metal = await MetalAsync(connection, company, cancellationToken).ConfigureAwait(false);
            var repairs = await CountAsync(connection, "epc_jewel_repair", company, cancellationToken).ConfigureAwait(false);
            var fitOut = await FitOutAsync(connection, cancellationToken).ConfigureAwait(false);
            return new(gold.Rate, gold.Date, metal.Items, metal.Grams, repairs, fitOut.Records, fitOut.Amount);
        }
        catch (DbException)
        {
            return ErpIndustryPulse.Empty;
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            return ErpIndustryPulse.Empty;
        }
    }

    private static async Task<(decimal Rate, string Date)> GoldAsync(DbConnection connection, int companyId, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "epc_gold_rates", cancellationToken).ConfigureAwait(false))
        {
            return (0, "");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `sell_rate`,`rate_date` FROM `epc_gold_rates` WHERE `karat`='24K' AND `sell_rate`>0 AND (?=0 OR `company_id`=?) ORDER BY `rate_date` DESC LIMIT 1");
        ErpDb.AddParameters(command, companyId, companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (0, "");
        }

        var rate = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0), CultureInfo.InvariantCulture);
        var date = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "";
        return (rate, date);
    }

    private static async Task<(int Items, decimal Grams)> MetalAsync(DbConnection connection, int companyId, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "epc_jewel_metal_stock", cancellationToken).ConfigureAwait(false))
        {
            return (0, 0);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT COUNT(*), COALESCE(SUM(`stock_gms`),0) FROM `epc_jewel_metal_stock` WHERE (?=0 OR `company_id`=?)");
        ErpDb.AddParameters(command, companyId, companyId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (0, 0);
        }

        var items = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
        var grams = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
        return (items, grams);
    }

    private static async Task<int> CountAsync(DbConnection connection, string table, int companyId, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT COUNT(*) FROM `" + table + "` WHERE (?=0 OR `company_id`=?)");
        ErpDb.AddParameters(command, companyId, companyId);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<(int Records, decimal Amount)> FitOutAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "ecomae_fitout_delivery_records", cancellationToken).ConfigureAwait(false))
        {
            return (0, 0);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*), COALESCE(SUM(`amount`),0) FROM `ecomae_fitout_delivery_records`";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (0, 0);
        }

        var records = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
        var amount = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
        return (records, amount);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return count > 0;
    }
}
