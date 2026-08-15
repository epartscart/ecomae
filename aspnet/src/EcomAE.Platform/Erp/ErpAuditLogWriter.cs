using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>Port of PHP <c>epc_erp_audit_log</c> — every mutating ERP action is recorded.</summary>
public interface IErpAuditLogWriter
{
    Task LogAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int adminId,
        string action,
        string entityType,
        long entityId,
        string summary,
        IReadOnlyDictionary<string, string?>? detail,
        CancellationToken cancellationToken = default);
}

public sealed class ErpAuditLogWriter : IErpAuditLogWriter
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public ErpAuditLogWriter(IHttpContextAccessor? httpContextAccessor = null)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int adminId,
        string action,
        string entityType,
        long entityId,
        string summary,
        IReadOnlyDictionary<string, string?>? detail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var request = _httpContextAccessor?.HttpContext?.Request;
        var ip = _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var userAgent = request?.Headers.UserAgent.ToString() ?? string.Empty;

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_audit_log`"
                    + " (`time`, `admin_id`, `action`, `entity_type`, `entity_id`, `summary`, `detail_json`, `old_json`, `new_json`, `ip_address`, `user_agent`)"
                    + " VALUES (?,?,?,?,?,?,?,NULL,NULL,?,?)"),
                cancellationToken,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                adminId,
                Truncate(action, 64),
                Truncate(entityType, 32),
                entityId,
                Truncate(summary, 512),
                detail is null || detail.Count == 0 ? null : JsonSerializer.Serialize(detail),
                Truncate(ip, 64),
                Truncate(userAgent, 255)).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // Auditing must never break the action itself (PHP wraps the same call in try/catch).
        }
    }

    private static string Truncate(string? value, int max)
    {
        var text = value ?? string.Empty;
        return text.Length <= max ? text : text[..max];
    }
}
