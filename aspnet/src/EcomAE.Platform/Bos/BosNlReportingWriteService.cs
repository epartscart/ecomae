using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>nl_reporting</c> <c>create</c> / <c>epc_nlr_create_definition</c>.
/// Run, generate, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. This write uses the platform operator PDO.
/// </summary>
public interface IBosNlReportingWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        string? siteKey,
        string? reportDataJson,
        CancellationToken cancellationToken = default);
}

public sealed class BosNlReportingWriteService : IBosNlReportingWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosNlReportingWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP <c>(int)</c> on a token (leading optional sign + digits).</summary>
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

    private static long PhpIntval(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var n) => n,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => (long)d,
            JsonValueKind.String => PhpIntval(element.GetString()),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => 0
        };

    private static string JsonString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "";
        }

        return el.ValueKind == JsonValueKind.String ? (el.GetString() ?? "") : el.GetRawText();
    }

    /// <summary>
    /// PHP <c>json_encode($data['parameters'] ?? array())</c> — missing/null/empty-object becomes <c>[]</c>
    /// because <c>json_decode(..., true)</c> turns <c>{}</c> into an empty PHP array.
    /// </summary>
    public static string EncodeJsonField(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "[]";
        }

        if (el.ValueKind == JsonValueKind.Object && !el.EnumerateObject().Any())
        {
            return "[]";
        }

        return el.GetRawText();
    }

    /// <summary>PHP <c>json_decode((string)($_POST['report_data'] ?? '{}'), true) ?: array()</c>.</summary>
    public static (
        string Name,
        string Description,
        string ReportType,
        string QueryTemplate,
        string ParametersJson,
        string Schedule,
        string Format,
        string RecipientsJson,
        long CreatedBy) ParseReportData(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ("Custom Report", "", "custom", "", "[]", "manual", "csv", "[]", 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ("Custom Report", "", "custom", "", "[]", "manual", "csv", "[]", 0);
            }

            var root = doc.RootElement;
            var name = root.TryGetProperty("name", out _)
                ? JsonString(root, "name")
                : "Custom Report";
            var reportType = root.TryGetProperty("report_type", out _)
                ? JsonString(root, "report_type")
                : "custom";
            var schedule = root.TryGetProperty("schedule", out _)
                ? JsonString(root, "schedule")
                : "manual";
            var format = root.TryGetProperty("format", out _)
                ? JsonString(root, "format")
                : "csv";
            var createdBy = root.TryGetProperty("created_by", out var byEl)
                            && byEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? PhpIntval(byEl)
                : 0;
            return (
                name,
                JsonString(root, "description"),
                reportType,
                JsonString(root, "query_template"),
                EncodeJsonField(root, "parameters"),
                schedule,
                format,
                EncodeJsonField(root, "recipients"),
                createdBy);
        }
        catch (JsonException)
        {
            return ("Custom Report", "", "custom", "", "[]", "manual", "csv", "[]", 0);
        }
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        string? siteKey,
        string? reportDataJson,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        var parsed = ParseReportData(reportDataJson);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_report_definitions` (`site_key`, `name`, `description`, `report_type`, `query_template`, `parameters`, `schedule`, `format`, `recipients`, `created_by`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                key,
                parsed.Name,
                parsed.Description,
                parsed.ReportType,
                parsed.QueryTemplate,
                parsed.ParametersJson,
                parsed.Schedule,
                parsed.Format,
                parsed.RecipientsJson,
                parsed.CreatedBy).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("NL report definition created", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Report definitions table is missing — schema-ensure stays Classic.");
        }
    }
}
