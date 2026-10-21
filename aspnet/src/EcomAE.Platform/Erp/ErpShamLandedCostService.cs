using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM blueprint §9: landed-cost allocation. A purchase receipt carries
/// header charges (freight, duty, insurance, other); this engine spreads
/// them across the receipt's lines by value, quantity, or weight and
/// stores the allocation so each line's true landed unit cost is
/// reproducible. Header + lines live in
/// <c>epc_erp_org_landed_costs</c>/<c>epc_erp_org_landed_cost_lines</c>
/// (tenant-generic). Grant: <c>landed-cost</c>.
/// </summary>
public interface IErpShamLandedCostService
{
    Task<ErpShamLandedCostResult> AllocateAsync(string entityCode, string? buCode, string receiptRef, string method, decimal freight, decimal duty, decimal insurance, decimal other, IReadOnlyList<ErpShamLandedCostLineInput> lines, string userEmail, CancellationToken cancellationToken = default);
    Task<ErpShamLandedCostResult> ViewAsync(string entityCode, string? buCode, string receiptRef, string userEmail, CancellationToken cancellationToken = default);
}

public sealed record ErpShamLandedCostLineInput(string ItemCode, decimal Qty, decimal Weight, decimal BaseAmount);
public sealed record ErpShamLandedCostLineOut(string ItemCode, decimal Qty, decimal Weight, decimal BaseAmount, decimal Allocated, decimal LandedUnitCost);
public sealed record ErpShamLandedCostResult(bool Ok, string Message, string Entity, string Bu, string ReceiptRef, string Method, decimal TotalCharges, IReadOnlyList<ErpShamLandedCostLineOut>? Lines);

