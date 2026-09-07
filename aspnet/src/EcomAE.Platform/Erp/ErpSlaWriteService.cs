using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_sla_create</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpSlaWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpSlaCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpSlaCreateRequest(
    int CompanyId = 0,
    string? SlaCode = null,
    string? ClientName = null,
    int ClientId = 0,
    string? ServiceType = null,
    decimal ResponseHours = 4,
    decimal ResolutionHours = 24,
    decimal UptimePct = 99.5m,
    string? PenaltyType = null,
    decimal PenaltyAmount = 0,
    string? StartDate = null,
    string? EndDate = null,
    string? Status = null,
    string? Notes = null);

public sealed class ErpSlaWriteService : IErpSlaWriteService
{
    private static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active", "expiring", "expired", "suspended"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpSlaWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpSlaCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = Clip((request.SlaCode ?? string.Empty).Trim(), 32);
        var client = Clip((request.ClientName ?? string.Empty).Trim(), 200);
        if (code.Length == 0 && client.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "SLA code or client name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_sla_agreements", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_sla_agreements", "sla_code", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "SLA tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        if (code.Length == 0)
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_sla_agreements` WHERE `company_id` = ?"),
                cancellationToken,
                companyId).ConfigureAwait(false);
            code = "SLA-" + DateTime.Now.Year.ToString(CultureInfo.InvariantCulture) + "-"
                   + (count + 1).ToString("D4", CultureInfo.InvariantCulture);
        }

        var status = NormalizeStatus(request.Status);
        var start = FormatDateOrNull(request.StartDate) ?? DateTime.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_sla_agreements` (`company_id`,`sla_code`,`client_name`,`client_id`,`service_type`,`response_hours`,`resolution_hours`,`uptime_pct`,`penalty_type`,`penalty_amount`,`start_date`,`end_date`,`status`,`notes`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            companyId,
            code,
            client,
            request.ClientId < 0 ? 0 : request.ClientId,
            Clip((request.ServiceType ?? string.Empty).Trim(), 100),
            RoundNonNeg(request.ResponseHours <= 0 ? 4 : request.ResponseHours, 2),
            RoundNonNeg(request.ResolutionHours <= 0 ? 24 : request.ResolutionHours, 2),
            NormalizeUptime(request.UptimePct),
            Clip((request.PenaltyType ?? string.Empty).Trim(), 50) is { Length: > 0 } penalty ? penalty : "credit_note",
            RoundNonNeg(request.PenaltyAmount, 2),
            start,
            FormatDateOrNull(request.EndDate),
            status,
            request.Notes ?? string.Empty,
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("SLA " + code + " created", id);
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

    private static string NormalizeStatus(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return Statuses.Contains(value) ? value : "active";
    }

    private static decimal NormalizeUptime(decimal value)
    {
        if (value <= 0)
        {
            return 99.50m;
        }

        return decimal.Round(value > 100 ? 100 : value, 2, MidpointRounding.AwayFromZero);
    }

    private static string? FormatDateOrNull(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            return parsed.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static int UnixNow()
        => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
