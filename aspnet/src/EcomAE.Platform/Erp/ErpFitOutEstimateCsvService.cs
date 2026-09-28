using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutEstimateCsvService
{
    Task<string> ExportAsync(long estimateId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ImportAsync(
        long estimateId,
        string csv,
        CancellationToken cancellationToken = default);
}

public sealed class ErpFitOutEstimateCsvService : IErpFitOutEstimateCsvService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutEstimateCsvService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<string> ExportAsync(
        long estimateId,
        CancellationToken cancellationToken = default)
    {
        if (estimateId <= 0 || !_connections.IsConfigured)
        {
            return string.Empty;
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        var builder = new StringBuilder(
            "section,description,cost_type,quantity,unit,unit_rate,sort_order\n");
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `section`,`description`,`cost_type`,`quantity`,`unit`,`unit_rate`,`sort_order`
            FROM `ecomae_fitout_boq_lines`
            WHERE `estimate_id`=?
            ORDER BY `sort_order`,`id`
            """);
        ErpDb.AddParameters(command, estimateId);
        await using var reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            builder.AppendJoin(',', new[]
            {
                Csv(reader.GetString(0)),
                Csv(reader.GetString(1)),
                Csv(reader.GetString(2)),
                reader.GetDecimal(3).ToString("0.####", CultureInfo.InvariantCulture),
                Csv(reader.GetString(4)),
                reader.GetDecimal(5).ToString("0.####", CultureInfo.InvariantCulture),
                reader.GetInt32(6).ToString(CultureInfo.InvariantCulture)
            });
            builder.Append('\n');
        }

        return builder.ToString();
    }

    public async Task<ErpSimpleWriteResult> ImportAsync(
        long estimateId,
        string csv,
        CancellationToken cancellationToken = default)
    {
        if (estimateId <= 0 || string.IsNullOrWhiteSpace(csv))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Estimate and CSV content are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var lines = csv
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
        if (lines.Length < 2)
        {
            return ErpSimpleWriteResult.Fail("invalid", "CSV must include a header and at least one BOQ line.");
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        var inserted = 0;
        foreach (var line in lines.Skip(1))
        {
            var fields = Parse(line);
            if (fields.Count != 7
                || !decimal.TryParse(fields[3], NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity)
                || !decimal.TryParse(fields[5], NumberStyles.Number, CultureInfo.InvariantCulture, out var unitRate)
                || !int.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sortOrder)
                || fields[1].Length == 0
                || fields[4].Length == 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", $"Invalid BOQ CSV row: {line}");
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    INSERT INTO `ecomae_fitout_boq_lines`
                        (`estimate_id`,`section`,`description`,`cost_type`,`quantity`,`unit`,`unit_rate`,`sort_order`)
                    VALUES (?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                estimateId,
                fields[0],
                fields[1],
                fields[2],
                Math.Max(0m, quantity),
                fields[4],
                Math.Max(0m, unitRate),
                Math.Max(0, sortOrder)).ConfigureAwait(false);
            inserted++;
        }

        return ErpSimpleWriteResult.Ok($"Imported {inserted} BOQ lines", inserted);
    }

    private static string Csv(string value)
        => value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;

    private static List<string> Parse(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(value.ToString().Trim());
                value.Clear();
            }
            else
            {
                value.Append(character);
            }
        }

        values.Add(value.ToString().Trim());
        return values;
    }
}
