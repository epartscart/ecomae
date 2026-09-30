using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwFixingReadService
{
    Task<ErpJwFixingResult> ReadAsync(
        int companyId,
        string? fixType = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwFixingRow(
    long Id,
    int CompanyId,
    string Branch,
    string FixType,
    string FixDate,
    int FixNumber,
    string PartyCode,
    string PartyName,
    string Metal,
    string Karat,
    string RateType,
    decimal FixRate,
    decimal FixQuantityGrams,
    decimal FixAmount,
    decimal UnfixedQuantity,
    string ReferenceVoucher,
    string Status,
    string Remarks);

public sealed record ErpJwFixingResult(
    IReadOnlyList<ErpJwFixingRow> Rows,
    string Source,
    string Message);

public sealed class ErpJwFixingReadService : IErpJwFixingReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwFixingReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public async Task<ErpJwFixingResult> ReadAsync(
        int companyId,
        string? fixType = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (companyId <= 0)
        {
            return Empty("Company is required.", "invalid");
        }

        if (!_connections.IsConfigured)
        {
            return Empty("TenantRegistry DB is not configured.", "migration");
        }

        var normalizedType = fixType?.Trim() ?? string.Empty;
        var boundedLimit = Math.Clamp(limit, 1, 200);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT `id`,`company_id`,`branch`,`fix_type`,`fix_date`,`fix_no`,
                   `party_code`,`party_name`,`metal`,`karat`,`rate_type`,
                   `fix_rate`,`fix_qty_gms`,`fix_amount`,`unfixed_qty`,
                   `reference_voc`,`status`,`remarks`
            FROM `epc_jewel_fixing`
            WHERE `company_id`=? AND (? = '' OR `fix_type`=?)
            ORDER BY `id` DESC
            LIMIT {boundedLimit}
            """);
        ErpDb.AddParameters(command, companyId, normalizedType, normalizedType);

        var rows = new List<ErpJwFixingRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    reader.GetInt32(1),
                    Text(reader, 2),
                    Text(reader, 3),
                    Text(reader, 4),
                    reader.GetInt32(5),
                    Text(reader, 6),
                    Text(reader, 7),
                    Text(reader, 8),
                    Text(reader, 9),
                    Text(reader, 10),
                    reader.GetDecimal(11),
                    reader.GetDecimal(12),
                    reader.GetDecimal(13),
                    reader.GetDecimal(14),
                    Text(reader, 15),
                    Text(reader, 16),
                    Text(reader, 17)));
            }
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }

        return new(rows, "database", string.Empty);
    }

    private static ErpJwFixingResult Empty(string message, string source)
        => new([], source, message);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
