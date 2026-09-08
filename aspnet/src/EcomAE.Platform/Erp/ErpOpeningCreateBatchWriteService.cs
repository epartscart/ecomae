using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_opening_create_batch</c> / ajax <c>opening_create_batch</c> twin.
/// INSERT draft row on <c>epc_erp_opening_batches</c>. Line add, post, and schema stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpOpeningCreateBatchWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpOpeningCreateBatchWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOpeningCreateBatchWriteRequest(
    string? Module = null,
    string? AsOfDate = null,
    string? Reference = null,
    string? Note = null,
    long AdminId = 0);

public sealed class ErpOpeningCreateBatchWriteService : IErpOpeningCreateBatchWriteService
{
    public static readonly string[] AllowedModules = ["coa", "cash_bank", "inventory", "fixed_assets", "combined"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpOpeningCreateBatchWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpOpeningCreateBatchWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var module = NormalizeModule(request.Module);
        var asOf = NormalizeAsOfDate(request.AsOfDate);
        var reference = (request.Reference ?? "").Trim();
        var note = (request.Note ?? "").Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_opening_batches", "as_of_date", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Opening batches table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_opening_batches` (`module`,`as_of_date`,`reference`,`status`,`note`,`admin_id`,`time_created`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            module,
            asOf,
            reference,
            "draft",
            note,
            request.AdminId,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Opening batch created (draft)", id);
    }

    public static string NormalizeModule(string? module)
    {
        var raw = (module ?? "").Trim();
        return AllowedModules.Contains(raw, StringComparer.Ordinal) ? raw : "combined";
    }

    public static string NormalizeAsOfDate(string? asOfDate)
    {
        var raw = asOfDate ?? "";
        if (raw.Length == 0 || raw == "0")
        {
            return TodayYmd();
        }

        var trimmed = raw.Trim();
        if (DateTime.TryParseExact(
                trimmed,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var day))
        {
            return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return TodayYmd();
    }

    public static string TodayYmd()
        => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool JsonFlag(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n) && n != 0)
            {
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
    }

    public static string JsonText(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return "";
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? "";
            }

            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetRawText();
            }
        }

        return "";
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
