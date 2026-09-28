using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpDeliveryNoteWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpDeliveryNoteWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpDeliveryNoteWriteRequest(
    long OrderId = 0,
    string? Carrier = null,
    string? TrackingNumber = null,
    bool MarkShipped = false,
    string? Notes = null,
    int AdminId = 0);

public sealed class ErpDeliveryNoteWriteService : IErpDeliveryNoteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpDeliveryNoteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpDeliveryNoteWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OrderId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Order ID is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var next = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(MAX(`id`),0)+1 FROM `epc_erp_delivery_notes`"),
            cancellationToken).ConfigureAwait(false);
        var noteNo = "DN-" + next.ToString(CultureInfo.InvariantCulture);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var shippedAt = request.MarkShipped ? now : 0;
        var status = request.MarkShipped ? "shipped" : "draft";

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_erp_delivery_notes`
                    (`note_no`,`order_id`,`carrier`,`tracking_no`,`shipped_at`,`status`,`notes`,
                     `admin_id`,`time_created`)
                VALUES (?,?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            noteNo,
            request.OrderId,
            (request.Carrier ?? string.Empty).Trim(),
            (request.TrackingNumber ?? string.Empty).Trim(),
            shippedAt,
            status,
            (request.Notes ?? string.Empty).Trim(),
            request.AdminId,
            now).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var pdfPath = "/content/files/epc_erp_delivery_notes/dn_"
            + noteNo.Replace("-", "_", StringComparison.Ordinal)
            + ".html";
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_delivery_notes` SET `pdf_path`=? WHERE `id`=?"),
            cancellationToken,
            pdfPath,
            id).ConfigureAwait(false);

        return ErpSimpleWriteResult.Ok("Delivery note " + noteNo + " created.", id);
    }
}
