using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live twin of PHP <c>print_docs/print_doc_tuning.php</c> <c>action=save</c>.</summary>
public interface ICpPrintDocsWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        long printDocId,
        long officeId,
        IReadOnlyDictionary<string, string> submitted,
        IReadOnlyCollection<string> clearedImages,
        CancellationToken cancellationToken = default);
}

public sealed class CpPrintDocsWriteService : ICpPrintDocsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPrintDocsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>
    /// PHP parameter merge: text/textarea are html-encoded, checkboxes collapse to 1/0, image files keep the
    /// previous value unless explicitly cleared, and JSON-shaped values (profile builders) are stored as JSON.
    /// </summary>
    public static string MergeValues(
        IReadOnlyList<CpPrintDocParameter> parameters,
        IReadOnlyDictionary<string, string> submitted,
        IReadOnlyCollection<string> clearedImages)
    {
        var result = new JsonObject();
        foreach (var parameter in parameters)
        {
            submitted.TryGetValue(parameter.Name, out var posted);
            posted ??= string.Empty;

            switch (parameter.Type)
            {
                case "checkbox":
                    result[parameter.Name] = JsonValue.Create(
                        posted.Length > 0 && posted is not "0" and not "false" ? 1 : 0);
                    break;
                case "image_file":
                    if (clearedImages.Contains(parameter.Name))
                    {
                        result[parameter.Name] = JsonValue.Create(string.Empty);
                    }
                    else
                    {
                        result[parameter.Name] = JsonValue.Create(posted.Trim().Length > 0 ? posted.Trim() : parameter.Value);
                    }

                    break;
                case "user_profile_json_builder":
                    result[parameter.Name] = ParseJsonOrKeep(posted, parameter.Value);
                    break;
                default:
                    result[parameter.Name] = JsonValue.Create(WebUtility.HtmlEncode(posted));
                    break;
            }
        }

        return result.ToJsonString();
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        long printDocId,
        long officeId,
        IReadOnlyDictionary<string, string> submitted,
        IReadOnlyCollection<string> clearedImages,
        CancellationToken cancellationToken = default)
    {
        if (printDocId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A print document must be selected.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        string docName;
        string description;
        string values;
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT IFNULL(`name`,''), IFNULL(`parameters_description`,''), IFNULL(`parameters_values`,'') FROM `shop_print_docs` WHERE `id` = ?");
            ErpDb.AddParameters(cmd, [printDocId]);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("missing", "Print document not found.");
            }

            docName = r.GetString(0);
            description = r.GetString(1);
            values = r.GetString(2);
        }

        if (officeId > 0)
        {
            var officeValues = await OfficeValuesAsync(connection, docName, officeId, cancellationToken).ConfigureAwait(false);
            if (officeValues is null)
            {
                return ErpSimpleWriteResult.Fail("missing", "This office has no settings row for the document.");
            }

            values = officeValues;
        }

        var parameters = CpPrintDocsDeskService.BuildParameters(description, values);
        if (parameters.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "The document has no tunable parameters.");
        }

        var json = MergeValues(parameters, submitted, clearedImages);
        if (officeId > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_print_docs_wholesaler` SET `parameters_values` = ? WHERE `doc_name` = ? AND `office_id` = ?"),
                cancellationToken,
                json, docName, officeId);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_print_docs` SET `parameters_values` = ? WHERE `id` = ?"),
                cancellationToken,
                json, printDocId);
        }

        return ErpSimpleWriteResult.Ok("Print document settings saved.", printDocId);
    }

    private static async Task<string?> OfficeValuesAsync(
        DbConnection connection,
        string docName,
        long officeId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT IFNULL(`parameters_values`,'') FROM `shop_print_docs_wholesaler` WHERE `doc_name` = ? AND `office_id` = ? LIMIT 1");
        ErpDb.AddParameters(cmd, [docName, officeId]);
        var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    private static JsonNode? ParseJsonOrKeep(string posted, string previous)
    {
        var source = posted.Trim().Length > 0 ? posted : previous;
        if (source.Trim().Length == 0)
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(source);
        }
        catch (JsonException)
        {
            return JsonValue.Create(WebUtility.HtmlEncode(posted));
        }
    }
}
