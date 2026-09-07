using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wht_code_save</c> twin. INSERT/UPDATE <c>epc_wht_code</c>.
/// Schema ensure, record, and certificate minting stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpWhtCodeSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpWhtCodeSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpWhtCodeSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    decimal Rate = 0,
    string? Account = null,
    int? Active = null);

public sealed class ErpWhtCodeSaveWriteService : IErpWhtCodeSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWhtCodeSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpWhtCodeSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();
        var invalid = Validate(code, name, request.Rate);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        code = Clip(code, 40);
        name = Clip(name, 160);
        var account = Clip((request.Account ?? string.Empty).Trim(), 60);
        var rate = decimal.Round(request.Rate, 4, MidpointRounding.AwayFromZero);
        var active = request.Active is null ? 1 : (request.Active.Value == 1 ? 1 : 0);
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_wht_code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_wht_code", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_wht_code", "name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Withholding code table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_wht_code` SET `code`=?, `name`=?, `rate`=?, `account`=?, `active`=? WHERE `id`=?"),
                cancellationToken,
                code,
                name,
                rate,
                account,
                active,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Withholding code saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_wht_code` (`company_id`,`code`,`name`,`rate`,`account`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            code,
            name,
            rate,
            account,
            active,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Withholding code saved", id);
    }

    public static string? Validate(string code, string name, decimal rate)
    {
        if (code.Length == 0 || name.Length == 0)
        {
            return "Code and name are required";
        }

        if (rate < 0 || rate > 100)
        {
            return "Rate must be a percentage between 0 and 100";
        }

        return null;
    }

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

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
}
