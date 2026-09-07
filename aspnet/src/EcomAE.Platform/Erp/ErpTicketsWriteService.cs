using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_tickets_create</c> twin. Schema-ensure and reply attachments stay PHP.
/// </summary>
public interface IErpTicketsWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpTicketsCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpTicketsCreateRequest(
    int CompanyId = 0,
    string? Subject = null,
    string? Description = null,
    string? Category = null,
    string? Priority = null,
    int ClientId = 0,
    string? ClientName = null,
    int AssignedTo = 0,
    string? AssignedName = null,
    int SlaId = 0,
    string? ResponseDeadline = null,
    string? ResolutionDeadline = null);

public sealed class ErpTicketsWriteService : IErpTicketsWriteService
{
    private static readonly HashSet<string> Priorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "low", "medium", "high", "critical"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpTicketsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpTicketsCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var subject = Clip((request.Subject ?? string.Empty).Trim(), 300);
        if (subject.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Subject is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_tickets", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_tickets", "ticket_no", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Ticket tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_tickets` WHERE `company_id` = ?"),
            cancellationToken,
            companyId).ConfigureAwait(false);
        var ticketNo = "TKT-" + DateTime.Now.Year.ToString(CultureInfo.InvariantCulture) + "-"
                       + (count + 1).ToString("D4", CultureInfo.InvariantCulture);

        var category = Clip((request.Category ?? string.Empty).Trim(), 100);
        if (category.Length == 0)
        {
            category = "general";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_tickets` (`company_id`,`ticket_no`,`subject`,`description`,`category`,`priority`,`status`,`client_id`,`client_name`,`assigned_to`,`assigned_name`,`sla_id`,`response_deadline`,`resolution_deadline`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, 'open', ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            companyId,
            ticketNo,
            subject,
            request.Description ?? string.Empty,
            category,
            NormalizePriority(request.Priority),
            request.ClientId < 0 ? 0 : request.ClientId,
            Clip((request.ClientName ?? string.Empty).Trim(), 200),
            request.AssignedTo < 0 ? 0 : request.AssignedTo,
            Clip((request.AssignedName ?? string.Empty).Trim(), 120),
            request.SlaId < 0 ? 0 : request.SlaId,
            FormatDateTimeOrNull(request.ResponseDeadline),
            FormatDateTimeOrNull(request.ResolutionDeadline),
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Ticket " + ticketNo + " created", id);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }

    private static string NormalizePriority(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return Priorities.Contains(value) ? value : "medium";
    }

    private static object? FormatDateTimeOrNull(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            return parsed.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static int UnixNow()
        => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
