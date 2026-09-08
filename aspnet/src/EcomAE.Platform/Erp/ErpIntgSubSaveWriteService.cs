using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_intg_sub_save</c> / ajax <c>intg_sub_save</c> twin.
/// INSERT <c>epc_intg_event_sub</c>. Event raise and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpIntgSubSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpIntgSubSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpIntgSubSaveWriteRequest(
    long CompanyId = 0,
    string? Event = null,
    string? TargetType = null,
    string? Target = null,
    int? Active = null);

public sealed class ErpIntgSubSaveWriteService : IErpIntgSubSaveWriteService
{
    public static readonly HashSet<string> TargetTypes = new(StringComparer.Ordinal)
    {
        "webhook", "internal", "email",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpIntgSubSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpIntgSubSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ev = Clip((request.Event ?? string.Empty).Trim(), 120);
        var type = (request.TargetType ?? "webhook").Trim();
        var invalid = Validate(type);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var target = Clip((request.Target ?? string.Empty).Trim(), 255);
        var active = request.Active is null || request.Active != 0 ? 1 : 0;
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_intg_event_sub", "event", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_intg_event_sub", "target_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Event subscription table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_intg_event_sub` (`company_id`,`event`,`target_type`,`target`,`active`,`time_created`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            ev,
            type,
            target,
            active,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Subscription saved", id);
    }

    public static string? Validate(string targetType)
        => TargetTypes.Contains(targetType) ? null : "Invalid subscription target type";

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

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
}
