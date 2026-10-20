using System.Globalization;
using System.Text;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Erp;

public sealed record ErpPimProductRow(
    string Sku,
    string Name,
    string ItemType,
    string ProductType,
    string ItemGroup,
    string Unit,
    string PurchaseUnit,
    string SalesUnit,
    decimal SalesPrice,
    string CostingMethod,
    string Barcode,
    decimal ReorderLevel,
    bool TrackExpiry,
    bool Active,
    int Provided);

public sealed record ErpPimProductUploadResult(
    bool Ok,
    int Parsed,
    int Inserted,
    int Updated,
    IReadOnlyList<string> Errors,
    string Message);

/// <summary>
/// CSV product upload for the PIM workspace ("Item details CSV view" / bulk import).
/// Header row optional; columns: sku, name, item_type, product_type, item_group, unit,
/// purchase_unit, sales_unit, sales_price, costing_method, barcode, reorder_level,
/// track_expiry, active. Writes go to <c>epc_erp_inv_items</c> keyed on sku.
/// </summary>
public interface IErpPimProductUploadService
{
    Task<ErpPimProductUploadResult> UploadAsync(string csv, int adminId, CancellationToken cancellationToken = default);
    ErpPimProductUploadResult DryRun(string csv);
}

public sealed class ErpPimProductUploadService : IErpPimProductUploadService
{
    private const int MaxRows = 5000;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpPimProductUploadService(IErpWriteConnectionFactory connections, IErpAuditLogWriter audit)
    {
        _connections = connections;
        _audit = audit;
    }

    public ErpPimProductUploadResult DryRun(string csv)
    {
        var (rows, errors) = Parse(csv);
        if (errors.Count > 0)
        {
            return new ErpPimProductUploadResult(false, rows.Count, 0, 0, errors, string.Join(" ", errors.Take(3)));
        }

        if (rows.Count == 0)
        {
            return new ErpPimProductUploadResult(false, 0, 0, 0, ["No product rows found."], "No product rows found.");
        }

        return new ErpPimProductUploadResult(true, rows.Count, 0, 0, [],
            rows.Count.ToString(CultureInfo.InvariantCulture) + " products would be imported (dry run).");
    }