public sealed class ErpShamLandedCostService : IErpShamLandedCostService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;

    public ErpShamLandedCostService(IErpWriteConnectionFactory connections, IErpShamFoundationService foundation)
    {
        _connections = connections;
        _foundation = foundation;
    }

    public static async Task EnsureSchemaAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_landed_costs` (`id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `entity_id` int(11) NOT NULL DEFAULT 0, `bu_code` varchar(40) NOT NULL DEFAULT '',"
            + " `receipt_ref` varchar(190) NOT NULL DEFAULT '', `method` varchar(20) NOT NULL DEFAULT 'value',"
            + " `freight` decimal(14,2) NOT NULL DEFAULT 0, `duty` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `insurance` decimal(14,2) NOT NULL DEFAULT 0, `other` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `set_by` varchar(190) NOT NULL DEFAULT '', `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`), UNIQUE KEY `ux_entity_bu_receipt` (`entity_id`,`bu_code`,`receipt_ref`))"
            + " ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Landed-cost allocation headers (§9)'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_landed_cost_lines` (`id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `landed_cost_id` int(11) NOT NULL DEFAULT 0, `item_code` varchar(80) NOT NULL DEFAULT '',"
            + " `qty` decimal(14,3) NOT NULL DEFAULT 0, `weight` decimal(14,3) NOT NULL DEFAULT 0,"
            + " `base_amount` decimal(14,2) NOT NULL DEFAULT 0, `allocated` decimal(14,2) NOT NULL DEFAULT 0,"
            + " `landed_unit_cost` decimal(14,4) NOT NULL DEFAULT 0, PRIMARY KEY (`id`),"
            + " KEY `x_lc` (`landed_cost_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8"
            + " COMMENT='Landed-cost allocation per receipt line (§9)'",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpShamLandedCostResult> AllocateAsync(string entityCode, string? buCode, string receiptRef, string method, decimal freight, decimal duty, decimal insurance, decimal other, IReadOnlyList<ErpShamLandedCostLineInput> lines, string userEmail, CancellationToken cancellationToken = default)
    {
        method = (method ?? string.Empty).Trim().ToLowerInvariant();
        if (method is not ("value" or "qty" or "weight"))
        {
            return Fail(entityCode, buCode, receiptRef, method, "method must be 'value', 'qty', or 'weight'.");
        }
        if (lines.Count == 0)
        {
            return Fail(entityCode, buCode, receiptRef, method, "At least one receipt line is required.");
        }
        var totalCharges = freight + duty + insurance + other;
        if (totalCharges < 0 || freight < 0 || duty < 0 || insurance < 0 || other < 0)
        {
            return Fail(entityCode, buCode, receiptRef, method, "Charges cannot be negative.");
        }
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, buCode, "landed-cost", cancellationToken).ConfigureAwait(false))
        {
            return Fail(entityCode, buCode, receiptRef, method, "landed-cost grant required for this entity×BU scope.");
        }
        var entityId = await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false);
        if (entityId <= 0)
        {
            return Fail(entityCode, buCode, receiptRef, method, "Unknown entity code.");
        }
        var bu = (buCode ?? string.Empty).Trim();
        var refx = (receiptRef ?? string.Empty).Trim();
        if (refx.Length == 0)
        {
            return Fail(entityCode, buCode, receiptRef, method, "receiptRef is required.");
        }

        // Allocation base per line.
        decimal Base(ErpShamLandedCostLineInput l) => method switch
        {
            "qty" => l.Qty,
            "weight" => l.Weight,
            _ => l.BaseAmount,
        };
        var totalBase = lines.Sum(Base);
        if (totalBase <= 0)
        {
            return Fail(entityCode, buCode, receiptRef, method, $"Allocation base ({method}) totals zero.");
        }

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        // Idempotent: re-running for the same receipt replaces the previous allocation.
        var existingId = await ErpDb.LongAsync(conn, tx,
            "SELECT `id` FROM `epc_erp_org_landed_costs` WHERE `entity_id`=? AND `bu_code`=? AND `receipt_ref`=? LIMIT 1",
            cancellationToken, entityId, bu, refx).ConfigureAwait(false);
        if (existingId > 0)
        {
            await ErpDb.ExecuteAsync(conn, tx, "DELETE FROM `epc_erp_org_landed_cost_lines` WHERE `landed_cost_id`=?", cancellationToken, existingId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(conn, tx, "DELETE FROM `epc_erp_org_landed_costs` WHERE `id`=?", cancellationToken, existingId).ConfigureAwait(false);
        }
        await ErpDb.ExecuteAsync(conn, tx,
            "INSERT INTO `epc_erp_org_landed_costs` (`entity_id`,`bu_code`,`receipt_ref`,`method`,`freight`,`duty`,`insurance`,`other`,`set_by`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?)",
            cancellationToken, entityId, bu, refx, method, freight, duty, insurance, other, userEmail, now).ConfigureAwait(false);
        var headerId = await ErpDb.LastInsertIdAsync(conn, tx, cancellationToken).ConfigureAwait(false);

        var allocated = new List<ErpShamLandedCostLineOut>(lines.Count);
        decimal distributed = 0m;
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            // Largest remainder on the last line absorbs rounding.
            var share = i == lines.Count - 1
                ? totalCharges - distributed
                : Math.Round(totalCharges * Base(l) / totalBase, 2, MidpointRounding.AwayFromZero);
            distributed += share;
            var unitCost = l.Qty > 0 ? (l.BaseAmount + share) / l.Qty : 0m;
            await ErpDb.ExecuteAsync(conn, tx,
                "INSERT INTO `epc_erp_org_landed_cost_lines` (`landed_cost_id`,`item_code`,`qty`,`weight`,`base_amount`,`allocated`,`landed_unit_cost`) VALUES (?,?,?,?,?,?,?)",
                cancellationToken, headerId, l.ItemCode, l.Qty, l.Weight, l.BaseAmount, share, Math.Round(unitCost, 4, MidpointRounding.AwayFromZero)).ConfigureAwait(false);
            allocated.Add(new ErpShamLandedCostLineOut(l.ItemCode, l.Qty, l.Weight, l.BaseAmount, share, Math.Round(unitCost, 4, MidpointRounding.AwayFromZero)));
        }
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ErpShamLandedCostResult(true, $"Allocated {totalCharges:N2} across {lines.Count} line(s) by {method}.", entityCode, bu, refx, method, totalCharges, allocated);
    }

    public async Task<ErpShamLandedCostResult> ViewAsync(string entityCode, string? buCode, string receiptRef, string userEmail, CancellationToken cancellationToken = default)
    {
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, buCode, "landed-cost", cancellationToken).ConfigureAwait(false))
        {
            return Fail(entityCode, buCode, receiptRef, "", "landed-cost grant required for this entity×BU scope.");
        }
        var entityId = await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false);
        if (entityId <= 0)
        {
            return Fail(entityCode, buCode, receiptRef, "", "Unknown entity code.");
        }
        var bu = (buCode ?? string.Empty).Trim();
        var refx = (receiptRef ?? string.Empty).Trim();
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);

        long headerId = 0; string method = "value"; decimal totalCharges = 0m;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional("SELECT `id`,`method`,`freight`+`duty`+`insurance`+`other` FROM `epc_erp_org_landed_costs` WHERE `entity_id`=? AND `bu_code`=? AND `receipt_ref`=? LIMIT 1");
            ErpDb.AddParameters(cmd, entityId, bu, refx);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                headerId = reader.GetInt64(0); method = reader.GetString(1); totalCharges = reader.GetDecimal(2);
            }
        }
        if (headerId == 0)
        {
            return Fail(entityCode, buCode, receiptRef, "", "No landed-cost allocation for this receipt.");
        }
        var lines = new List<ErpShamLandedCostLineOut>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional("SELECT `item_code`,`qty`,`weight`,`base_amount`,`allocated`,`landed_unit_cost` FROM `epc_erp_org_landed_cost_lines` WHERE `landed_cost_id`=? ORDER BY `id`");
            ErpDb.AddParameters(cmd, headerId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new ErpShamLandedCostLineOut(reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetDecimal(5)));
            }
        }
        return new ErpShamLandedCostResult(true, $"Allocation ({method}) for {refx}.", entityCode, bu, refx, method, totalCharges, lines);
    }

    public static IReadOnlyList<ErpShamLandedCostLineInput> ParseLinesJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<ErpShamLandedCostLineInput>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<ErpShamLandedCostLineInput>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var item = el.TryGetProperty("itemCode", out var ic) ? ic.GetString() ?? "" : el.TryGetProperty("item", out var it) ? it.GetString() ?? "" : "";
                var qty = el.TryGetProperty("qty", out var q) ? q.GetDecimal() : 0m;
                var wt = el.TryGetProperty("weight", out var w) ? w.GetDecimal() : 0m;
                var amt = el.TryGetProperty("baseAmount", out var b) ? b.GetDecimal() : el.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0m;
                if (item.Length > 0) list.Add(new ErpShamLandedCostLineInput(item, qty, wt, amt));
            }
            return list;
        }
        catch (JsonException)
        {
            return Array.Empty<ErpShamLandedCostLineInput>();
        }
    }

    private ErpShamLandedCostResult Fail(string entityCode, string? buCode, string? receiptRef, string method, string message)
        => new(false, message, entityCode, buCode ?? "", receiptRef ?? "", method, 0m, null);

    private async Task<int> EntityIdAsync(string entityCode, CancellationToken cancellationToken)
    {
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return (int)await ErpDb.LongAsync(conn, null,
            "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1",
            cancellationToken, entityCode.Trim()).ConfigureAwait(false);
    }
}
