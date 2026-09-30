using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpRfqAwardWriteService
{
    Task<ErpRfqAwardWriteResult> AwardAsync(
        ErpRfqAwardWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRfqAwardWriteRequest(
    long RfqId = 0,
    long SupplierId = 0,
    long AdminId = 0);

public sealed record ErpRfqAwardWriteResult(
    bool Succeeded,
    string Code,
    string Message,
    long PurchaseOrderId,
    string PurchaseOrderNumber,
    decimal TotalExVat,
    int Lines,
    int Writes)
{
    public static ErpRfqAwardWriteResult Fail(string code, string message)
        => new(false, code, message, 0, string.Empty, 0m, 0, 0);
}

/// <summary>
/// Guarded PHP <c>epc_scm_rfq_award_to_po</c> handoff. The RFQ tables remain
/// Classic-owned; the existing ASP.NET PO writer owns the draft PO insert.
/// </summary>
public sealed class ErpRfqAwardWriteService : IErpRfqAwardWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpPurchaseOrderWriteService _purchaseOrders;

    public ErpRfqAwardWriteService(
        IErpWriteConnectionFactory connections,
        IErpPurchaseOrderWriteService purchaseOrders)
    {
        _connections = connections;
        _purchaseOrders = purchaseOrders;
    }

    public async Task<ErpRfqAwardWriteResult> AwardAsync(
        ErpRfqAwardWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RfqId <= 0 || request.SupplierId <= 0)
        {
            return ErpRfqAwardWriteResult.Fail("invalid", "RFQ and supplier ids are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpRfqAwardWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        List<ErpPurchaseOrderLineInput> lines;
        decimal totalExVat;
        await using (var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await TableExistsAsync(connection, "epc_scm_rfq", cancellationToken).ConfigureAwait(false)
                || !await TableExistsAsync(connection, "epc_scm_rfq_lines", cancellationToken).ConfigureAwait(false)
                || !await TableExistsAsync(connection, "epc_scm_rfq_responses", cancellationToken).ConfigureAwait(false))
            {
                return ErpRfqAwardWriteResult.Fail("schema", "PHP SCM RFQ tables are not provisioned.");
            }

            var header = await LoadHeaderAsync(connection, request.RfqId, cancellationToken).ConfigureAwait(false);
            if (header is null)
            {
                return ErpRfqAwardWriteResult.Fail("not_found", "RFQ was not found.");
            }

            if (header.Value.Status is "cancelled" or "awarded")
            {
                return ErpRfqAwardWriteResult.Fail("state", "Only an open RFQ can be awarded.");
            }

            var quoteLines = await LoadQuoteLinesAsync(
                connection,
                request.RfqId,
                request.SupplierId,
                cancellationToken).ConfigureAwait(false);
            if (quoteLines.Count == 0)
            {
                return ErpRfqAwardWriteResult.Fail("invalid", "The selected supplier has no quoted RFQ lines.");
            }

            if (quoteLines.Any(line => line.Qty <= 0m))
            {
                return ErpRfqAwardWriteResult.Fail("invalid", "RFQ quantities must be positive before award.");
            }

            lines = quoteLines
                .Select(line => new ErpPurchaseOrderLineInput(
                    line.ItemId.ToString(CultureInfo.InvariantCulture),
                    line.Description,
                    line.Qty,
                    line.UnitPrice,
                    ErpTaxAmountCalculator.Round2(line.Qty * line.UnitPrice)))
                .ToList();
            totalExVat = ErpTaxAmountCalculator.Round2(lines.Sum(line => line.LineExVat));
        }

        if (lines.Count == 0 || totalExVat < 0m)
        {
            return ErpRfqAwardWriteResult.Fail("invalid", "RFQ award has no usable purchase lines.");
        }

        ErpPurchaseOrderSaveResult purchaseOrder;
        try
        {
            purchaseOrder = await _purchaseOrders.SaveAsync(
                new ErpPurchaseOrderInput
                {
                    SupplierId = checked((int)request.SupplierId),
                    Title = "From RFQ",
                    AmountExVat = totalExVat,
                    Status = "draft",
                    Lines = lines
                },
                checked((int)request.AdminId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ErpWriteException or DbException)
        {
            return ErpRfqAwardWriteResult.Fail("po", ex.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var affected = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "UPDATE `epc_scm_rfq` SET `status`='awarded', `awarded_supplier_id`=?, `awarded_po_id`=?, `time_updated`=? WHERE `id`=? AND `status` NOT IN ('awarded','cancelled')"),
                cancellationToken,
                request.SupplierId,
                purchaseOrder.Id,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                request.RfqId).ConfigureAwait(false);
            if (affected == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpRfqAwardWriteResult.Fail(
                    "state",
                    "The RFQ changed state before the award could be recorded; the draft PO was not linked.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return ErpRfqAwardWriteResult.Fail(
                "link",
                "Draft PO " + purchaseOrder.PoNo + " was created but the Classic RFQ link was not recorded: " + ex.Message);
        }

        return new ErpRfqAwardWriteResult(
            true,
            "ok",
            "RFQ awarded and draft purchase order created.",
            purchaseOrder.Id,
            purchaseOrder.PoNo,
            purchaseOrder.AmountExVat,
            purchaseOrder.LinesAdded,
            2);
    }

    private static async Task<(string Status, long AwardedPurchaseOrderId)?> LoadHeaderAsync(
        DbConnection connection,
        long rfqId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `status`, `awarded_po_id` FROM `epc_scm_rfq` WHERE `id`=? LIMIT 1");
        ErpDb.AddParameters(command, rfqId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (
            reader.IsDBNull(0) ? "draft" : reader.GetString(0),
            reader.IsDBNull(1) ? 0L : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture));
    }

    private static async Task<List<(long ItemId, string Description, decimal Qty, decimal UnitPrice)>> LoadQuoteLinesAsync(
        DbConnection connection,
        long rfqId,
        long supplierId,
        CancellationToken cancellationToken)
    {
        var rows = new List<(long, string, decimal, decimal)>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT l.`item_id`, l.`description`, l.`qty`, r.`unit_price`
            FROM `epc_scm_rfq_lines` l
            INNER JOIN (
                SELECT `rfq_line_id`, MIN(`id`) AS response_id
                FROM `epc_scm_rfq_responses`
                WHERE `rfq_id`=? AND `supplier_id`=?
                GROUP BY `rfq_line_id`
            ) first_response ON first_response.`rfq_line_id`=l.`id`
            INNER JOIN `epc_scm_rfq_responses` r ON r.`id`=first_response.`response_id`
            WHERE l.`rfq_id`=?
            ORDER BY l.`sort_order`, l.`id`
            """);
        ErpDb.AddParameters(command, rfqId, supplierId, rfqId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add((
                reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture),
                reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return count > 0;
    }
}
