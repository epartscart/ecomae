using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_oa_address_save</c> twin. INSERT <c>epc_oa_address</c>,
/// clearing other primaries of the same purpose when requested.
/// Party/contact/calendar/holiday and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpOaAddressSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpOaAddressSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOaAddressSaveWriteRequest(
    long PartyId = 0,
    string? Purpose = null,
    string? Line1 = null,
    string? City = null,
    string? State = null,
    string? Postcode = null,
    string? Country = null,
    int? IsPrimary = null);

public sealed class ErpOaAddressSaveWriteService : IErpOaAddressSaveWriteService
{
    private static readonly HashSet<string> Purposes = new(StringComparer.Ordinal)
    {
        "business", "invoice", "delivery", "home", "other"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpOaAddressSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpOaAddressSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var purpose = request.Purpose ?? "business";
        var invalid = Validate(purpose);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var line1 = Clip(request.Line1 ?? string.Empty, 255);
        var city = Clip(request.City ?? string.Empty, 120);
        var state = Clip(request.State ?? string.Empty, 120);
        var postcode = Clip(request.Postcode ?? string.Empty, 40);
        var country = Clip(request.Country ?? string.Empty, 60);
        var primary = request.IsPrimary is > 0 ? 1 : 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_oa_address", "purpose", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_oa_address", "line1", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Address-book address table is not provisioned");
        }

        if (primary == 1)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_oa_address` SET `is_primary`=0 WHERE party_id=? AND purpose=?"),
                cancellationToken,
                request.PartyId,
                purpose).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_oa_address` (`party_id`,`purpose`,`line1`,`city`,`state`,`postcode`,`country`,`is_primary`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            request.PartyId,
            purpose,
            line1,
            city,
            state,
            postcode,
            country,
            primary).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Address saved", id);
    }

    public static string? Validate(string purpose)
    {
        if (!Purposes.Contains(purpose))
        {
            return "Invalid address purpose";
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
