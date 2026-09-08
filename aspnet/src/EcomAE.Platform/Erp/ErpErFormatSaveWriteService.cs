using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_er_format_save</c> twin. INSERT/UPDATE <c>epc_er_format</c>.
/// Field add, run generation, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpErFormatSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpErFormatSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpErFormatSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    string? OutputType = null,
    string? RootElement = null,
    string? RowElement = null,
    int? Active = null);

public sealed class ErpErFormatSaveWriteService : IErpErFormatSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpErFormatSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpErFormatSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();
        var type = string.IsNullOrWhiteSpace(request.OutputType) ? "csv" : request.OutputType.Trim();
        var invalid = Validate(code, name, type);
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
        var root = Clip(
            string.IsNullOrWhiteSpace(request.RootElement) ? "rows" : request.RootElement.Trim(),
            60);
        var row = Clip(
            string.IsNullOrWhiteSpace(request.RowElement) ? "row" : request.RowElement.Trim(),
            60);
        var active = request.Active is null ? 1 : (request.Active.Value == 1 ? 1 : 0);
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_er_format", "code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_er_format", "name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Electronic reporting format table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_er_format` SET `code`=?, `name`=?, `output_type`=?, `root_element`=?, `row_element`=?, `active`=? WHERE `id`=?"),
                cancellationToken,
                code,
                name,
                type,
                root,
                row,
                active,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Format saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_er_format` (`company_id`,`code`,`name`,`output_type`,`root_element`,`row_element`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            code,
            name,
            type,
            root,
            row,
            active,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Format saved", id);
    }

    public static string? Validate(string code, string name, string outputType)
    {
        if (code.Length == 0 || name.Length == 0)
        {
            return "Code and name are required";
        }

        if (outputType is not ("csv" or "xml" or "json"))
        {
            return "Output type must be csv, xml or json";
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
