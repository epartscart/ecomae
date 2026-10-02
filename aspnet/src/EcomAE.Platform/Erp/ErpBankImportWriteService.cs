using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpBankImportWriteService
{
    Task<ErpSimpleWriteResult> ImportAsync(
        ErpBankImportWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBankImportWriteRequest(
    long AccountId = 0,
    string? CsvText = null,
    long AdminId = 0);

public sealed class ErpBankImportWriteService : IErpBankImportWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpBankImportWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ImportAsync(
        ErpBankImportWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AccountId <= 0)
        {
            return ErpSimpleWriteResult.Fail("validation", "Account required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_erp_bank_statement_lines", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Bank statement table is not provisioned");
        }

        var rows = ParseCsv(request.CsvText);
        var batch = $"IMP-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var inserted = 0;
        long firstId = 0;

        foreach (var row in rows)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("""
                    INSERT INTO `epc_erp_bank_statement_lines`
                        (`account_id`, `line_date`, `description`, `reference`, `amount`,
                         `direction`, `import_batch`, `time_created`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                request.AccountId,
                row.LineDate,
                row.Description,
                row.Reference,
                row.Amount,
                row.Direction,
                batch,
                now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(
                connection,
                transaction,
                cancellationToken).ConfigureAwait(false);
            firstId = firstId == 0 ? id : firstId;
            inserted++;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(
            true,
            "ok",
            $"Imported {inserted} bank statement line(s).",
            firstId,
            inserted);
    }

    public static IReadOnlyList<ErpBankImportRow> ParseCsv(string? csvText)
    {
        var text = (csvText ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        if (text.Length == 0)
        {
            return [];
        }

        var rows = new List<ErpBankImportRow>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i == 0 && lines[i].Contains("date", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var columns = SplitCsvLine(line);
            if (columns.Count < 2)
            {
                continue;
            }

            var lineDate = ParseDate(columns[0]);
            var description = columns.Count > 1 ? columns[1].Trim() : string.Empty;
            var reference = columns.Count > 2 ? columns[2].Trim() : string.Empty;
            var amount = columns.Count > 3
                ? ParseAmount(columns[3])
                : 0m;
            if (amount == 0m && columns.Count > 2 && TryParseAmount(columns[2], out var fallbackAmount))
            {
                amount = fallbackAmount;
                reference = string.Empty;
            }

            rows.Add(new(
                lineDate,
                description,
                reference,
                Math.Abs(amount),
                amount >= 0m ? 1 : 0));
        }

        return rows;
    }

    private static long ParseDate(string value)
        => DateTimeOffset.TryParse(
            value.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var parsed)
            ? parsed.ToUnixTimeSeconds()
            : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static decimal ParseAmount(string value)
        => TryParseAmount(value, out var amount) ? amount : 0m;

    private static bool TryParseAmount(string value, out decimal amount)
        => decimal.TryParse(
            value.Trim().Replace(",", string.Empty, StringComparison.Ordinal),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out amount);

    private static List<string> SplitCsvLine(string line)
    {
        var columns = new List<string>();
        var value = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (ch == ',' && !quoted)
            {
                columns.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(ch);
            }
        }

        columns.Add(value.ToString());
        return columns;
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("""
                SELECT COUNT(*)
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?
                """),
            cancellationToken,
            table).ConfigureAwait(false);
        return count > 0;
    }
}

public sealed record ErpBankImportRow(
    long LineDate,
    string Description,
    string Reference,
    decimal Amount,
    int Direction);
