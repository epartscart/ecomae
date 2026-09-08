using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_fin_alloc_rule_save</c> / ajax <c>fin_alloc_save</c> twin.
/// UPDATE <c>epc_fin_alloc_rule</c> when <c>id</c> &gt; 0, else INSERT.
/// Alloc run, accrual save, FX revalue, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpFinAllocSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpFinAllocSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFinAllocSaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Code = null,
    string? Name = null,
    string? SourceAccount = null,
    string? Basis = null,
    int? Active = null);

public sealed class ErpFinAllocSaveWriteService : IErpFinAllocSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFinAllocSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpFinAllocSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var code = request.Code ?? string.Empty;
        var name = request.Name ?? string.Empty;
        var sourceAccount = request.SourceAccount ?? string.Empty;
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var active = request.Active ?? 1;
        var basisJson = EncodeBasis(ParseBasis(request.Basis));

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_fin_alloc_rule", "code", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Allocation rule table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_fin_alloc_rule` SET `code`=?, `name`=?, `source_account`=?, `basis`=?, `active`=? WHERE `id`=?"),
                cancellationToken,
                code,
                name,
                sourceAccount,
                basisJson,
                active,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Allocation rule saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_fin_alloc_rule` (`company_id`,`code`,`name`,`source_account`,`basis`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            code,
            name,
            sourceAccount,
            basisJson,
            active,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Allocation rule saved", id);
    }

    /// <summary>PHP ajax <c>fin_alloc_save</c> dest|weight line parser.</summary>
    public static Dictionary<string, decimal> ParseBasis(string? text)
    {
        var basis = new Dictionary<string, decimal>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(text))
        {
            return basis;
        }

        foreach (var raw in text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
        {
            var line = raw.Trim();
            if (line.Length == 0 || !line.Contains('|', StringComparison.Ordinal))
            {
                continue;
            }

            var split = line.Split('|', 2);
            var dest = split[0].Trim();
            if (dest.Length == 0)
            {
                continue;
            }

            var weight = 0m;
            if (split.Length > 1)
            {
                decimal.TryParse(split[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out weight);
            }

            basis[dest] = weight;
        }

        return basis;
    }

    public static string EncodeBasis(IReadOnlyDictionary<string, decimal> basis)
    {
        if (basis.Count == 0)
        {
            return "[]";
        }

        return JsonSerializer.Serialize(basis);
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
