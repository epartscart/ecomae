using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> <c>update_ticket_status</c> twin of <c>epc_crm_save_ticket</c> after load.
/// Schema-ensure, file attachments, and send stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmTicketWriteService
{
    Task<ErpSimpleWriteResult> UpdateStatusAsync(
        long id,
        string? status,
        string? priority,
        string? message,
        long authorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class CpCrmTicketWriteService : ICpCrmTicketWriteService
{
    public static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "open", "pending", "resolved", "closed",
    };

    public static readonly HashSet<string> Priorities = new(StringComparer.Ordinal)
    {
        "low", "normal", "high", "urgent",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpCrmTicketWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> UpdateStatusAsync(
        long id,
        string? status,
        string? priority,
        string? message,
        long authorUserId,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Ticket id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var select = connection.CreateCommand();
            select.CommandText = ErpDb.Positional(
                """
                SELECT `customer_user_id`, `order_id`, `subject`, `status`, `priority`, `assigned_user_id`
                FROM `epc_crm_tickets`
                WHERE `id`=? AND `active`=1
                LIMIT 1
                """);
            ErpDb.AddParameters(select, id);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("not_found", "Ticket not found");
            }

            var customerUserId = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            var orderId = reader.IsDBNull(1) ? 0L : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            var subject = reader.IsDBNull(2) ? "" : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "";
            var rowStatus = reader.IsDBNull(3) ? "open" : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? "open";
            var rowPriority = reader.IsDBNull(4) ? "normal" : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? "normal";
            var assigned = reader.IsDBNull(5) ? 0L : Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture);
            await reader.DisposeAsync().ConfigureAwait(false);

            var nextStatus = (status ?? string.Empty).Trim();
            if (nextStatus.Length == 0 || !Statuses.Contains(nextStatus))
            {
                nextStatus = Statuses.Contains(rowStatus) ? rowStatus : "open";
            }

            var nextPriority = (priority ?? string.Empty).Trim();
            if (nextPriority.Length == 0 || !Priorities.Contains(nextPriority))
            {
                nextPriority = Priorities.Contains(rowPriority) ? rowPriority : "normal";
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_crm_tickets`
                    SET `customer_user_id`=?, `order_id`=?, `subject`=?, `status`=?, `priority`=?, `assigned_user_id`=?, `time_updated`=?
                    WHERE `id`=?
                    """),
                cancellationToken,
                customerUserId, orderId, subject, nextStatus, nextPriority, assigned, now, id);

            var note = (message ?? string.Empty).Trim();
            if (note.Length > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        INSERT INTO `epc_crm_ticket_messages`
                        (`ticket_id`, `author_user_id`, `is_staff`, `body`, `time_created`)
                        VALUES (?, ?, 1, ?, ?)
                        """),
                    cancellationToken,
                    id, authorUserId > 0 ? authorUserId : 0, note, now);
            }

            return ErpSimpleWriteResult.Ok("Ticket updated", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM ticket table is missing — schema-ensure stays Classic.");
        }
    }
}
