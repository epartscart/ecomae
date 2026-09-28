using System.Data.Common;
using System.Net;
using System.Text;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Erp;

public interface IErpDeliveryNoteDocumentService
{
    Task<string?> RenderAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpDeliveryNoteDocumentService : IErpDeliveryNoteDocumentService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpDeliveryNoteDocumentService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<string?> RenderAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.`note_no`, d.`order_id`, d.`carrier`, d.`tracking_no`, d.`status`,
                   d.`notes`, d.`time_created`, u.`email`
            FROM `epc_erp_delivery_notes` d
            LEFT JOIN `shop_orders` o ON o.`id` = d.`order_id`
            LEFT JOIN `users` u ON u.`user_id` = o.`user_id`
            WHERE d.`id` = @id
            LIMIT 1
            """;
        ErpDb.AddParameters(command, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var noteNo = ReadString(reader, 0);
        var orderId = ReadLong(reader, 1);
        var carrier = ReadString(reader, 2);
        var trackingNo = ReadString(reader, 3);
        var status = ReadString(reader, 4);
        var notes = ReadString(reader, 5);
        var timeCreated = ReadLong(reader, 6);
        var customerEmail = ReadString(reader, 7);

        return BuildHtml(noteNo, orderId, carrier, trackingNo, status, notes, timeCreated, customerEmail);
    }

    private static string BuildHtml(
        string noteNo,
        long orderId,
        string carrier,
        string trackingNo,
        string status,
        string notes,
        long timeCreated,
        string customerEmail)
    {
        var builder = new StringBuilder("""
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <title>Delivery note</title>
            <style>body{font-family:Arial,sans-serif;margin:40px;}h1{font-size:20px;}
            table{border-collapse:collapse;width:100%;margin-top:20px;}td,th{border:1px solid #ccc;padding:8px;}
            </style></head><body>
            """);
        builder.Append("<h1>Delivery note ").Append(Encode(noteNo)).Append("</h1>");
        builder.Append("<p><strong>Order:</strong> #").Append(orderId).Append("<br>");
        builder.Append("<strong>Customer:</strong> ").Append(Encode(string.IsNullOrWhiteSpace(customerEmail) ? "—" : customerEmail)).Append("<br>");
        builder.Append("<strong>Carrier:</strong> ").Append(Encode(string.IsNullOrWhiteSpace(carrier) ? "—" : carrier)).Append("<br>");
        builder.Append("<strong>Tracking:</strong> ").Append(Encode(string.IsNullOrWhiteSpace(trackingNo) ? "—" : trackingNo)).Append("<br>");
        builder.Append("<strong>Status:</strong> ").Append(Encode(status)).Append("</p>");
        if (!string.IsNullOrWhiteSpace(notes))
        {
            builder.Append("<p>").Append(Encode(notes).Replace("\n", "<br>", StringComparison.Ordinal)).Append("</p>");
        }

        var generated = timeCreated > 0
            ? DateTimeOffset.FromUnixTimeSeconds(timeCreated).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "—";
        builder.Append("<p style=\"margin-top:40px;font-size:12px;color:#666;\">Generated ")
            .Append(Encode(generated))
            .Append(" — ECOM AE ERP</p></body></html>");
        return builder.ToString();
    }

    private static string ReadString(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;

    private static long ReadLong(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal));

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
