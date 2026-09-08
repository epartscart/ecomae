using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_mfg_bom_save</c> / ajax <c>mfg_bom_save</c> twin. INSERT/UPDATE
/// <c>epc_mfg_bom</c> and replace <c>epc_mfg_bom_lines</c>. Does not CREATE tables.
/// Work-order create/issue/complete and schema ensure stay PHP.
/// </summary>
public interface IErpMfgBomSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpMfgBomSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpMfgBomLine(long ComponentItemId, decimal QtyPer, decimal ScrapPercent);

public sealed record ErpMfgBomSaveWriteRequest(
    long Id = 0,
    long ProductItemId = 0,
    string? Name = null,
    decimal OutputQty = 1,
    decimal LabourCost = 0,
    decimal OverheadCost = 0,
    IReadOnlyList<ErpMfgBomLine>? Lines = null);

public sealed class ErpMfgBomSaveWriteService : IErpMfgBomSaveWriteService
{
    private static readonly Regex FormLineKey = new(
        @"^lines\[(\d+)\]\[(component_item_id|qty_per|scrap_percent)\]$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpMfgBomSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpMfgBomSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProductItemId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select a finished product");
        }

        var lines = NormalizeLines(request.Lines);
        if (lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Add at least one component");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var name = Clip((request.Name ?? string.Empty).Trim(), 160);
        var outputQty = decimal.Round(request.OutputQty, 4, MidpointRounding.AwayFromZero);
        var labour = decimal.Round(request.LabourCost, 2, MidpointRounding.AwayFromZero);
        var overhead = decimal.Round(request.OverheadCost, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_mfg_bom", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_mfg_bom", "product_item_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_mfg_bom_lines", "component_item_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_mfg_bom_lines", "qty_per", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Manufacturing BOM tables are not provisioned");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var id = request.Id;
            if (id > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "UPDATE `epc_mfg_bom` SET `product_item_id`=?, `name`=?, `output_qty`=?, `labour_cost`=?, `overhead_cost`=? WHERE `id`=?"),
                    cancellationToken,
                    request.ProductItemId, name, outputQty, labour, overhead, id).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `epc_mfg_bom_lines` WHERE `bom_id`=?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_mfg_bom` (`product_item_id`,`name`,`output_qty`,`labour_cost`,`overhead_cost`,`active`,`time_created`) VALUES (?,?,?,?,?,1,?)"),
                    cancellationToken,
                    request.ProductItemId, name, outputQty, labour, overhead, now).ConfigureAwait(false);
                id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            }

            foreach (var line in lines)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_mfg_bom_lines` (`bom_id`,`component_item_id`,`qty_per`,`scrap_percent`) VALUES (?,?,?,?)"),
                    cancellationToken,
                    id, line.ComponentItemId, line.QtyPer, line.ScrapPercent).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(FormatSavedMessage(id, lines.Count), id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static string FormatSavedMessage(long id, int lineCount)
        => "BOM saved (#" + id.ToString(CultureInfo.InvariantCulture) + ") with "
           + lineCount.ToString(CultureInfo.InvariantCulture) + " component(s)";

    public static IReadOnlyList<ErpMfgBomLine> NormalizeLines(IEnumerable<ErpMfgBomLine>? raw)
    {
        var kept = new List<ErpMfgBomLine>();
        if (raw is null)
        {
            return kept;
        }

        foreach (var line in raw)
        {
            if (line.ComponentItemId <= 0)
            {
                continue;
            }

            kept.Add(new ErpMfgBomLine(
                line.ComponentItemId,
                decimal.Round(line.QtyPer, 4, MidpointRounding.AwayFromZero),
                decimal.Round(line.ScrapPercent, 3, MidpointRounding.AwayFromZero)));
        }

        return kept;
    }

    public static IReadOnlyList<ErpMfgBomLine> ParseLines(
        IReadOnlyList<ErpMfgBomLine>? jsonLines,
        string? linesJson,
        IFormCollection? form)
    {
        if (jsonLines is { Count: > 0 })
        {
            var fromBody = NormalizeLines(jsonLines);
            if (fromBody.Count > 0)
            {
                return fromBody;
            }
        }

        if (!string.IsNullOrWhiteSpace(linesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(linesJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var parsed = new List<ErpMfgBomLine>();
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        parsed.Add(new ErpMfgBomLine(
                            ReadLong(item, "componentItemId", "component_item_id"),
                            ReadDecimal(item, "qtyPer", "qty_per"),
                            ReadDecimal(item, "scrapPercent", "scrap_percent")));
                    }

                    var fromJson = NormalizeLines(parsed);
                    if (fromJson.Count > 0)
                    {
                        return fromJson;
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to form fields.
            }
        }

        if (form is not null)
        {
            var byIndex = new SortedDictionary<int, (long Component, decimal? Qty, decimal? Scrap)>();
            foreach (var key in form.Keys)
            {
                var match = FormLineKey.Match(key);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                {
                    continue;
                }

                byIndex.TryGetValue(idx, out var row);
                var field = match.Groups[2].Value;
                if (string.Equals(field, "component_item_id", StringComparison.OrdinalIgnoreCase))
                {
                    row.Component = LiveWriteFormBinder.Long(form, key);
                }
                else if (string.Equals(field, "qty_per", StringComparison.OrdinalIgnoreCase))
                {
                    row.Qty = LiveWriteFormBinder.Dec(form, key);
                }
                else
                {
                    row.Scrap = LiveWriteFormBinder.Dec(form, key);
                }

                byIndex[idx] = row;
            }

            if (byIndex.Count > 0)
            {
                return NormalizeLines(byIndex.Values.Select(row =>
                    new ErpMfgBomLine(row.Component, row.Qty ?? 0, row.Scrap ?? 0)));
            }

            var component = LiveWriteFormBinder.Long(form, "componentItemId", "component_item_id");
            var qty = LiveWriteFormBinder.Dec(form, "qtyPer", "qty_per");
            var scrap = LiveWriteFormBinder.Dec(form, "scrapPercent", "scrap_percent");
            return NormalizeLines([new ErpMfgBomLine(component, qty, scrap)]);
        }

        return [];
    }

    private static long ReadLong(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.Number
                && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (item.TryGetProperty(name, out prop)
                && prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static decimal ReadDecimal(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.Number
                && prop.TryGetDecimal(out var n))
            {
                return n;
            }

            if (item.TryGetProperty(name, out prop)
                && prop.ValueKind == JsonValueKind.String
                && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
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
