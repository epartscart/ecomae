using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpObtainingModeRow(long Id, string Caption, int SortOrder, int Available);

public sealed record CpObtainingModeList(IReadOnlyList<CpObtainingModeRow> Rows, int Total, int Page, int PageSize)
{
    public int Pages => Math.Max(1, (Total + PageSize - 1) / Math.Max(1, PageSize));
}

/// <summary>PHP <c>obtaining_mode.php</c> dynamic parameter (from the mode's <c>parameters</c> JSON schema).</summary>
public sealed record CpObtainingModeParameter(string Name, string Caption, string Type, string Value);

public sealed record CpObtainingModeEditor(
    long Id,
    string Caption,
    string CaptionLangStrId,
    int SortOrder,
    int Available,
    IReadOnlyList<CpObtainingModeParameter> Parameters);

public interface ICpObtainingModeEditorService
{
    Task<CpObtainingModeList> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<CpObtainingModeEditor?> OpenAsync(long modeId, CancellationToken cancellationToken = default);
}

/// <summary>Reads for the CP obtaining-modes (delivery methods) twin; writes stay in <see cref="ICpObtainingModeWriteService"/>.</summary>
public sealed class CpObtainingModeEditorService : ICpObtainingModeEditorService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpObtainingModeEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpObtainingModeList> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 200);
        var rows = new List<CpObtainingModeRow>();
        var total = 0;
        if (!_connections.IsConfigured)
        {
            return new(rows, 0, page, pageSize);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            total = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_obtaining_modes` WHERE `control_available` = 1", cancellationToken).ConfigureAwait(false);
            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            var raw = new List<(long Id, string Caption, int Order, int Available)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`caption`,''), IFNULL(`order`,0), IFNULL(`available`,0) FROM `shop_obtaining_modes` WHERE `control_available` = 1 ORDER BY `id` LIMIT ? OFFSET ?");
                ErpDb.AddParameters(cmd, pageSize, (page - 1) * pageSize);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        reader.GetString(1),
                        Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture)));
                }
            }

            foreach (var r in raw)
            {
                rows.Add(new CpObtainingModeRow(r.Id, await translate(r.Caption).ConfigureAwait(false), r.Order, r.Available));
            }
        }
        catch (DbException)
        {
        }

        return new(rows, total, page, pageSize);
    }

    public async Task<CpObtainingModeEditor?> OpenAsync(long modeId, CancellationToken cancellationToken = default)
    {
        if (modeId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            string captionKey;
            int order;
            int available;
            string parameters;
            string values;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`caption`,''), IFNULL(`order`,0), IFNULL(`available`,0), IFNULL(`parameters`,'[]'), IFNULL(`parameters_values`,'[]') FROM `shop_obtaining_modes` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(cmd, modeId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                captionKey = reader.GetString(0);
                order = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
                available = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture);
                parameters = reader.GetString(3);
                values = reader.GetString(4);
            }

            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            var caption = await translate(captionKey).ConfigureAwait(false);
            var langStrId = captionKey.Length > 0 && captionKey.All(char.IsDigit) ? captionKey : "0";
            return new CpObtainingModeEditor(modeId, caption, langStrId, order, available, ParseParameters(parameters, values));
        }
        catch (DbException)
        {
            return null;
        }
    }

    /// <summary>obtaining_mode.php: <c>parameters</c> is <c>[{name,caption,type}]</c>; <c>parameters_values</c> is <c>{name: value}</c>.</summary>
    public static IReadOnlyList<CpObtainingModeParameter> ParseParameters(string parametersJson, string valuesJson)
    {
        var list = new List<CpObtainingModeParameter>();
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var vdoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(valuesJson) ? "{}" : valuesJson);
            if (vdoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in vdoc.RootElement.EnumerateObject())
                {
                    values[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.ToString();
                }
            }
        }
        catch (JsonException)
        {
        }

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(parametersJson) ? "[]" : parametersJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return list;
            }

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty("name", out var nameEl))
                {
                    continue;
                }

                var name = nameEl.GetString() ?? "";
                if (name.Length == 0)
                {
                    continue;
                }

                var caption = el.TryGetProperty("caption", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? name : name;
                var type = el.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? (t.GetString() ?? "text") : "text";
                type = type.ToLowerInvariant() is "text" or "number" or "password" or "email" or "url" ? type.ToLowerInvariant() : "text";
                list.Add(new CpObtainingModeParameter(name, caption, type, values.TryGetValue(name, out var v) ? v : ""));
            }
        }
        catch (JsonException)
        {
        }

        return list;
    }

    /// <summary>Serialise posted <c>pv_&lt;name&gt;</c> inputs to the PHP <c>parameters_values</c> object.</summary>
    public static string ParametersValuesFromForm(IFormCollection form, IReadOnlyList<CpObtainingModeParameter> schema)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in schema)
        {
            dict[p.Name] = form["pv_" + p.Name].ToString();
        }

        return JsonSerializer.Serialize(dict);
    }
}
