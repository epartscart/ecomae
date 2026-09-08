using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rtl_discount_save</c> / ajax <c>rtl_discount_save</c> twin.
/// INSERT <c>epc_rtl_discount</c>. Channel save, assortment, POS, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpRtlDiscountSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpRtlDiscountSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRtlDiscountSaveWriteRequest(
    long CompanyId = 0,
    long ChannelId = 0,
    string? Code = null,
    string? Name = null,
    string? DiscType = null,
    decimal Value = 0,
    long Starts = 0,
    long Ends = 0,
    int? Active = null);

public sealed class ErpRtlDiscountSaveWriteService : IErpRtlDiscountSaveWriteService
{
    public static readonly HashSet<string> DiscTypes = new(StringComparer.Ordinal)
    {
        "percent", "amount",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpRtlDiscountSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpRtlDiscountSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = request.DiscType ?? "percent";
        var invalid = Validate(type);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var code = request.Code ?? string.Empty;
        var name = request.Name ?? string.Empty;
        var value = decimal.Round(request.Value, 4, MidpointRounding.AwayFromZero);
        var active = request.Active is null || request.Active == 0 ? 0 : 1;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_rtl_discount", "disc_type", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rtl_discount", "value", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Discount table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_rtl_discount` (`company_id`,`channel_id`,`code`,`name`,`disc_type`,`value`,`starts`,`ends`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            request.ChannelId,
            code,
            name,
            type,
            value,
            request.Starts,
            request.Ends,
            active,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Discount saved", id);
    }

    public static string? Validate(string discType)
        => DiscTypes.Contains(discType) ? null : "Invalid discount type";

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
