using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpPluginsActivateRequest(string? PluginsList, long PluginId, int FlagValue);

public sealed record CpPluginsDeleteRequest(string? PluginsList, long PluginId, string DocRoot);

/// <summary>PHP <c>plugin_edit.php</c> <c>save_plugin_action</c> form.</summary>
public sealed record CpPluginSaveRequest(
    long Id,
    string Caption,
    string CaptionLangStrId,
    string Description,
    string DescriptionLangStrId,
    int Activated,
    int Order,
    IReadOnlyDictionary<string, string> DataValue,
    IReadOnlyDictionary<string, string> DataValueLangStrIds,
    IReadOnlyList<string> DataValueLang,
    IReadOnlyDictionary<string, string> PhpConfig);

public interface ICpPluginsWriteService
{
    Task<ErpSimpleWriteResult> ActivateAsync(CpPluginsActivateRequest request, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> ActivateAsync(CpPluginsActivateRequest request, IReadOnlyDictionary<string, string> phpConfig, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> DeleteAsync(CpPluginsDeleteRequest request, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> SaveAsync(CpPluginSaveRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Live PHP <c>plugins_manager.php</c> (<c>activated</c>, <c>delete</c>) and <c>plugin_edit.php</c> (<c>save_plugin_action</c>) twin over the <c>plugins</c> table.</summary>
public sealed class CpPluginsWriteService : ICpPluginsWriteService
{
    public const long BackendTwoFactorPluginId = 10;

    private static readonly string[] SmtpConfigKeys = ["from_name", "from_email", "smtp_mode", "smtp_encryption", "smtp_host", "smtp_port", "smtp_user", "smtp_password"];

    private readonly IErpWriteConnectionFactory _connections;

    public CpPluginsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>str_replace('&lt;backend_dir&gt;', 'cp', path)</c> under DOCUMENT_ROOT, rejecting traversal outside the root.</summary>
    public static string? ResolvePluginPath(string docRoot, string relative)
    {
        var rel = (relative ?? string.Empty).Replace("<backend_dir>", "cp", StringComparison.Ordinal).Trim().TrimStart('/', '\\');
        if (rel.Length == 0 || docRoot.Length == 0)
        {
            return null;
        }

        var root = Path.GetFullPath(docRoot);
        var full = Path.GetFullPath(Path.Combine(root, rel));
        var rootSlash = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(rootSlash, StringComparison.Ordinal) ? full : null;
    }

    /// <summary>PHP plugin 10 guard: 2FA needs a debug-verified email (config smtp_* + notifications_settings backend_2fa_email) or SMS (sms_api active + backend_2fa_phone) channel.</summary>
    public static async Task<bool> TwoFactorChannelReadyAsync(DbConnection connection, IReadOnlyDictionary<string, string> phpConfig, CancellationToken cancellationToken)
    {
        var smtpOk = SmtpConfigKeys.All(k => phpConfig.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v));
        if (smtpOk)
        {
            var emailDebug = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT IFNULL(`status`,0) FROM `debug_results` WHERE `name` = ? LIMIT 1"), cancellationToken, "email").ConfigureAwait(false);
            if (emailDebug == 1)
            {
                var emailOn = await ErpDb.LongAsync(connection, null,
                    "SELECT IFNULL(`email_on`,0) FROM `notifications_settings` WHERE `name` = 'backend_2fa_email' LIMIT 1", cancellationToken).ConfigureAwait(false);
                if (emailOn == 1)
                {
                    return true;
                }
            }
        }

        var smsActive = await ErpDb.LongAsync(connection, null,
            ErpDb.Positional("SELECT COUNT(*) FROM `sms_api` WHERE `active` = ?"), cancellationToken, 1).ConfigureAwait(false);
        if (smsActive == 1)
        {
            var smsDebug = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT IFNULL(`status`,0) FROM `debug_results` WHERE `name` = ? LIMIT 1"), cancellationToken, "sms").ConfigureAwait(false);
            if (smsDebug == 1)
            {
                var smsOn = await ErpDb.LongAsync(connection, null,
                    "SELECT IFNULL(`sms_on`,0) FROM `notifications_settings` WHERE `name` = 'backend_2fa_phone' LIMIT 1", cancellationToken).ConfigureAwait(false);
                if (smsOn == 1)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(CpPluginsDeleteRequest request, CancellationToken cancellationToken = default)
    {
        var ids = ParsePluginIds(request.PluginsList, request.PluginId);
        if (ids.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one plugin id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "plugins", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "plugins table is not provisioned.");
            }

            var dirsFiles = new List<string>();
            foreach (var id in ids)
            {
                var lockFlag = await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT IFNULL(`control_lock`, 0) FROM `plugins` WHERE `id` = ? LIMIT 1"), cancellationToken, id).ConfigureAwait(false);
                if (lockFlag != 0)
                {
                    return ErpSimpleWriteResult.Fail("locked", "Control of one of the selected plugins is locked.");
                }

                var json = await ErpDb.StringAsync(connection, null,
                    ErpDb.Positional("SELECT IFNULL(`dirs_files`,'') FROM `plugins` WHERE `id` = ? LIMIT 1"), cancellationToken, id).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    dirsFiles.Add(json);
                }
            }

            foreach (var json in dirsFiles)
            {
                RemoveDirsFiles(request.DocRoot, json);
            }

            var placeholders = string.Join(" OR ", ids.Select((_, i) => "`id` = @p" + i.ToString(CultureInfo.InvariantCulture)));
            var writes = await ErpDb.ExecuteAsync(connection, null, "DELETE FROM `plugins` WHERE " + placeholders, cancellationToken, ids.Cast<object?>().ToArray()).ConfigureAwait(false);
            if (writes <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "No plugins were deleted.");
            }

            return new ErpSimpleWriteResult(true, "ok", "Plugins deleted.", 0, writes);
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    private static void RemoveDirsFiles(string docRoot, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("path", out var p) || !e.TryGetProperty("type", out var t))
                {
                    continue;
                }

                var full = ResolvePluginPath(docRoot, p.GetString() ?? string.Empty);
                if (full is null)
                {
                    continue;
                }

                try
                {
                    if (t.GetString() == "dir" && Directory.Exists(full))
                    {
                        Directory.Delete(full, recursive: true);
                    }
                    else if (t.GetString() == "file" && File.Exists(full))
                    {
                        File.Delete(full);
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (JsonException)
        {
        }
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(CpPluginSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A plugin id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Caption))
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
            var exists = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `plugins` WHERE `id` = ?"), cancellationToken, request.Id).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Plugin was not found.");
            }

            var lockFlag = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT IFNULL(`control_lock`, 0) FROM `plugins` WHERE `id` = ? LIMIT 1"), cancellationToken, request.Id).ConfigureAwait(false);
            if (lockFlag != 0)
            {
                return ErpSimpleWriteResult.Fail("locked", "Control of this plugin is locked.");
            }

            if (request.Id == BackendTwoFactorPluginId && request.Activated == 1
                && !await TwoFactorChannelReadyAsync(connection, request.PhpConfig, cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("two_factor", "This plugin cannot be activated until SMS or email notifications are configured and verified.");
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var translations = new CpCustomTranslationWriter("PLUGIN EDITING");
            var captionKey = await translations.SaveAsync(connection, transaction, request.CaptionLangStrId, HtmlEntities(request.Caption), "en", null, cancellationToken).ConfigureAwait(false);
            var descriptionKey = await translations.SaveAsync(connection, transaction, request.DescriptionLangStrId, HtmlEntities(request.Description), "en", null, cancellationToken).ConfigureAwait(false);

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in request.DataValue)
            {
                if (kv.Key.Length == 0 || HtmlEntities(kv.Key) != kv.Key)
                {
                    continue;
                }

                var item = HtmlEntities(kv.Value);
                if (request.DataValueLang.Contains(kv.Key, StringComparer.Ordinal))
                {
                    request.DataValueLangStrIds.TryGetValue(kv.Key, out var strId);
                    item = await translations.SaveAsync(connection, transaction, strId, item, "en", null, cancellationToken).ConfigureAwait(false);
                }

                values[kv.Key] = item;
            }

            var writes = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `plugins` SET `caption` = ?, `description` = ?, `activated` = ?, `order` = ?, `data_value` = ? WHERE `id` = ?"),
                cancellationToken,
                captionKey,
                descriptionKey,
                NormalizeFlag(request.Activated),
                request.Order,
                JsonSerializer.Serialize(values),
                request.Id).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Plugin saved.", request.Id, Math.Max(1, writes));
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    /// <summary>PHP <c>htmlentities($s, ENT_QUOTES, 'UTF-8', false)</c> for the characters it escapes.</summary>
    public static string HtmlEntities(string? s)
        => (s ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("&amp;amp;", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    public static IReadOnlyList<long> ParsePluginIds(string? pluginsList, long pluginId)
    {
        var ids = new List<long>();
        if (pluginId > 0)
        {
            ids.Add(pluginId);
        }

        var raw = (pluginsList ?? string.Empty).Trim();
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
                                 && long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                                 && parsed > 0)
                        {
                            ids.Add(parsed);
                        }
                    }
                }
            }
            catch (JsonException)
            {
                return ids.Count > 0 ? ids.Distinct().ToArray() : [];
            }
        }

        return ids.Distinct().ToArray();
    }

    public static int NormalizeFlag(int flagValue)
        => flagValue == 0 ? 0 : 1;

    public Task<ErpSimpleWriteResult> ActivateAsync(CpPluginsActivateRequest request, CancellationToken cancellationToken = default)
        => ActivateAsync(request, new Dictionary<string, string>(StringComparer.Ordinal), cancellationToken);

    public async Task<ErpSimpleWriteResult> ActivateAsync(CpPluginsActivateRequest request, IReadOnlyDictionary<string, string> phpConfig, CancellationToken cancellationToken = default)
    {
        var ids = ParsePluginIds(request.PluginsList, request.PluginId);
        if (ids.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one plugin id is required.");
        }

        var flag = NormalizeFlag(request.FlagValue);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "plugins", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "plugins table is not provisioned. Schema ensure stays on the Classic twin.");
            }

