using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_plt_feature_save</c> / ajax <c>plt_feature_save</c> twin.
/// INSERT/UPDATE <c>epc_plt_feature</c>. Feature toggle and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpPltFeatureSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPltFeatureSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPltFeatureSaveWriteRequest(
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    int? Enabled = null);

public sealed class ErpPltFeatureSaveWriteService : IErpPltFeatureSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPltFeatureSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPltFeatureSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        var invalid = Validate(code);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        code = Clip(code, 60);
        var name = Clip(request.Name ?? string.Empty, 160);
        var enabled = request.Enabled is > 0 ? 1 : 0;
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_plt_feature", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_plt_feature", "enabled", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Feature flag table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_plt_feature` (`company_id`,`code`,`name`,`enabled`,`time_updated`) VALUES (?,?,?,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `enabled`=VALUES(`enabled`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            companyId,
            code,
            name,
            enabled,
            now).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT id FROM `epc_plt_feature` WHERE company_id=? AND code=? LIMIT 1"),
            cancellationToken,
            companyId,
            code).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Feature saved", id);
    }

    public static string? Validate(string code)
        => code.Length == 0 ? "Feature code is required" : null;

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
