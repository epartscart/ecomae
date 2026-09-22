using System.Data.Common;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpPackRow(long Id, string Caption, string Version, string Author, string Name, long TimeSetup);

public sealed record CpPackFile(string ServerPath, string FileName, string PackPath);

public sealed record CpPackComponent(long Id, string Caption);

public sealed record CpPackDetail(
    CpPackRow Pack,
    IReadOnlyList<CpPackFile> Files,
    IReadOnlyList<CpPackComponent> Templates,
    IReadOnlyList<CpPackComponent> ModulesPrototypes,
    IReadOnlyList<CpPackComponent> Plugins);

public sealed record CpPackInstallResult(bool Succeeded, string Code, string Message, long PackId, int Writes, CpPackDetail? Installed)
{
    public static CpPackInstallResult Fail(string code, string message) => new(false, code, message, 0, 0, null);
}

/// <summary>
/// PHP twin of cp/content/packs_control (packs_manager.php, pack_control.php, setup_page.php and its
/// ajax_prepare_setup / ajax_processing_files / ajax_insert_extensions / ajax_delete_pack / ajax_clear_tmp_folder steps).
/// </summary>
public interface ICpPacksService
{
    Task<CpPagedList<CpPackRow>> ListAsync(int page, CancellationToken cancellationToken = default);

    Task<CpPackDetail?> OpenAsync(long packId, CancellationToken cancellationToken = default);

    Task<CpPackInstallResult> InstallAsync(Stream zip, string docRoot, long adminId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long packId, string docRoot, CancellationToken cancellationToken = default);
}

public sealed class CpPacksService : ICpPacksService
{
    public const int PageLimit = CpTemplatesPluginsService.PageLimit;
    public const string BackendDir = "cp";
    public const string TmpRelative = "cp/tmp/pack_setup";
    public const long MaxZipBytes = 64 * 1024 * 1024;

    private readonly IErpWriteConnectionFactory _connections;

    public CpPacksService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpPagedList<CpPackRow>> ListAsync(int page, CancellationToken cancellationToken = default)
    {
        var rows = new List<CpPackRow>();
        if (!_connections.IsConfigured)
        {
            return new(rows, 0, 0, 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var total = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `packs` WHERE `removed` = 0", cancellationToken).ConfigureAwait(false);
            var pages = total == 0 ? 0 : (total + PageLimit - 1) / PageLimit;
            page = Math.Clamp(page, 0, Math.Max(0, pages - 1));

            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`caption`,''), IFNULL(`version`,''), IFNULL(`author`,''), IFNULL(`name`,''), IFNULL(`time_setup`,0) " +
                "FROM `packs` WHERE `removed` = 0 ORDER BY `id` LIMIT ? OFFSET ?");
            ErpDb.AddParameters(c, PageLimit, page * PageLimit);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(ReadRow(r));
            }