            foreach (var id in ids)
            {
                var lockFlag = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT IFNULL(`control_lock`, 0) FROM `plugins` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    id).ConfigureAwait(false);
                var exists = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `plugins` WHERE `id` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Plugin was not found.");
                }

                if (lockFlag != 0)
                {
                    return ErpSimpleWriteResult.Fail("locked", "Control of one of the selected plugins is locked.");
                }

                if (id == BackendTwoFactorPluginId && flag == 1
                    && !await TwoFactorChannelReadyAsync(connection, phpConfig, cancellationToken).ConfigureAwait(false))
                {
                    return ErpSimpleWriteResult.Fail(
                        "two_factor",
                        "This plugin cannot be activated until SMS or email notifications are configured and verified.");
                }
            }

            var placeholders = string.Join(" OR ", ids.Select((_, i) => "`id` = @p" + (i + 1).ToString(CultureInfo.InvariantCulture)));
            var sql = "UPDATE `plugins` SET `activated` = @p0 WHERE " + placeholders;
            var args = new object?[ids.Count + 1];
            args[0] = flag;
            for (var i = 0; i < ids.Count; i++)
            {
                args[i + 1] = ids[i];
            }

            var writes = await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken, args).ConfigureAwait(false);
            if (writes <= 0)
            {
                return ErpSimpleWriteResult.Fail("unchanged", "Plugin activation was not updated.");
            }

            return ErpSimpleWriteResult.Ok(
                flag == 1 ? "Plugin activated." : "Plugin deactivated.",
                ids[0]);
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
