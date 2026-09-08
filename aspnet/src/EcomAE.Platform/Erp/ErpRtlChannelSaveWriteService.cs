using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rtl_channel_save</c> twin. INSERT/UPDATE <c>epc_rtl_channel</c>.
/// Assortment, discount, POS, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpRtlChannelSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpRtlChannelSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRtlChannelSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    string? ChannelType = null,
    string? Currency = null,
    int? Active = null);

public sealed class ErpRtlChannelSaveWriteService : IErpRtlChannelSaveWriteService
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "store", "online", "callcenter"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpRtlChannelSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpRtlChannelSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        var type = request.ChannelType ?? "store";
        var invalid = Validate(code, type);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        code = Clip(code, 40);
        var name = Clip(request.Name ?? string.Empty, 160);
        var currency = Clip(request.Currency ?? string.Empty, 8);
        var active = request.Active is > 0 ? 1 : 0;
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_rtl_channel", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rtl_channel", "channel_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Retail channel table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_rtl_channel` SET `name`=?, `channel_type`=?, `currency`=?, `active`=?, `time_updated`=? WHERE id=? AND company_id=?"),
                cancellationToken,
                name,
                type,
                currency,
                active,
                now,
                request.Id,
                companyId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Channel saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_rtl_channel` (`company_id`,`code`,`name`,`channel_type`,`currency`,`active`,`time_updated`) VALUES (?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `channel_type`=VALUES(`channel_type`), `currency`=VALUES(`currency`), `active`=VALUES(`active`), `time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            companyId,
            code,
            name,
            type,
            currency,
            active,
            now).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT id FROM `epc_rtl_channel` WHERE company_id=? AND code=? LIMIT 1"),
            cancellationToken,
            companyId,
            code).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Channel saved", id);
    }

    public static string? Validate(string code, string channelType)
    {
        if (code.Length == 0)
        {
            return "Channel code is required";
        }

        if (!Types.Contains(channelType))
        {
            return "Invalid channel type";
        }

        return null;
    }

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
