using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_qm_ncr_update</c> / ajax <c>qm_ncr_update</c> twin.
/// UPDATE <c>epc_qm_ncr</c> status/disposition/corrective_action.
/// Order create/record, NCR create, and schema stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpQmNcrUpdateWriteService
{
    Task<ErpSimpleWriteResult> UpdateAsync(
        ErpQmNcrUpdateWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpQmNcrUpdateWriteRequest(
    long Id = 0,
    string? Status = null,
    string? Disposition = null,
    string? CorrectiveAction = null);

public sealed class ErpQmNcrUpdateWriteService : IErpQmNcrUpdateWriteService
{
    public const string InvalidStatus = "Invalid status";
    public const string InvalidDisposition = "Invalid disposition";

    private static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "open", "investigate", "action", "closed",
    };

    private static readonly HashSet<string> Dispositions = new(StringComparer.Ordinal)
    {
        "use_as_is", "rework", "scrap", "return",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpQmNcrUpdateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> UpdateAsync(
        ErpQmNcrUpdateWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var status = request.Status ?? "open";
        if (!Statuses.Contains(status))
        {
            return ErpSimpleWriteResult.Fail("invalid", InvalidStatus);
        }

        var disp = request.Disposition ?? "";
        if (disp.Length > 0 && !Dispositions.Contains(disp))
        {
            return ErpSimpleWriteResult.Fail("invalid", InvalidDisposition);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var action = request.CorrectiveAction ?? "";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_qm_ncr", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "NCR table is not provisioned");
        }

        var closed = status == "closed" ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_qm_ncr` SET `status`=?, `disposition`=?, `corrective_action`=?, `time_closed`=? WHERE id=?"),
            cancellationToken,
            status, disp, action, closed, request.Id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Non-conformance updated", request.Id);
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

            if (prop.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(prop.GetString())
                && prop.GetString() is not "0")
            {
                return true;
            }
        }

        return false;
    }

    public static bool JsonHas(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out _))
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
