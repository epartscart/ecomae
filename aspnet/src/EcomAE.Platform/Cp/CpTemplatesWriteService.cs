using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpTemplatesSetCurrentRequest(long TemplateId);

public sealed record CpTemplatesDeleteRequest(IReadOnlyList<long> TemplateIds, string DocRoot);

/// <summary>PHP <c>template_edit.php</c> <c>save_template_action</c> form.</summary>
public sealed record CpTemplateSaveRequest(
    long Id,
    string Caption,
    bool SetCurrent,
    IReadOnlyDictionary<string, string> DataValue,
    string DocRoot,
    string DomainPath,
    string SecretSuccession);

public interface ICpTemplatesWriteService
{
    Task<ErpSimpleWriteResult> SetCurrentAsync(CpTemplatesSetCurrentRequest request, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> DeleteAsync(CpTemplatesDeleteRequest request, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> SaveAsync(CpTemplateSaveRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Live PHP <c>templates_manager.php</c> (<c>set_current</c>, <c>delete</c>) and <c>template_edit.php</c> (<c>save_template_action</c>) twin over the <c>templates</c> table.</summary>
public sealed class CpTemplatesWriteService : ICpTemplatesWriteService
{
    private static readonly HttpClient StyleClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly IErpWriteConnectionFactory _connections;

    public CpTemplatesWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static IReadOnlyList<long> ParseIds(string? list, long single)
    {
        var ids = new List<long>();
        if (single > 0)
        {
            ids.Add(single);
        }

        var raw = (list ?? string.Empty).Trim();
        if (raw.Length > 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n) && n > 0)
                        {
                            ids.Add(n);
                        }
                        else if (el.ValueKind == JsonValueKind.String
                                 && long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p > 0)
                        {
                            ids.Add(p);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p > 0)
                    {
                        ids.Add(p);
                    }
                }
            }
        }

        return ids.Distinct().ToArray();
    }

    /// <summary>PHP: <c>DOCUMENT_ROOT/templates/&lt;name&gt;</c> (frontend) or <c>DOCUMENT_ROOT/cp/templates/&lt;name&gt;</c> (backend); name must be a plain folder name.</summary>
    public static string? TemplateDir(string docRoot, string name, int isFrontend)
    {
        name = (name ?? string.Empty).Trim();
        if (docRoot.Length == 0 || name.Length == 0 || name != Path.GetFileName(name) || name is "." or "..")
        {
            return null;
        }

        return isFrontend == 0
            ? Path.Combine(docRoot, "cp", "templates", name)
            : Path.Combine(docRoot, "templates", name);
    }

    /// <summary>PHP <c>str_replace(["\n","\r","\t","'","`",'"','#','--'], '', $s)</c> then trim + htmlentities.</summary>
    public static string CleanName(string? s)
    {
        var v = s ?? string.Empty;
        foreach (var bad in new[] { "\n", "\r", "\t", "'", "`", "\"", "#", "--" })
        {
            v = v.Replace(bad, string.Empty, StringComparison.Ordinal);
        }

        return CpPluginsWriteService.HtmlEntities(v.Trim());
    }

