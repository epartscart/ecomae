using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_pm_cheque_save</c> / ajax <c>pm_cheque_save</c> twin.
/// INSERT <c>epc_erp_pm_cheques</c> with status <c>printed</c>. Listing, toggle, budget, and schema stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpPmChequeSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPmChequeSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPmChequeSaveWriteRequest(
    long BankAccountId = 0,
    string? ChequeNo = null,
    string? PayTo = null,
    decimal Amount = 0,
    string? ChequeDate = null,
    string? Memo = null);

public sealed class ErpPmChequeSaveWriteService : IErpPmChequeSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPmChequeSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPmChequeSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var chequeNo = (request.ChequeNo ?? "").Trim();
        var payTo = (request.PayTo ?? "").Trim();
        var memo = (request.Memo ?? "").Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var chequeDate = ResolveChequeDate(request.ChequeDate, now);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_pm_cheques", "cheque_no", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cheque table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_pm_cheques` (`bank_account_id`,`cheque_no`,`pay_to`,`amount`,`cheque_date`,`memo`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            (int)request.BankAccountId,
            chequeNo,
            payTo,
            request.Amount,
            chequeDate,
            memo,
            "printed",
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Cheque recorded", id);
    }

    /// <summary>PHP <c>!empty($cheque_date) ? strtotime(...) : time()</c> then <c>$date ?: time()</c>.</summary>
    public static long ResolveChequeDate(string? raw, long fallbackUnix)
    {
        if (!IsPhpNonEmpty(raw))
        {
            return fallbackUnix;
        }

        var text = raw!.Trim();
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix)
            && unix > 0)
        {
            return unix;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed.ToUnixTimeSeconds();
        }

        return fallbackUnix;
    }

    /// <summary>PHP <c>!empty</c>.</summary>
    public static bool IsPhpNonEmpty(string? raw)
    {
        return !string.IsNullOrEmpty(raw) && raw is not "0";
    }

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

            if (prop.ValueKind == JsonValueKind.String && IsPhpNonEmpty(prop.GetString()))
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
                return (prop.GetString() ?? "").Trim();
            }

            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetRawText();
            }
        }

        return "";
    }

    public static long JsonLong(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
            {
                return n;
            }

            if (prop.ValueKind == JsonValueKind.String
                && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }
        }

        return 0;
    }

    public static decimal JsonDec(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var d))
            {
                return d;
            }

            if (prop.ValueKind == JsonValueKind.String
                && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
        }

        return 0;
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
