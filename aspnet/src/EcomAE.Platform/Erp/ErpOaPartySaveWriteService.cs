using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_oa_party_save</c> twin. INSERT/UPDATE <c>epc_oa_party</c>.
/// Address, contact, calendar, holiday, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpOaPartySaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpOaPartySaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOaPartySaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Name = null,
    string? PartyType = null);

public sealed class ErpOaPartySaveWriteService : IErpOaPartySaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpOaPartySaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpOaPartySaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = (request.Name ?? string.Empty).Trim();
        var type = request.PartyType ?? "organization";
        var invalid = Validate(name, type);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        name = Clip(name, 190);
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_oa_party", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_oa_party", "party_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Address-book party table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_oa_party` SET `party_type`=?, `name`=?, `time_updated`=? WHERE id=? AND company_id=?"),
                cancellationToken,
                type,
                name,
                now,
                request.Id,
                companyId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Party saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_oa_party` (`company_id`,`party_type`,`name`,`time_updated`) VALUES (?,?,?,?)"),
            cancellationToken,
            companyId,
            type,
            name,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Party saved", id);
    }

    public static string? Validate(string name, string partyType)
    {
        if (name.Length == 0)
        {
            return "Party name is required";
        }

        if (partyType is not ("organization" or "person"))
        {
            return "Invalid party type";
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