    /// <summary>PHP navbar_style=inverse / link_style=main copy <c>main_color</c> into navbar_color / link_color.</summary>
    public static Dictionary<string, string> ApplyColorRules(IReadOnlyDictionary<string, string> data)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in data)
        {
            if (kv.Key.Length == 0 || CpPluginsWriteService.HtmlEntities(kv.Key) != kv.Key)
            {
                continue;
            }

            values[kv.Key] = StripTags(kv.Value);
        }

        values.TryGetValue("main_color", out var main);
        if (!string.IsNullOrEmpty(main))
        {
            if (values.TryGetValue("navbar_style", out var ns) && ns == "inverse" && values.TryGetValue("navbar_color", out var nc) && nc.Length > 0)
            {
                values["navbar_color"] = main;
            }

            if (values.TryGetValue("link_style", out var ls) && ls == "main" && values.TryGetValue("link_color", out var lc) && lc.Length > 0)
            {
                values["link_color"] = main;
            }
        }

        return values;
    }

    /// <summary>PHP <c>strip_tags($s, '&lt;br&gt;&lt;lang&gt;')</c> loop for non-numeric values.</summary>
    public static string StripTags(string? s)
    {
        var v = s ?? string.Empty;
        if (v.Length == 0 || decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            return v;
        }

        string before;
        do
        {
            before = v;
            v = System.Text.RegularExpressions.Regex.Replace(v, "<(?!/?(br|lang)\\b)[^>]*>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        while (v != before);
        return v;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(CpTemplatesDeleteRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TemplateIds.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select at least one template.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var dirs = new List<string>();
            foreach (var id in request.TemplateIds)
            {
                await using var c = connection.CreateCommand();
                c.CommandText = ErpDb.Positional("SELECT IFNULL(`name`,''), IFNULL(`current`,0), IFNULL(`is_frontend`,1) FROM `templates` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(c, id);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Template was not found.");
                }

                if (Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture) == 1)
                {
                    return ErpSimpleWriteResult.Fail("current", "The current template cannot be deleted.");
                }

                var dir = TemplateDir(request.DocRoot, r.GetString(0), Convert.ToInt32(r.GetValue(2), CultureInfo.InvariantCulture));
                if (dir is not null)
                {
                    dirs.Add(dir);
                }
            }

            foreach (var dir in dirs)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, recursive: true);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            var placeholders = string.Join(" OR ", request.TemplateIds.Select((_, i) => "`id` = @p" + i.ToString(CultureInfo.InvariantCulture)));
            var writes = await ErpDb.ExecuteAsync(connection, null, "DELETE FROM `templates` WHERE " + placeholders, cancellationToken, request.TemplateIds.Cast<object?>().ToArray()).ConfigureAwait(false);
            if (writes <= 0)
            {
                return ErpSimpleWriteResult.Fail("unchanged", "Templates were not deleted.");
            }

            return new ErpSimpleWriteResult(true, "ok", "Templates deleted.", 0, writes);
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(CpTemplateSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A template id is required.");
        }

        var caption = CleanName(request.Caption);
        if (caption.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Caption is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            string name;
            int isFrontend;
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional("SELECT IFNULL(`name`,''), IFNULL(`is_frontend`,1) FROM `templates` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(c, request.Id);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Template was not found.");
                }

                name = r.GetString(0);
                isFrontend = Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture);
            }

            var warning = string.Empty;
            if (request.SetCurrent)
            {
                var switched = await SetCurrentAsync(new CpTemplatesSetCurrentRequest(request.Id), cancellationToken).ConfigureAwait(false);
                if (!switched.Succeeded)
                {
                    warning = " Current template was not switched: " + switched.Message;
                }
            }

            var values = ApplyColorRules(request.DataValue);
            var json = JsonSerializer.Serialize(values);
            await GenerateStyleAsync(request, name, isFrontend, values, json, cancellationToken).ConfigureAwait(false);

            var writes = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `templates` SET `caption` = ?, `data_value` = ? WHERE `id` = ?"),
                cancellationToken,
                caption,
                json,
                request.Id).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Template saved." + warning, request.Id, Math.Max(1, writes));
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    /// <summary>PHP posts <c>key/dir/data_value</c> to <c>templates/&lt;name&gt;/assets/css/generate_style/generate_style.php</c> when it exists and <c>main_color</c> is set.</summary>
    private static async Task GenerateStyleAsync(CpTemplateSaveRequest request, string name, int isFrontend, Dictionary<string, string> values, string json, CancellationToken cancellationToken)
    {
        if (isFrontend == 0 || !values.TryGetValue("main_color", out var main) || string.IsNullOrEmpty(main) || request.DomainPath.Length == 0)
        {
            return;
        }

        var dir = TemplateDir(request.DocRoot, name, 1);
        if (dir is null || !File.Exists(Path.Combine(dir, "assets", "css", "generate_style", "generate_style.php")))
        {
            return;
        }

        try
        {
            var url = request.DomainPath.TrimEnd('/') + "/templates/" + Uri.EscapeDataString(name) + "/assets/css/generate_style/generate_style.php";
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["key"] = request.SecretSuccession,
                ["dir"] = name,
                ["data_value"] = json,
            });
            using var response = await StyleClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException)
        {
        }
    }

    public async Task<ErpSimpleWriteResult> SetCurrentAsync(CpTemplatesSetCurrentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TemplateId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A template id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "templates", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "templates table is not provisioned. Schema ensure stays on the Classic twin.");
            }

            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `templates` WHERE `id` = ?"),
                cancellationToken,
                request.TemplateId).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Template was not found.");
            }

            var isFrontend = (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`is_frontend`, 0) FROM `templates` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                request.TemplateId).ConfigureAwait(false);
            var already = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`current`, 0) FROM `templates` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                request.TemplateId).ConfigureAwait(false);
            if (already != 0)
            {
                return ErpSimpleWriteResult.Ok("This template is already current.", request.TemplateId);
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `templates` SET `current` = 0 WHERE `is_frontend` = ?"),
                cancellationToken,
                isFrontend).ConfigureAwait(false);
            var writes = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `templates` SET `current` = 1 WHERE `id` = ?"),
                cancellationToken,
                request.TemplateId).ConfigureAwait(false);
            if (writes <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("unchanged", "Current template was not updated.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Current template updated.", request.TemplateId);
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }
}
