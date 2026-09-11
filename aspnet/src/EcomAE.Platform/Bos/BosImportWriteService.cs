using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>import_orchestrator</c> <c>create</c> / <c>epc_import_create_job</c>.
/// Process, cancel, retry, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. This write uses the platform operator PDO.
/// </summary>
public interface IBosImportWriteService
{
    Task<ErpSimpleWriteResult> CreateJobAsync(
        string? siteKey,
        string? jobDataJson,
        CancellationToken cancellationToken = default);
}

public sealed class BosImportWriteService : IBosImportWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, (string[] Required, string[] Optional, string UniqueKey)> EntitySchemas
        = new Dictionary<string, (string[] Required, string[] Optional, string UniqueKey)>(StringComparer.Ordinal)
        {
            ["products"] = (["sku", "product_name"], ["price", "stock_qty", "category", "brand", "weight", "description", "image_url", "barcode"], "sku"),
            ["customers"] = (["email"], ["first_name", "last_name", "phone", "company", "address", "city", "country", "tax_id"], "email"),
            ["orders"] = (["order_ref", "customer_email", "total"], ["status", "currency", "shipping_address", "items_json"], "order_ref"),
            ["inventory"] = (["sku", "stock_qty"], ["warehouse", "location", "reorder_point", "cost_price"], "sku"),
            ["gl_entries"] = (["account_code", "debit", "credit"], ["description", "reference", "date", "currency"], ""),
        };

    private readonly IErpWriteConnectionFactory _connections;

    public BosImportWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateJobAsync(
        string? siteKey,
        string? jobDataJson,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        var job = ParseJobData(jobDataJson);
        if (!EntitySchemas.ContainsKey(job.EntityType))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown entity type: " + job.EntityType);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_import_jobs`
                        (`site_key`,`entity_type`,`source_format`,`filename`,`total_rows`,`field_mapping`,`options`,`dry_run`,`created_by`)
                    VALUES (?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                key,
                job.EntityType,
                job.SourceFormat,
                job.Filename,
                job.TotalRows,
                job.FieldMappingJson,
                job.OptionsJson,
                job.DryRun,
                job.CreatedBy).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Import job created", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Import jobs table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP <c>(int)</c> on a token (leading digits; trailing junk ignored).</summary>
    public static long PhpIntval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var text = raw.Trim();
        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        while (i < text.Length && char.IsDigit(text[i]))
        {
            i++;
        }

        if (i == 0 || (i == 1 && text[0] is '+' or '-'))
        {
            return 0;
        }

        return long.TryParse(text[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>
    /// PHP <c>json_decode((string)($_POST['job_data'] ?? '{}'), true) ?: array()</c>
    /// then <c>entity_type ?? products</c>, <c>source_format ?? csv</c>.
    /// </summary>
    public static (
        string EntityType,
        string SourceFormat,
        string Filename,
        long TotalRows,
        string FieldMappingJson,
        string OptionsJson,
        long DryRun,
        long CreatedBy) ParseJobData(string? json)
    {
        JsonElement root = default;
        var hasObject = false;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    root = doc.RootElement.Clone();
                    hasObject = true;
                }
            }
            catch (JsonException)
            {
                hasObject = false;
            }
        }

        var entityType = "products";
        if (hasObject && root.TryGetProperty("entity_type", out _))
        {
            entityType = ReadLooseString(root, "entity_type");
        }

        var sourceFormat = "csv";
        if (hasObject && root.TryGetProperty("source_format", out _))
        {
            sourceFormat = ReadLooseString(root, "source_format");
        }

        return (
            entityType,
            sourceFormat,
            hasObject ? ReadLooseString(root, "filename") : "",
            hasObject ? ReadLong(root, "total_rows") : 0,
            SerializeJsonField(hasObject ? ReadRaw(root, "field_mapping") : null),
            SerializeJsonField(hasObject ? ReadRaw(root, "options") : null),
            hasObject ? ReadLong(root, "dry_run") : 0,
            hasObject ? ReadLong(root, "created_by") : 0);
    }

    public static string SerializeJsonField(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "[]";
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json));
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (!doc.RootElement.EnumerateObject().Any())
                {
                    return "[]";
                }

                return JsonSerializer.Serialize(JsonSerializer.Deserialize<object>(json));
            }

            return "[]";
        }
        catch (JsonException)
        {
            return "[]";
        }
    }

    private static long ReadLong(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
        {
            return 0;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetInt64(out var n) => n,
            JsonValueKind.String => PhpIntval(prop.GetString()),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => 0,
        };
    }

    private static string ReadLooseString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
        {
            return "";
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? "",
            JsonValueKind.Number => prop.ToString(),
            _ => "",
        };
    }

    private static string? ReadRaw(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop)
            || prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return prop.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? prop.GetRawText()
            : prop.ValueKind == JsonValueKind.String
                ? prop.GetString()
                : null;
    }
}
