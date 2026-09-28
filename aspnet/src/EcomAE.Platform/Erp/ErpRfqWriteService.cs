using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpRfqWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(ErpRfqWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed record ErpRfqWriteRequest(
    long Id = 0,
    long SupplierId = 0,
    string? Title = null,
    string? Description = null,
    decimal AmountEstimate = 0,
    string? Status = null,
    string? DueDate = null,
    long OrderId = 0,
    int AdminId = 0);

public sealed class ErpRfqWriteService : IErpRfqWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpRfqWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpRfqWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "RFQ title is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var status = request.Status is "draft" or "sent" or "quoted" or "accepted" or "rejected" or "cancelled"
            ? request.Status
            : "draft";
        var dueDate = 0L;
        if (DateTime.TryParseExact(
                (request.DueDate ?? string.Empty).Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            dueDate = new DateTimeOffset(parsed, TimeSpan.Zero).AddHours(23).AddMinutes(59).AddSeconds(59).ToUnixTimeSeconds();
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var amount = Math.Round(request.AmountEstimate, 2, MidpointRounding.AwayFromZero);
        if (request.Id > 0)
        {
            var updated = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    UPDATE `epc_erp_rfq`
                    SET `supplier_id`=?, `title`=?, `description`=?, `amount_est`=?, `status`=?,
                        `due_date`=?, `order_id`=?, `time_updated`=?
                    WHERE `id`=?
                    """),
                cancellationToken,
                request.SupplierId,
                title,
                (request.Description ?? string.Empty).Trim(),
                amount,
                status,
                dueDate,
                request.OrderId,
                now,
                request.Id).ConfigureAwait(false);
            return updated == 0
                ? ErpSimpleWriteResult.Fail("invalid", "RFQ not found.")
                : ErpSimpleWriteResult.Ok("RFQ saved.", request.Id);
        }

        var next = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(MAX(`id`),0)+1 FROM `epc_erp_rfq`"),
            cancellationToken).ConfigureAwait(false);
        var rfqNo = "RFQ-" + next.ToString(CultureInfo.InvariantCulture);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_erp_rfq`
                    (`rfq_no`,`supplier_id`,`title`,`description`,`amount_est`,`status`,
                     `due_date`,`order_id`,`admin_id`,`time_created`,`time_updated`)
                VALUES (?,?,?,?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            rfqNo,
            request.SupplierId,
            title,
            (request.Description ?? string.Empty).Trim(),
            amount,
            status,
            dueDate,
            request.OrderId,
            request.AdminId,
            now,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("RFQ saved.", id);
    }
}
