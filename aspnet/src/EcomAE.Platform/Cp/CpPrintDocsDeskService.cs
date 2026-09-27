using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>One entry of PHP <c>shop_print_docs.parameters_description</c> with its current value.</summary>
public sealed record CpPrintDocParameter(
    string Name,
    string Caption,
    string Type,
    string Hint,
    string Value);

public sealed record CpPrintDocSummary(long Id, string Name, string Caption, int ParameterCount);

/// <summary>Wholesaler office scope (PHP <c>shop_print_docs_wholesaler</c>).</summary>
public sealed record CpPrintDocOffice(long Id, string Label);

public sealed record CpPrintDocsDesk(
    bool Available,
    string Message,
    IReadOnlyList<CpPrintDocSummary> Documents,
    CpPrintDocSummary? Selected,
    IReadOnlyList<CpPrintDocParameter> Parameters,
    IReadOnlyList<CpPrintDocOffice> Offices,
    long OfficeId)
{
    public static CpPrintDocsDesk Unavailable(string message)
        => new(false, message, [], null, [], [], 0);
}

/// <summary>
/// Typed twin of PHP <c>print_docs/print_doc_tuning.php</c> page rendering: the document list plus the
/// <c>parameters_description</c>/<c>parameters_values</c> widget pairs, optionally scoped to a wholesaler office.
/// </summary>
public interface ICpPrintDocsDeskService
{
    Task<CpPrintDocsDesk> LoadAsync(long printDocId, long officeId, CancellationToken cancellationToken = default);
}

public sealed class CpPrintDocsDeskService : ICpPrintDocsDeskService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPrintDocsDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>json_decode(parameters_description)</c> merged with <c>parameters_values</c>.</summary>
    public static IReadOnlyList<CpPrintDocParameter> BuildParameters(string description, string values)
    {
        var current = ReadValues(values);
        var list = new List<CpPrintDocParameter>();
        if (description.Trim().Length == 0)
        {
            return list;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(description);
        }
        catch (JsonException)
        {
            return list;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return list;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = Text(item, "name");
                if (name.Length == 0)
                {
                    continue;
                }

                list.Add(new CpPrintDocParameter(
                    name,
                    Text(item, "caption"),
                    Text(item, "type") is { Length: > 0 } type ? type : "text",
                    Text(item, "hint"),
                    current.TryGetValue(name, out var value) ? value : string.Empty));
            }
        }

        return list;
    }

    /// <summary>Flattens <c>parameters_values</c>; non-scalar values (user profile builders) stay as raw JSON.</summary>
    public static Dictionary<string, string> ReadValues(string values)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (values.Trim().Length == 0)
        {
            return map;
        }

        try
        {
            using var doc = JsonDocument.Parse(values);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                map[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.Value.ToString(),
                    _ => property.Value.GetRawText(),
                };
            }
        }
        catch (JsonException)
        {
            return map;
        }

        return map;
    }

    public async Task<CpPrintDocsDesk> LoadAsync(long printDocId, long officeId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPrintDocsDesk.Unavailable("No database configured — print document tuning is read-only.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var documents = new List<CpPrintDocSummary>();
            var descriptions = new Dictionary<long, (string Name, string Description, string Values)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`caption`,''), IFNULL(`parameters_description`,''), IFNULL(`parameters_values`,'') "
                                  + "FROM `shop_print_docs` ORDER BY `id`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                    var name = r.GetString(1);
                    var caption = r.GetString(2);
                    var description = r.GetString(3);
                    var values = r.GetString(4);
                    descriptions[id] = (name, description, values);
                    documents.Add(new CpPrintDocSummary(
                        id,
                        name,
                        caption.Length > 0 ? caption : name,
                        BuildParameters(description, "").Count));
                }
            }

            if (documents.Count == 0)
            {
                return new CpPrintDocsDesk(true, "No print documents are registered for this tenant.", documents, null, [], [], 0);
            }

            var selected = documents.FirstOrDefault(d => d.Id == printDocId) ?? documents[0];
            var (docName, docDescription, docValues) = descriptions[selected.Id];

            var offices = await LoadOfficesAsync(connection, docName, cancellationToken).ConfigureAwait(false);
            var activeOffice = 0L;
            if (offices.Count > 0)
            {
                activeOffice = offices.Any(o => o.Id == officeId) ? officeId : offices[0].Id;
                var officeValues = await LoadOfficeValuesAsync(connection, docName, activeOffice, cancellationToken).ConfigureAwait(false);
                if (officeValues is not null)
                {
                    docValues = officeValues;
                }
            }

            return new CpPrintDocsDesk(
                true,
                "",
                documents,
                selected,
                BuildParameters(docDescription, docValues),
                offices,
                activeOffice);
        }
        catch (DbException ex)
        {
            return CpPrintDocsDesk.Unavailable("Print documents could not be loaded: " + ex.Message);
        }
    }

    /// <summary>Wholesaler scope only: offices that already have a per-office settings row for the document.</summary>
    private static async Task<IReadOnlyList<CpPrintDocOffice>> LoadOfficesAsync(
        DbConnection connection,
        string docName,
        CancellationToken cancellationToken)
    {
        var list = new List<CpPrintDocOffice>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT `shop_offices`.`id`, IFNULL(`shop_offices`.`caption`,''), IFNULL(`shop_offices`.`city`,''), IFNULL(`shop_offices`.`address`,'') "
                + "FROM `shop_offices` INNER JOIN `shop_print_docs_wholesaler` "
                + "ON `shop_print_docs_wholesaler`.`office_id` = `shop_offices`.`id` AND `shop_print_docs_wholesaler`.`doc_name` = ? "
                + "ORDER BY `shop_offices`.`id`");
            ErpDb.AddParameters(cmd, [docName]);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                var parts = new[] { r.GetString(1), r.GetString(2), r.GetString(3) }
                    .Where(p => p.Trim().Length > 0)
                    .ToArray();
                list.Add(new CpPrintDocOffice(
                    id,
                    (parts.Length == 0 ? "Office" : string.Join(", ", parts))
                    + " (ID " + id.ToString(CultureInfo.InvariantCulture) + ")"));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return list;
    }

    private static async Task<string?> LoadOfficeValuesAsync(
        DbConnection connection,
        string docName,
        long officeId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT IFNULL(`parameters_values`,'') FROM `shop_print_docs_wholesaler` WHERE `doc_name` = ? AND `office_id` = ? LIMIT 1");
            ErpDb.AddParameters(cmd, [docName, officeId]);
            var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static string Text(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
