using System.Data.Common;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_print_template_save</c> / ajax <c>print_designer_save</c> twin.
/// UPDATE/INSERT <c>epc_erp_print_templates</c> for present fields only.
/// Render and schema ensure stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpPrintDesignerSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPrintDesignerSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPrintDesignerSaveWriteRequest(
    long Id = 0,
    IReadOnlyDictionary<string, string>? Fields = null,
    bool IsDefault = false);

public sealed class ErpPrintDesignerSaveWriteService : IErpPrintDesignerSaveWriteService
{
    public static readonly string[] AllowedFields =
    [
        "doc_type", "name", "page_size", "orientation",
        "margin_top", "margin_bottom", "margin_left", "margin_right",
        "font_family", "font_size", "primary_color", "secondary_color",
        "logo_position", "logo_max_height",
        "header_html", "footer_html", "body_columns",
        "show_terms", "terms_html", "show_bank_details", "bank_details_html",
        "show_signature_line", "signature_labels",
        "show_qr_code", "show_barcode", "custom_css",
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPrintDesignerSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPrintDesignerSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var fields = CollectAllowed(request.Fields);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_print_templates", "doc_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Print template table is not provisioned");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long id;
        if (request.Id > 0)
        {
            id = request.Id;
            var sets = new List<string>();
            var args = new List<object?>();
            foreach (var pair in fields)
            {
                sets.Add("`" + pair.Key + "`=?");
                args.Add(pair.Value);
            }

            sets.Add("`time_updated`=?");
            args.Add(now);
            args.Add(id);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_print_templates` SET " + string.Join(", ", sets) + " WHERE `id`=?"),
                cancellationToken,
                args.ToArray()).ConfigureAwait(false);
        }
        else
        {
            var cols = new List<string>();
            var placeholders = new List<string>();
            var args = new List<object?>();
            foreach (var pair in fields)
            {
                cols.Add("`" + pair.Key + "`");
                placeholders.Add("?");
                args.Add(pair.Value);
            }

            cols.Add("`time_created`");
            placeholders.Add("?");
            args.Add(now);
            cols.Add("`time_updated`");
            placeholders.Add("?");
            args.Add(now);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_print_templates` (" + string.Join(",", cols) + ") VALUES (" + string.Join(",", placeholders) + ")"),
                cancellationToken,
                args.ToArray()).ConfigureAwait(false);
            id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            if (id <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Save failed");
            }
        }

        if (request.IsDefault)
        {
            var docType = fields.TryGetValue("doc_type", out var dt) ? dt : string.Empty;
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_print_templates` SET `is_default`=0 WHERE `doc_type`=? AND `id`!=?"),
                cancellationToken,
                docType,
                id).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_print_templates` SET `is_default`=1 WHERE `id`=?"),
                cancellationToken,
                id).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Template saved", id);
    }

    public static Dictionary<string, string> CollectAllowed(IReadOnlyDictionary<string, string>? incoming)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (incoming is null)
        {
            return values;
        }

        foreach (var key in AllowedFields)
        {
            if (incoming.TryGetValue(key, out var raw))
            {
                values[key] = raw;
            }
        }

        return values;
    }

    public static Dictionary<string, string> CollectFromJson(JsonElement root)
    {
        var incoming = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object)
        {
            return incoming;
        }

        foreach (var key in AllowedFields)
        {
            if (TryGetJsonString(root, key, out var value)
                || TryGetJsonString(root, ToCamel(key), out value))
            {
                incoming[key] = value;
            }
        }

        return incoming;
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

    /// <summary>PHP <c>!empty</c> for is_default / confirm flags.</summary>
    public static bool IsPhpNonEmpty(string? raw)
    {
        return !string.IsNullOrEmpty(raw) && raw is not "0";
    }

    private static bool TryGetJsonString(JsonElement root, string name, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(name, out var prop))
        {
            return false;
        }

        value = prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? string.Empty,
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            JsonValueKind.Null => string.Empty,
            _ => prop.GetRawText(),
        };
        return true;
    }

    private static string ToCamel(string snake)
    {
        var parts = snake.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return snake;
        }

        var sb = new StringBuilder(snake.Length);
        sb.Append(parts[0]);
        for (var i = 1; i < parts.Length; i++)
        {
            sb.Append(char.ToUpperInvariant(parts[i][0]));
            if (parts[i].Length > 1)
            {
                sb.Append(parts[i].AsSpan(1));
            }
        }

        return sb.ToString();
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
