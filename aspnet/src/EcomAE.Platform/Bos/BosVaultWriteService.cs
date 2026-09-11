using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>document_vault</c> <c>new_version</c> / <c>epc_vault_new_version</c>
/// and <c>create_folder</c> / <c>epc_vault_create_folder</c>.
/// Upload, delete, restore, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosVaultWriteService
{
    Task<ErpSimpleWriteResult> NewVersionAsync(
        long documentId,
        string? versionData,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateFolderAsync(
        string? siteKey,
        string? name,
        long parentId,
        long createdBy,
        CancellationToken cancellationToken = default);
}

public sealed class BosVaultWriteService : IBosVaultWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosVaultWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>(int)</c> / <c>intval</c> on a token (leading optional sign + digits).</summary>
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

    /// <summary>PHP <c>json_decode((string)($_POST['version_data'] ?? '{}'), true) ?: array()</c>.</summary>
    public static (string FilePath, long FileSize, string Checksum, string ChangeNote, long UploadedBy) ParseVersionData(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ("", 0, "", "", 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ("", 0, "", "", 0);
            }

            var fileSize = doc.RootElement.TryGetProperty("file_size", out var sizeEl)
                           && sizeEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? PhpIntval(sizeEl)
                : 0;
            var uploadedBy = doc.RootElement.TryGetProperty("uploaded_by", out var userEl)
                             && userEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? PhpIntval(userEl)
                : 0;
            return (
                JsonString(doc.RootElement, "file_path"),
                fileSize,
                JsonString(doc.RootElement, "checksum"),
                JsonString(doc.RootElement, "change_note"),
                uploadedBy);
        }
        catch (JsonException)
        {
            return ("", 0, "", "", 0);
        }
    }

    public async Task<ErpSimpleWriteResult> NewVersionAsync(
        long documentId,
        string? versionData,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        var parsed = ParseVersionData(versionData);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var current = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `current_version` FROM `epc_vault_documents` WHERE `id`=?"),
                cancellationToken, documentId).ConfigureAwait(false);
            if (current == 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Document not found");
            }

            var newVer = current + 1;
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_vault_versions` (`document_id`,`version_number`,`file_path`,`file_size`,`checksum`,`change_note`,`uploaded_by`) VALUES (?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                documentId,
                newVer,
                parsed.FilePath,
                parsed.FileSize,
                parsed.Checksum,
                parsed.ChangeNote,
                parsed.UploadedBy).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_vault_documents` SET `current_version`=?, `file_size`=? WHERE `id`=?"),
                cancellationToken, newVer, parsed.FileSize, documentId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Vault document version added", newVer);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Vault table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> CreateFolderAsync(
        string? siteKey,
        string? name,
        long parentId,
        long createdBy,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        var folderName = name ?? "";
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var parentPath = "/";
            if (parentId > 0)
            {
                var path = await ErpDb.StringAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `path` FROM `epc_vault_folders` WHERE `id`=? AND `site_key`=?"),
                    cancellationToken, parentId, key).ConfigureAwait(false);
                var parentName = await ErpDb.StringAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `name` FROM `epc_vault_folders` WHERE `id`=? AND `site_key`=?"),
                    cancellationToken, parentId, key).ConfigureAwait(false);
                if (path is not null || parentName is not null)
                {
                    parentPath = BuildParentPath(path, parentName);
                }
            }

            object? parent = parentId == 0 ? null : parentId;
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    "INSERT INTO `epc_vault_folders` (`site_key`,`parent_id`,`name`,`path`,`created_by`) VALUES (?,?,?,?,?)"),
                cancellationToken, key, parent, folderName, parentPath, createdBy).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Vault folder created", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Vault folders table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP <c>rtrim($path, '/') . '/' . $name . '/'</c>.</summary>
    public static string BuildParentPath(string? path, string? name)
        => (path ?? "").TrimEnd('/') + "/" + (name ?? "") + "/";
}
