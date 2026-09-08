using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_uae_tax_legislation_checklist_set_status</c> /
/// ajax <c>uae_tax_legislation_checklist_set</c> twin.
/// UPSERT <c>epc_uae_tax_legislation_checklist</c>. FTA fetch, ask, regen,
/// CT adjustments, and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpUaeTaxLegislationChecklistSetWriteService
{
    Task<ErpUaeTaxChecklistWriteResult> SetAsync(
        ErpUaeTaxLegislationChecklistSetWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpUaeTaxLegislationChecklistSetWriteRequest(
    string? ItemKey = null,
    string? ActionKey = null,
    string? ActionText = null,
    string? Status = null,
    bool Done = false,
    IReadOnlyList<string>? AllActions = null);

public sealed record ErpUaeTaxChecklistWriteResult(
    bool Succeeded,
    string Code,
    string Message,
    long Id,
    int Writes,
    string ItemKey,
    string ActionKey,
    string ActionStatus,
    string ImplStatus,
    int ImplPending,
    int ImplDone)
{
    public static ErpUaeTaxChecklistWriteResult Fail(string code, string message) =>
        new(false, code, message, 0, 0, "", "", "pending", "pending", 0, 0);

    public static ErpUaeTaxChecklistWriteResult Ok(
        string message,
        string itemKey,
        string actionKey,
        string actionStatus,
        string implStatus,
        int implPending,
        int implDone) =>
        new(true, "ok", message, 0, 1, itemKey, actionKey, actionStatus, implStatus, implPending, implDone);
}

public sealed class ErpUaeTaxLegislationChecklistSetWriteService : IErpUaeTaxLegislationChecklistSetWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpUaeTaxLegislationChecklistSetWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpUaeTaxChecklistWriteResult> SetAsync(
        ErpUaeTaxLegislationChecklistSetWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var itemKey = (request.ItemKey ?? string.Empty).Trim();
        var actionKey = (request.ActionKey ?? string.Empty).Trim();
        var actionText = (request.ActionText ?? string.Empty).Trim();
        var status = request.Done || string.Equals(request.Status ?? string.Empty, "done", StringComparison.Ordinal)
            ? "done"
            : "pending";
        if (itemKey.Length == 0 || actionKey.Length == 0)
        {
            return ErpUaeTaxChecklistWriteResult.Fail("invalid", "Missing item or action key.");
        }

        if (actionText.Length == 0)
        {
            actionText = actionKey;
        }

        if (!_connections.IsConfigured)
        {
            return ErpUaeTaxChecklistWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_uae_tax_legislation_checklist", "item_key", cancellationToken).ConfigureAwait(false))
        {
            return ErpUaeTaxChecklistWriteResult.Fail("invalid", "Legislation checklist table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_uae_tax_legislation_checklist` (`item_key`, `action_key`, `action_text`, `status`, `time_updated`) VALUES (?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `action_text` = VALUES(`action_text`), `status` = VALUES(`status`), `time_updated` = VALUES(`time_updated`)"),
            cancellationToken,
            itemKey,
            actionKey,
            actionText,
            status,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

        var allTexts = ParseAllActions(request.AllActions, actionText);
        var chk = allTexts.Count == 0
            ? (ImplStatus: "pending", Pending: 0, Done: 0)
            : await WithStatusAsync(connection, itemKey, allTexts, cancellationToken).ConfigureAwait(false);

        var message = status == "done"
            ? "Checklist step marked implemented."
            : "Checklist step marked pending.";
        return ErpUaeTaxChecklistWriteResult.Ok(message, itemKey, actionKey, status, chk.ImplStatus, chk.Pending, chk.Done);
    }

    public static string ActionKey(string actionText)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes((actionText ?? string.Empty).Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..32];
    }

    public static List<string> ParseAllActionsJson(string? json)
    {
        var texts = new List<string>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return texts;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return texts;
            }

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var t = (el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText())?.Trim() ?? "";
                if (t.Length > 0)
                {
                    texts.Add(t);
                }
            }
        }
        catch (JsonException)
        {
            return texts;
        }

        return texts;
    }

    public static List<string> CollectAllActionsFromJson(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (root.TryGetProperty("all_actions_json", out var raw) || root.TryGetProperty("allActionsJson", out raw))
        {
            if (raw.ValueKind == JsonValueKind.String)
            {
                return ParseAllActionsJson(raw.GetString());
            }

            if (raw.ValueKind == JsonValueKind.Array)
            {
                return ParseAllActionsJson(raw.GetRawText());
            }
        }

        if (root.TryGetProperty("all_actions", out var arr) || root.TryGetProperty("allActions", out arr))
        {
            if (arr.ValueKind == JsonValueKind.Array)
            {
                return ParseAllActionsJson(arr.GetRawText());
            }
        }

        return [];
    }

    public static List<string> ParseAllActions(IReadOnlyList<string>? incoming, string actionText)
    {
        var texts = new List<string>();
        if (incoming is not null)
        {
            foreach (var raw in incoming)
            {
                var t = (raw ?? string.Empty).Trim();
                if (t.Length > 0)
                {
                    texts.Add(t);
                }
            }
        }

        if (texts.Count == 0 && actionText.Length > 0)
        {
            texts.Add(actionText);
        }

        return texts;
    }

    private static async Task<(string ImplStatus, int Pending, int Done)> WithStatusAsync(
        DbConnection connection,
        string itemKey,
        IReadOnlyList<string> actions,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("SELECT `action_key`, `status` FROM `epc_uae_tax_legislation_checklist` WHERE `item_key`=?");
        ErpDb.AddParameters(cmd, itemKey);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            map[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
        }

        var pending = 0;
        var done = 0;
        foreach (var text in actions)
        {
            var key = ActionKey(text);
            var st = map.TryGetValue(key, out var saved) && saved == "done" ? "done" : "pending";
            if (st == "done")
            {
                done++;
            }
            else
            {
                pending++;
            }
        }

        var impl = "not_started";
        if (done > 0 && pending == 0)
        {
            impl = "implemented";
        }
        else if (done > 0)
        {
            impl = "in_progress";
        }
        else
        {
            impl = "pending";
        }

        return (impl, pending, done);
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