    public async Task<ErpPimProductUploadResult> UploadAsync(string csv, int adminId, CancellationToken cancellationToken = default)
    {
        var (rows, errors) = Parse(csv);
        if (errors.Count > 0)
        {
            return new ErpPimProductUploadResult(false, rows.Count, 0, 0, errors, string.Join(" ", errors.Take(3)));
        }

        if (rows.Count == 0)
        {
            return new ErpPimProductUploadResult(false, 0, 0, 0, ["No product rows found."], "No product rows found.");
        }

        if (!_connections.IsConfigured)
        {
            return new ErpPimProductUploadResult(false, rows.Count, 0, 0, ["TenantRegistry DB is not configured."], "TenantRegistry DB is not configured.");
        }

        var inserted = 0;
        var updated = 0;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureInventoryAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var row in rows)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = tx;
                command.CommandText = ErpDb.Positional(
                    "INSERT INTO `epc_erp_inv_items` (`sku`,`name`,`item_type`,`product_type`,`item_group`,`unit`," +
                    "`purchase_unit`,`sales_unit`,`sales_price`,`costing_method`,`barcode`,`reorder_level`," +
                    "`track_expiry`,`active`,`time_created`)" +
                    " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)" +
                    " ON DUPLICATE KEY UPDATE `name`=VALUES(`name`)," +
                    "`item_type`=IF((? & 1)<>0,VALUES(`item_type`),`item_type`)," +
                    "`product_type`=IF((? & 2)<>0,VALUES(`product_type`),`product_type`)," +
                    "`item_group`=IF((? & 4)<>0,VALUES(`item_group`),`item_group`)," +
                    "`unit`=IF((? & 8)<>0,VALUES(`unit`),`unit`)," +
                    "`purchase_unit`=IF((? & 16)<>0,VALUES(`purchase_unit`),`purchase_unit`)," +
                    "`sales_unit`=IF((? & 32)<>0,VALUES(`sales_unit`),`sales_unit`)," +
                    "`sales_price`=IF((? & 64)<>0,VALUES(`sales_price`),`sales_price`)," +
                    "`costing_method`=IF((? & 128)<>0,VALUES(`costing_method`),`costing_method`)," +
                    "`barcode`=IF((? & 256)<>0,VALUES(`barcode`),`barcode`)," +
                    "`reorder_level`=IF((? & 512)<>0,VALUES(`reorder_level`),`reorder_level`)," +
                    "`track_expiry`=IF((? & 1024)<>0,VALUES(`track_expiry`),`track_expiry`)," +
                    "`active`=IF((? & 2048)<>0,VALUES(`active`),`active`)");
                ErpDb.AddParameters(command,
                    row.Sku, row.Name, row.ItemType, row.ProductType, row.ItemGroup, row.Unit,
                    row.PurchaseUnit, row.SalesUnit, row.SalesPrice, row.CostingMethod, row.Barcode,
                    row.ReorderLevel, row.TrackExpiry ? 1 : 0, row.Active ? 1 : 0, now,
                    row.Provided, row.Provided, row.Provided, row.Provided, row.Provided, row.Provided,
                    row.Provided, row.Provided, row.Provided, row.Provided, row.Provided, row.Provided);
                var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                // INSERT ... ON DUPLICATE KEY UPDATE returns 1 for insert, 2 for update.
                if (affected == 1) inserted++;
                else if (affected >= 2) updated++;
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "pim_product_upload",
            "item",
            rows.Count,
            "PIM product upload: " + inserted + " inserted, " + updated + " updated",
            null,
            cancellationToken).ConfigureAwait(false);

        return new ErpPimProductUploadResult(true, rows.Count, inserted, updated, [],
            inserted + " products imported, " + updated + " updated.");
    }

    /// <summary>Parses CSV text into product rows; tolerant of a header row and quoted fields.</summary>
    public static (IReadOnlyList<ErpPimProductRow> Rows, IReadOnlyList<string> Errors) Parse(string csv)
    {
        var rows = new List<ErpPimProductRow>();
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lineNo = 0;
        var skippedHeader = false;

        foreach (var fields in CsvLines(csv))
        {
            lineNo++;
            if (fields.Count == 0 || fields.All(f => string.IsNullOrWhiteSpace(f)))
            {
                continue;
            }

            var head = fields[0].Trim().TrimStart('﻿');
            if (!skippedHeader && head.Equals("sku", StringComparison.OrdinalIgnoreCase))
            {
                skippedHeader = true;
                continue;
            }

            skippedHeader = true;
            string Cell(int i) => i < fields.Count ? fields[i].Trim() : "";
            var sku = Cell(0);
            if (sku.Length == 0 || sku.Length > 64)
            {
                errors.Add("Row " + lineNo + ": sku is required (max 64 chars).");
                continue;
            }

            if (Cell(1).Length == 0)
            {
                errors.Add("Row " + lineNo + " (" + sku + "): name is required.");
                continue;
            }

            var provided = 0;
            for (var c = 2; c <= 13; c++)
            {
                if (Cell(c).Length > 0) provided |= 1 << (c - 2);
            }

            var itemType = Cell(2).ToLowerInvariant();
            if (itemType.Length == 0) itemType = "standard";
            if (itemType is not ("standard" or "perishable" or "serialized"))
            {
                errors.Add("Row " + lineNo + " (" + sku + "): item_type must be standard, perishable, or serialized.");
                continue;
            }

            var productType = Cell(3).ToLowerInvariant();
            if (productType.Length == 0) productType = "item";

            if (Cell(8).Length > 0 && !decimal.TryParse(Cell(8), NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                errors.Add("Row " + lineNo + " (" + sku + "): sales_price must be a number.");
                continue;
            }

            if (Cell(11).Length > 0 && !decimal.TryParse(Cell(11), NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                errors.Add("Row " + lineNo + " (" + sku + "): reorder_level must be a number.");
                continue;
            }

            if (!decimal.TryParse(Cell(8), NumberStyles.Any, CultureInfo.InvariantCulture, out var price)) price = 0;
            if (!decimal.TryParse(Cell(11), NumberStyles.Any, CultureInfo.InvariantCulture, out var reorder)) reorder = 0;
            var trackExpiry = Truthy(Cell(12));
            var active = Cell(13).Length == 0 || Truthy(Cell(13));

            if (!seen.Add(sku))
            {
                errors.Add("Row " + lineNo + " (" + sku + "): duplicate sku in file.");
                continue;
            }

            rows.Add(new ErpPimProductRow(
                sku,
                Cell(1).Length > 255 ? Cell(1)[..255] : Cell(1),
                itemType,
                productType.Length > 16 ? productType[..16] : productType,
                Cell(4).Length > 64 ? Cell(4)[..64] : Cell(4),
                Cell(5).Length == 0 ? "pcs" : Cell(5).Length > 16 ? Cell(5)[..16] : Cell(5),
                Cell(6).Length > 16 ? Cell(6)[..16] : Cell(6),
                Cell(7).Length > 16 ? Cell(7)[..16] : Cell(7),
                price,
                Cell(9).Length > 24 ? Cell(9)[..24] : Cell(9),
                Cell(10).Length > 128 ? Cell(10)[..128] : Cell(10),
                reorder,
                trackExpiry,
                active,
                provided));
        }

        if (rows.Count > MaxRows)
        {
            errors.Add("Too many rows (" + rows.Count + "); maximum " + MaxRows + " per upload.");
        }

        return (rows, errors);
    }

    private static bool Truthy(string raw)
        => raw is "1" or "true" or "yes" or "y" ||
           raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
           raw.Equals("yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>Minimal RFC-4180 CSV line splitter (handles quoted commas and quotes).</summary>
    private static IEnumerable<List<string>> CsvLines(string csv)
    {
        var fields = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var hasContent = false;
        for (var i = 0; i <= csv.Length; i++)
        {
            var ch = i < csv.Length ? csv[i] : '\n';
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    cell.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    quoted = true;
                    hasContent = true;
                    break;
                case ',':
                    fields.Add(cell.ToString());
                    cell.Clear();
                    hasContent = true;
                    break;
                case '\n' or '\r':
                    if (ch == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    fields.Add(cell.ToString());
                    cell.Clear();
                    if (hasContent || fields.Count > 1) yield return fields;
                    fields = new List<string>();
                    hasContent = false;
                    break;
                default:
                    cell.Append(ch);
                    hasContent = true;
                    break;
            }
        }
    }
}
