using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwFinanceHistoryReadService
{
    Task<ErpJwFinanceHistoryResult> ReadPettyCashAsync(
        int companyId, string from, string to, CancellationToken cancellationToken = default);

    Task<ErpJwFinanceHistoryResult> ReadTouristVatAsync(
        int companyId, string from, string to, CancellationToken cancellationToken = default);
}

public sealed record ErpJwFinanceHistoryRow(
    long Id,
    string Branch,
    string VoucherType,
    string VoucherDate,
    int VoucherNumber,
    string PartyCode,
    string PartyName,
    string Description,
    decimal DebitAmount,
    decimal CreditAmount,
    string Currency,
    string Status,
    string Remarks);

public sealed record ErpJwFinanceHistoryResult(
    IReadOnlyList<ErpJwFinanceHistoryRow> Rows,
    string Source,
    string Message);

public sealed class ErpJwFinanceHistoryReadService : IErpJwFinanceHistoryReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwFinanceHistoryReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public Task<ErpJwFinanceHistoryResult> ReadPettyCashAsync(
        int companyId, string from, string to, CancellationToken cancellationToken = default)
        => ReadAsync("""
            SELECT `id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`description`,
                   `account_code`,`account_name`,`debit_amount`,`credit_amount`,
                   `currency`,`status`,`remarks`
            FROM `epc_jewel_petty_cash`
            WHERE `company_id`=? AND `voc_date` BETWEEN ? AND ?
            ORDER BY `voc_no` DESC
            """, companyId, from, to, cancellationToken, false);

    public Task<ErpJwFinanceHistoryResult> ReadTouristVatAsync(
        int companyId, string from, string to, CancellationToken cancellationToken = default)
        => ReadAsync("""
            SELECT `id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`party_code`,
                   `party_name`,`party_phone`,`party_email`,`party_ref_number`,
                   `party_ref_date`,`date_from`,`date_to`,`salesman`,
                   `journal_ref`,`remarks`,`status`
            FROM `epc_jewel_tourist_vat_refund`
            WHERE `company_id`=? AND `voc_date` BETWEEN ? AND ?
            ORDER BY `voc_no` DESC
            """, companyId, from, to, cancellationToken, true);

    private async Task<ErpJwFinanceHistoryResult> ReadAsync(
        string sql, int companyId, string from, string to,
        CancellationToken cancellationToken, bool touristVat)
    {
        if (companyId <= 0 || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return Empty("Company and date range are required.", "invalid");
        }

        if (!_connections.IsConfigured)
        {
            return Empty("TenantRegistry DB is not configured.", "migration");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, companyId, from.Trim(), to.Trim());
        var rows = new List<ErpJwFinanceHistoryRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(touristVat
                    ? new(reader.GetInt64(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                        reader.GetInt32(4), Text(reader, 5), Text(reader, 6), string.Empty, 0, 0,
                        string.Empty, Text(reader, 16), Text(reader, 15))
                    : new(reader.GetInt64(0), Text(reader, 1), Text(reader, 2), Text(reader, 3),
                        reader.GetInt32(4), Text(reader, 6), Text(reader, 7), Text(reader, 5),
                        reader.GetDecimal(8), reader.GetDecimal(9), Text(reader, 10),
                        Text(reader, 11), Text(reader, 12)));
            }
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }

        return new(rows, "database", string.Empty);
    }

    private static ErpJwFinanceHistoryResult Empty(string message, string source)
        => new([], source, message);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
