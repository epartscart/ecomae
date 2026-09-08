using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_oa_contact_save</c> twin. INSERT <c>epc_oa_contact</c>,
/// clearing other primaries of the same type when requested.
/// Party/address/calendar/holiday and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpOaContactSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpOaContactSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOaContactSaveWriteRequest(
    long PartyId = 0,
    string? ContactType = null,
    string? Value = null,
    int? IsPrimary = null);

public sealed class ErpOaContactSaveWriteService : IErpOaContactSaveWriteService
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "email", "phone", "mobile", "fax", "url"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpOaContactSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpOaContactSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = request.ContactType ?? "email";
        var invalid = Validate(type);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var value = Clip(request.Value ?? string.Empty, 190);
        var primary = request.IsPrimary is > 0 ? 1 : 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_oa_contact", "contact_type", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_oa_contact", "value", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Address-book contact table is not provisioned");
        }

        if (primary == 1)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_oa_contact` SET `is_primary`=0 WHERE party_id=? AND contact_type=?"),
                cancellationToken,
                request.PartyId,
                type).ConfigureAwait(false);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_oa_contact` (`party_id`,`contact_type`,`value`,`is_primary`) VALUES (?,?,?,?)"),
            cancellationToken,
            request.PartyId,
            type,
            value,
            primary).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Contact saved", id);
    }

    public static string? Validate(string contactType)
    {
        if (!Types.Contains(contactType))
        {
            return "Invalid contact type";
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
