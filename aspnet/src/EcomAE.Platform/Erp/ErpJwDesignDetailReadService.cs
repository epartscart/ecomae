using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwDesignDetailReadService
{
    Task<ErpJwDesignDetailResult> ReadAsync(
        int companyId, string designCode, CancellationToken cancellationToken = default);
}

public sealed record ErpJwDesignMetalLine(
    int LineNumber, string Division, string Karat, decimal GrossWeight,
    string RateType, decimal MetalRate, decimal AmountForeign, decimal AmountLocal);

public sealed record ErpJwDesignStoneLine(
    int LineNumber, string StoneType, string Shape, string Size, string Color,
    string Clarity, int Pieces, decimal Carat, decimal Rate,
    decimal AmountForeign, decimal AmountLocal);

public sealed record ErpJwDesignDetailResult(
    ErpJwDesignDetail? Design, IReadOnlyList<ErpJwDesignMetalLine> Metals,
    IReadOnlyList<ErpJwDesignStoneLine> Stones, string Source, string Message);

public sealed record ErpJwDesignDetail(
    long Id, string DesignCode, string Description, string Currency,
    string Type, string Category, string SubCategory, string Brand,
    decimal CostAmount, decimal Price1Local, decimal Price2Local,
    decimal MetalTotalQuantity, decimal StoneTotalQuantity);

public sealed class ErpJwDesignDetailReadService : IErpJwDesignDetailReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    public ErpJwDesignDetailReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    public async Task<ErpJwDesignDetailResult> ReadAsync(
        int companyId, string designCode, CancellationToken cancellationToken = default)
    {
        if (companyId <= 0 || string.IsNullOrWhiteSpace(designCode))
            return Empty("Company and design code are required.", "invalid");
        if (!_connections.IsConfigured) return Empty("TenantRegistry DB is not configured.", "migration");
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var detail = await ReadHeaderAsync(connection, companyId, designCode.Trim(), cancellationToken);
            if (detail is null) return Empty("Design was not found for the selected company.", "database");
            var metals = await ReadMetalsAsync(connection, detail.Id, cancellationToken);
            var stones = await ReadStonesAsync(connection, detail.Id, cancellationToken);
            return new(detail, metals, stones, "database", string.Empty);
        }
        catch (DbException exception) { return Empty(exception.Message, "database-error"); }
    }

    private static async Task<ErpJwDesignDetail?> ReadHeaderAsync(
        DbConnection connection, int companyId, string designCode, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `id`,`design_code`,`description`,`currency`,`type`,`category`,
                   `sub_category`,`brand`,`cost_amount`,`price1_lc`,`price2_lc`,
                   `metal_total_qty`,`stone_total_qty`
            FROM `epc_jewel_design`
            WHERE `company_id`=? AND `design_code`=?
            LIMIT 1
            """);
        ErpDb.AddParameters(command, companyId, designCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        return new(reader.GetInt64(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
            Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7),
            reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10),
            reader.GetDecimal(11), reader.GetDecimal(12));
    }

    private static async Task<IReadOnlyList<ErpJwDesignMetalLine>> ReadMetalsAsync(
        DbConnection connection, long designId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `line_no`,`division`,`karat`,`gross_wt`,`rate_type`,`metal_rate`,
                   `amount_fc`,`amount_lc`
            FROM `epc_jewel_design_metals`
            WHERE `design_id`=?
            ORDER BY `line_no`
            """);
        ErpDb.AddParameters(command, designId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<ErpJwDesignMetalLine>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(new(reader.GetInt32(0), Text(reader, 1), Text(reader, 2), reader.GetDecimal(3),
                Text(reader, 4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7)));
        return rows;
    }

    private static async Task<IReadOnlyList<ErpJwDesignStoneLine>> ReadStonesAsync(
        DbConnection connection, long designId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `line_no`,`stone_type`,`shape`,`size`,`color`,`clarity`,`pcs`,`carat`,
                   `rate`,`amount_fc`,`amount_lc`
            FROM `epc_jewel_design_stones`
            WHERE `design_id`=?
            ORDER BY `line_no`
            """);
        ErpDb.AddParameters(command, designId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new List<ErpJwDesignStoneLine>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(new(reader.GetInt32(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                Text(reader, 4), Text(reader, 5), reader.GetInt32(6), reader.GetDecimal(7),
                reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10)));
        return rows;
    }

    private static ErpJwDesignDetailResult Empty(string message, string source)
        => new(null, [], [], source, message);
    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