            return new(rows, total, page, pages);
        }
        catch (DbException)
        {
            return new(rows, 0, 0, 0);
        }
    }

    public async Task<CpPackDetail?> OpenAsync(long packId, CancellationToken cancellationToken = default)
    {
        if (packId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var (row, json) = await LoadAsync(connection, packId, cancellationToken).ConfigureAwait(false);
            return row is null ? null : Detail(row, json);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpPackInstallResult> InstallAsync(Stream zip, string docRoot, long adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPackInstallResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        docRoot = (docRoot ?? string.Empty).Trim();
        if (docRoot.Length == 0 || !Directory.Exists(docRoot))
        {
            return CpPackInstallResult.Fail("docroot", "PHP document root is not configured; pack files cannot be placed.");
        }

        var tmp = Path.GetFullPath(Path.Combine(docRoot, TmpRelative.Replace('/', Path.DirectorySeparatorChar)));
        ClearTmp(tmp);
        Directory.CreateDirectory(tmp);
        try
        {
            using (var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true))
            {
                foreach (var entry in archive.Entries)
                {
                    var rel = CpFileManagerService.NormalizeRelative(entry.FullName.Replace('\\', '/'));
                    if (rel is null)
                    {
                        return CpPackInstallResult.Fail("archive", "Archive contains an unsafe path: " + entry.FullName);
                    }

                    if (rel.Length == 0)
                    {
                        continue;
                    }

                    var target = Path.Combine(tmp, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (entry.FullName.EndsWith('/'))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await using var outStream = File.Create(target);
                    await using var inStream = entry.Open();
                    await inStream.CopyToAsync(outStream, cancellationToken).ConfigureAwait(false);
                }
            }

            var packJsonPath = Path.Combine(tmp, "pack.json");
            if (!File.Exists(packJsonPath))
            {
                return CpPackInstallResult.Fail("pack_json", "The archive does not contain pack.json.");
            }

            var packJsonText = await File.ReadAllTextAsync(packJsonPath, cancellationToken).ConfigureAwait(false);
            JsonObject pack;
            try
            {
                pack = JsonNode.Parse(packJsonText) as JsonObject ?? throw new JsonException("pack.json must be an object.");
            }
            catch (JsonException ex)
            {
                return CpPackInstallResult.Fail("pack_json", "pack.json is not valid: " + ex.Message);
            }

            var name = Str(pack, "name");
            var caption = Str(pack, "caption");
            var version = Str(pack, "version");
            if (caption.Length == 0) return CpPackInstallResult.Fail("invalid", "pack.json: caption is required.");
            if (name.Length == 0) return CpPackInstallResult.Fail("invalid", "pack.json: name is required.");
            if (version.Length == 0) return CpPackInstallResult.Fail("invalid", "pack.json: version is required.");

            var files = Arr(pack, "files");
            foreach (var f in files)
            {
                var packPath = Str(f, "pack_path");
                var fileName = Str(f, "file_name");
                var serverPath = Str(f, "server_path");
                if (fileName.Length == 0 || fileName != Path.GetFileName(fileName) || !SafeServerPath(serverPath) || CpFileManagerService.NormalizeRelative(packPath) is null)
                {
                    return CpPackInstallResult.Fail("files", "pack.json: unsafe file entry " + fileName);
                }

                if (!File.Exists(Path.Combine(tmp, (CpFileManagerService.NormalizeRelative(packPath) ?? "").Replace('/', Path.DirectorySeparatorChar), fileName)))
                {
                    return CpPackInstallResult.Fail("files", "File listed in pack.json is missing from the archive: " + fileName);
                }

                if (File.Exists(DestinationPath(docRoot, serverPath, fileName)))
                {
                    return CpPackInstallResult.Fail("files", "File already exists on the server and would be overwritten: " + fileName);
                }
            }

            foreach (var m in Arr(pack, "modules_prototypes"))
            {
                if (!HasBool(m, "is_frontend")) return CpPackInstallResult.Fail("modules", "Module prototype without is_frontend.");
                if (!m.ContainsKey("prototype_name")) return CpPackInstallResult.Fail("modules", "Module prototype without prototype_name.");
                var ct = Str(m, "content_type");
                if (ct is not ("php" or "text")) return CpPackInstallResult.Fail("modules", "Module prototype " + Str(m, "prototype_name") + ": content_type must be php or text.");
                if (!m.ContainsKey("content")) return CpPackInstallResult.Fail("modules", "Module prototype without content.");
            }

            foreach (var p in Arr(pack, "plugins"))
            {
                if (!HasBool(p, "is_frontend")) return CpPackInstallResult.Fail("plugins", "Plugin without is_frontend.");
                if (!p.ContainsKey("caption")) return CpPackInstallResult.Fail("plugins", "Plugin without caption.");
                if (!p.ContainsKey("source")) return CpPackInstallResult.Fail("plugins", "Plugin without source.");
            }

            foreach (var t in Arr(pack, "templates"))
            {
                if (!HasBool(t, "is_frontend")) return CpPackInstallResult.Fail("templates", "Template without is_frontend.");
                if (!t.ContainsKey("name")) return CpPackInstallResult.Fail("templates", "Template without name.");
                if (!t.ContainsKey("caption")) return CpPackInstallResult.Fail("templates", "Template without caption.");
                if (!t.ContainsKey("positions")) return CpPackInstallResult.Fail("templates", "Template without positions.");
            }

            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var same = await ErpDb.StringAsync(connection, null,
                ErpDb.Positional("SELECT IFNULL(`version`,'') FROM `packs` WHERE `name` = ? AND `removed` = 0 LIMIT 1"), cancellationToken, name).ConfigureAwait(false);
            if (same is not null)
            {
                var cmp = CompareVersions(version, same);
                var hint = cmp > 0 ? "a newer version" : cmp < 0 ? "an older version" : "the same version";
                return CpPackInstallResult.Fail("exists", "A pack with technical name '" + name + "' is already installed (v" + same + "); you are installing " + hint + ". Delete it first.");
            }

            var writes = 0;
            await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                writes += await ErpDb.ExecuteAsync(connection, tx,
                    ErpDb.Positional("INSERT INTO `packs` (`name`, `caption`, `author`, `version`, `time_setup`, `admin_id`, `pack_json`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
                    cancellationToken, name, caption, Str(pack, "author"), version, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminId, packJsonText).ConfigureAwait(false);
                var packId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);

                foreach (var f in files)
                {
                    var src = Path.Combine(tmp, (CpFileManagerService.NormalizeRelative(Str(f, "pack_path")) ?? "").Replace('/', Path.DirectorySeparatorChar), Str(f, "file_name"));
                    var dst = DestinationPath(docRoot, Str(f, "server_path"), Str(f, "file_name"));
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Copy(src, dst, overwrite: false);
                }

                foreach (var t in Arr(pack, "templates"))
                {
                    writes += await ErpDb.ExecuteAsync(connection, tx,
                        ErpDb.Positional("INSERT INTO `templates` (`is_frontend`, `name`, `caption`, `positions`, `phone_support`, `tablet_support`, `data_structure`, `data_value`, `current`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, 0)"),
                        cancellationToken, Int(t, "is_frontend"), Str(t, "name"), Str(t, "caption"), Str(t, "positions"), Int(t, "phone_support"), Int(t, "tablet_support"), Str(t, "data_structure"), Str(t, "data_value")).ConfigureAwait(false);
                    t["id"] = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                }

                foreach (var p in Arr(pack, "plugins"))
                {
                    writes += await ErpDb.ExecuteAsync(connection, tx,
                        ErpDb.Positional("INSERT INTO `plugins` (`is_frontend`, `caption`, `source`, `description`, `activated`, `data_structure`, `data_value`, `control_lock`) VALUES (?, ?, ?, ?, ?, ?, ?, ?)"),
                        cancellationToken, Int(p, "is_frontend"), Str(p, "caption"), Str(p, "source"), Str(p, "description"), Int(p, "activated"), Str(p, "data_structure"), Str(p, "data_value"), Int(p, "control_lock")).ConfigureAwait(false);
                    p["id"] = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                }

                foreach (var m in Arr(pack, "modules_prototypes"))
                {
                    writes += await ErpDb.ExecuteAsync(connection, tx,
                        ErpDb.Positional("INSERT INTO `modules` (`is_frontend`, `is_prototype`, `prototype_name`, `content_type`, `content`, `data`, `css_js`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
                        cancellationToken, Int(m, "is_frontend"), Int(m, "is_prototype"), Str(m, "prototype_name"), Str(m, "content_type"), Str(m, "content"), Str(m, "data"), Str(m, "css_js")).ConfigureAwait(false);
                    m["id"] = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                    if (m["caption"] is null)
                    {
                        m["caption"] = Str(m, "prototype_name");
                    }
                }

                var finalJson = pack.ToJsonString();
                writes += await ErpDb.ExecuteAsync(connection, tx,
                    ErpDb.Positional("UPDATE `packs` SET `pack_json` = ? WHERE `id` = ?"), cancellationToken, finalJson, packId).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

                var row = new CpPackRow(packId, caption, version, Str(pack, "author"), name, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                return new CpPackInstallResult(true, "ok", "Pack '" + caption + "' v" + version + " installed.", packId, writes, Detail(row, finalJson));
            }
            catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return CpPackInstallResult.Fail("db", ex.Message);
            }
        }
        catch (InvalidDataException ex)
        {
            return CpPackInstallResult.Fail("archive", "Archive could not be read: " + ex.Message);
        }
        catch (DbException ex)
        {
            return CpPackInstallResult.Fail("db", ex.Message);
        }
        finally
        {
            ClearTmp(tmp);
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long packId, string docRoot, CancellationToken cancellationToken = default)
    {
        if (packId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "pack_id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        docRoot = (docRoot ?? string.Empty).Trim();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var (row, json) = await LoadAsync(connection, packId, cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Pack was not found.");
            }

            var pack = ParseJson(json);
            var templates = Arr(pack, "templates");
            foreach (var t in templates)
            {
                var current = await ErpDb.ScalarAsync(connection, null,
                    ErpDb.Positional("SELECT IFNULL(`current`,0) FROM `templates` WHERE `id` = ? LIMIT 1"), cancellationToken, Long(t, "id")).ConfigureAwait(false);
                if (current is not null && Convert.ToInt32(current, CultureInfo.InvariantCulture) == 1)
                {
                    return ErpSimpleWriteResult.Fail("current", "The pack contains the current template and cannot be deleted.");
                }
            }

            var writes = 0;
            await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var t in templates)
                {
                    var id = Long(t, "id");
                    await using var c = connection.CreateCommand();
                    c.Transaction = tx;
                    c.CommandText = ErpDb.Positional("SELECT IFNULL(`name`,''), IFNULL(`is_frontend`,1) FROM `templates` WHERE `id` = ? LIMIT 1");
                    ErpDb.AddParameters(c, id);
                    string? dir = null;
                    await using (var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (await r.ReadAsync(cancellationToken).ConfigureAwait(false) && docRoot.Length > 0)
                        {
                            dir = CpTemplatesWriteService.TemplateDir(docRoot, r.GetString(0), Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture));
                        }
                    }

                    RemoveDir(dir);
                    writes += await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `templates` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false);
                }

                foreach (var p in Arr(pack, "plugins"))
                {
                    var id = Long(p, "id");
                    var dirsFiles = await ErpDb.StringAsync(connection, tx,
                        ErpDb.Positional("SELECT IFNULL(`dirs_files`,'') FROM `plugins` WHERE `id` = ? LIMIT 1"), cancellationToken, id).ConfigureAwait(false);
                    if (docRoot.Length > 0 && !string.IsNullOrWhiteSpace(dirsFiles))
                    {
                        JsonNode? parsed = null;
                        try { parsed = JsonNode.Parse(dirsFiles); } catch (JsonException) { }
                        if (parsed is JsonArray items)
                        {
                            foreach (var item in items.OfType<JsonObject>())
                            {
                                var rel = Str(item, "path").Replace("<backend_dir>", BackendDir, StringComparison.Ordinal);
                                var full = Inside(docRoot, rel);
                                if (full is null) continue;
                                if (Str(item, "type") == "dir") RemoveDir(full);
                                else if (Str(item, "type") == "file") RemoveFile(full);
                            }
                        }
                    }

                    writes += await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `plugins` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false);
                }

                foreach (var m in Arr(pack, "modules_prototypes"))
                {
                    var id = Long(m, "id");
                    writes += await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `modules` WHERE `prototype_id` = ?"), cancellationToken, id).ConfigureAwait(false);
                    writes += await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `modules` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false);
                }

                if (docRoot.Length > 0)
                {
                    foreach (var f in Arr(pack, "files"))
                    {
                        var fileName = Str(f, "file_name");
                        if (fileName.Length == 0 || fileName != Path.GetFileName(fileName)) continue;
                        var full = Inside(docRoot, Str(f, "server_path").Replace("<backend_dir>", BackendDir, StringComparison.Ordinal).TrimEnd('/') + "/" + fileName);
                        RemoveFile(full);
                    }
                }

                writes += await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("UPDATE `packs` SET `removed` = 1 WHERE `id` = ?"), cancellationToken, packId).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new ErpSimpleWriteResult(true, "ok", "Pack '" + row.Caption + "' deleted.", packId, writes);
            }
            catch (DbException ex)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("db", ex.Message);
            }
        }
        catch (DbException ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    // ---- helpers ----

    /// <summary>PHP: strip dots, right-pad the shorter string with zeros, compare numerically.</summary>
    public static int CompareVersions(string a, string b)
    {
        var x = new string((a ?? "").Where(char.IsDigit).ToArray());
        var y = new string((b ?? "").Where(char.IsDigit).ToArray());
        var len = Math.Max(x.Length, y.Length);
        x = x.PadRight(len, '0');
        y = y.PadRight(len, '0');
        return string.CompareOrdinal(x, y);
    }

    /// <summary>server_path must be an absolute docroot-relative folder ("/content/files/") with no traversal.</summary>
    public static bool SafeServerPath(string serverPath)
    {
        if (string.IsNullOrEmpty(serverPath) || !serverPath.StartsWith('/'))
        {
            return false;
        }

        var rel = serverPath.Replace("<backend_dir>", BackendDir, StringComparison.Ordinal);
        return CpFileManagerService.NormalizeRelative(rel) is not null && !rel.Contains('\0');
    }

    public static string DestinationPath(string docRoot, string serverPath, string fileName)
    {
        var rel = (CpFileManagerService.NormalizeRelative(serverPath.Replace("<backend_dir>", BackendDir, StringComparison.Ordinal)) ?? "").Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(docRoot, rel, fileName));
    }

    private static string? Inside(string docRoot, string relative)
    {
        var rel = CpFileManagerService.NormalizeRelative(relative);
        if (rel is null || rel.Length == 0)
        {
            return null;
        }

        var root = Path.GetFullPath(docRoot);
        var full = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    private static void RemoveDir(string? dir)
    {
        if (dir is null) return;
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void RemoveFile(string? file)
    {
        if (file is null) return;
        try
        {
            if (File.Exists(file)) File.Delete(file);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void ClearTmp(string tmp) => RemoveDir(tmp);

    private static async Task<(CpPackRow? Row, string Json)> LoadAsync(DbConnection connection, long packId, CancellationToken cancellationToken)
    {
        await using var c = connection.CreateCommand();
        c.CommandText = ErpDb.Positional(
            "SELECT `id`, IFNULL(`caption`,''), IFNULL(`version`,''), IFNULL(`author`,''), IFNULL(`name`,''), IFNULL(`time_setup`,0), IFNULL(`pack_json`,'') " +
            "FROM `packs` WHERE `id` = ? LIMIT 1");
        ErpDb.AddParameters(c, packId);
        await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (null, "");
        }

        return (ReadRow(r), r.GetString(6));
    }

    private static CpPackRow ReadRow(DbDataReader r) => new(
        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
        r.GetString(1),
        r.GetString(2),
        r.GetString(3),
        r.GetString(4),
        Convert.ToInt64(r.GetValue(5), CultureInfo.InvariantCulture));

    public static CpPackDetail Detail(CpPackRow row, string json)
    {
        var pack = ParseJson(json);
        return new CpPackDetail(
            row,
            Arr(pack, "files").Select(f => new CpPackFile(Str(f, "server_path"), Str(f, "file_name"), Str(f, "pack_path"))).ToList(),
            Components(pack, "templates", "caption"),
            Components(pack, "modules_prototypes", "caption", "prototype_name"),
            Components(pack, "plugins", "caption"));
    }

    private static List<CpPackComponent> Components(JsonObject pack, string key, string captionKey, string? fallbackKey = null)
        => Arr(pack, key).Select(o =>
        {
            var caption = Str(o, captionKey);
            if (caption.Length == 0 && fallbackKey is not null) caption = Str(o, fallbackKey);
            return new CpPackComponent(Long(o, "id"), caption);
        }).ToList();

    private static JsonObject ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new JsonObject();
        try
        {
            return JsonNode.Parse(json.Replace("\\\\", "\\", StringComparison.Ordinal)) as JsonObject
                   ?? JsonNode.Parse(json) as JsonObject
                   ?? new JsonObject();
        }
        catch (JsonException)
        {
            try { return JsonNode.Parse(json) as JsonObject ?? new JsonObject(); }
            catch (JsonException) { return new JsonObject(); }
        }
    }

    private static List<JsonObject> Arr(JsonObject o, string key)
        => o[key] is JsonArray a ? a.OfType<JsonObject>().ToList() : [];

    private static string Str(JsonObject o, string key)
    {
        var n = o[key];
        if (n is null) return "";
        if (n is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s;
            if (v.TryGetValue<long>(out var l)) return l.ToString(CultureInfo.InvariantCulture);
            if (v.TryGetValue<bool>(out var b)) return b ? "1" : "0";
            if (v.TryGetValue<double>(out var d)) return d.ToString(CultureInfo.InvariantCulture);
        }

        return n.ToJsonString();
    }

    private static long Long(JsonObject o, string key)
        => long.TryParse(Str(o, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static int Int(JsonObject o, string key)
    {
        var s = Str(o, key);
        if (s.Equals("true", StringComparison.OrdinalIgnoreCase)) return 1;
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? (v != 0 ? 1 : 0) : 0;
    }

    private static bool HasBool(JsonObject o, string key)
        => o.ContainsKey(key) && Str(o, key) is "0" or "1" or "true" or "false" or "";
}
