using System.Data.Common;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>
/// Native twin of the deploy / automation endpoint <c>epc-upload-uae-prices.php</c> (<c>list_prices</c>,
/// <c>list_latest_uploads</c>, <c>upload</c>, <c>reupload_latest</c>). Lists are resolved or created by
/// <c>epc_price_resolve_or_create_list</c> and linked to the same-named warehouse by
/// <c>epc_price_link_storage_to_list</c>; the import itself is <see cref="ICpPriceImportService"/>.
/// </summary>
public interface ICpPriceDeployApiService
{
    Task<IReadOnlyList<Dictionary<string, object?>>> ListPricesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Dictionary<string, object?>>> ListLatestUploadsAsync(CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> UploadAsync(long priceId, string? priceName, CpPriceUpload upload, CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> ReuploadLatestAsync(long priceId, string? priceName, CancellationToken cancellationToken = default);
}

public sealed class CpPriceDeployApiService : ICpPriceDeployApiService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpPriceImportService _imports;
    private readonly string _filesRoot;

    public CpPriceDeployApiService(IErpWriteConnectionFactory connections, ICpPriceImportService imports, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        : this(connections, imports, Path.Combine(Presentation.PhpLegacyAssetBridge.FindRepoRoot(env), "content", "files"))
    {
    }

    public CpPriceDeployApiService(IErpWriteConnectionFactory connections, ICpPriceImportService imports, string filesRoot)
    {
        _connections = connections;
        _imports = imports;
        _filesRoot = filesRoot;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ListPricesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `name`, `last_updated`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = `shop_docpart_prices`.`id`) AS `records_count` FROM `shop_docpart_prices` ORDER BY `id`";
        try
        {
        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                ["name"] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                ["last_updated"] = reader.IsDBNull(2) ? 0L : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                ["records_count"] = Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
            });
        }

        return rows;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ListLatestUploadsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await CpPriceUploadHistory.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var uploads = new List<Dictionary<string, object?>>();
        foreach (var row in await CpPriceUploadHistory.LatestPerListAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            uploads.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["price_id"] = Long(row, "price_id"),
                ["history_id"] = Long(row, "id"),
                ["price_name"] = CpPriceUploadHistory.Text(row, "price_name"),
                ["original_filename"] = CpPriceUploadHistory.Text(row, "original_filename"),
                ["stored_relpath"] = CpPriceUploadHistory.Text(row, "stored_relpath"),
                ["file_size"] = Long(row, "file_size"),
                ["created_at"] = row.TryGetValue("created_at", out var created) && created is DateTime at
                    ? at.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    : CpPriceUploadHistory.Text(row, "created_at"),
                ["status"] = CpPriceUploadHistory.Text(row, "status"),
                ["rows_imported"] = Long(row, "rows_imported"),
                ["is_active"] = Long(row, "is_active"),
            });
        }

        return uploads;
    }

    public async Task<Dictionary<string, object?>> UploadAsync(long priceId, string? priceName, CpPriceUpload upload, CancellationToken cancellationToken = default)
    {
        var extension = PriceFileReader.ExtensionOf(upload.FileName);
        if (!CpPriceImportService.PlainUploadExtensions.Contains(extension))
        {
            return Failure("Unsupported file type");
        }

        long resolvedId;
        string resolvedName;
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var list = await ResolveOrCreateListAsync(connection, priceId, priceName ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (list is null)
            {
                return Failure("Price list not found and could not be created");
            }

            (resolvedId, resolvedName) = list.Value;
            await LinkStorageToListAsync(connection, resolvedName, resolvedId, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_docpart_prices` SET `file_name_substring` = ? WHERE `id` = ?"),
                cancellationToken,
                resolvedName,
                resolvedId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return Failure("Price lists are not in this database.");
        }

        var result = await _imports.ImportUploadAsync(new CpPriceImportRequest(resolvedId, "api", 0, upload), cancellationToken).ConfigureAwait(false);
        var payload = result.ToPayload();
        payload["price_id"] = resolvedId;
        payload["price_name"] = resolvedName;
        payload["items_count"] = result.RowsInDb;
        payload["file_name_substring"] = resolvedName;
        return payload;
    }

    public async Task<Dictionary<string, object?>> ReuploadLatestAsync(long priceId, string? priceName, CancellationToken cancellationToken = default)
    {
        var name = (priceName ?? string.Empty).Trim();
        if (priceId <= 0 && name.Length == 0)
        {
            return Failure("price_id or price_name required");
        }

        long resolvedId;
        string resolvedName;
        string sourcePath;
        string originalName;
        long sourceHistoryId;
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CpPriceUploadHistory.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var list = await ResolveOrCreateListAsync(connection, priceId, name, cancellationToken).ConfigureAwait(false);
            if (list is null)
            {
                return Failure("Price list not found");
            }

            (resolvedId, resolvedName) = list.Value;
            var row = await CpPriceUploadHistory.GetActiveAsync(connection, _filesRoot, resolvedId, cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                return Failure("No downloadable upload in CP history for this price list");
            }

            sourcePath = CpPriceUploadHistory.AbsolutePath(_filesRoot, CpPriceUploadHistory.Text(row, "stored_relpath"));
            sourceHistoryId = Long(row, "id");
            originalName = CpPriceUploadHistory.Text(row, "original_filename");
            if (originalName.Length == 0 || PriceFileReader.ExtensionOf(originalName) == "noext")
            {
                originalName = Path.GetFileName(sourcePath);
            }

            await LinkStorageToListAsync(connection, resolvedName, resolvedId, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return Failure("Price lists are not in this database.");
        }

        await using var content = File.OpenRead(sourcePath);
        var result = await _imports.ImportUploadAsync(
            new CpPriceImportRequest(
                resolvedId,
                "api_reupload",
                0,
                new CpPriceUpload(originalName, content),
                "from_history_" + sourceHistoryId.ToString(CultureInfo.InvariantCulture),
                new Dictionary<string, object?> { ["reupload_from_history_id"] = sourceHistoryId }),
            cancellationToken).ConfigureAwait(false);
        var payload = result.ToPayload();
        payload["action"] = "reupload_latest";
        payload["price_id"] = resolvedId;
        payload["price_name"] = resolvedName;
        payload["source_history_id"] = sourceHistoryId;
        payload["source_filename"] = originalName;
        return payload;
    }

    /// <summary>PHP <c>epc_price_resolve_or_create_list</c> (new lists get the deploy layout 1,2,3,4,5 + time col 7, UTF-8, comma).</summary>
    public static async Task<(long Id, string Name)?> ResolveOrCreateListAsync(DbConnection connection, long priceId, string priceName, CancellationToken cancellationToken)
    {
        priceName = priceName.Trim();
        if (priceId > 0)
        {
            var byId = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1"), cancellationToken, priceId).ConfigureAwait(false);
            if (byId is not null)
            {
                return (priceId, byId);
            }
        }

        if (priceName.Length == 0)
        {
            return null;
        }

        foreach (var sql in new[]
                 {
                     "SELECT `id` FROM `shop_docpart_prices` WHERE `name` = ? LIMIT 1",
                     "SELECT `id` FROM `shop_docpart_prices` WHERE UPPER(`name`) = UPPER(?) LIMIT 1",
                 })
        {
            var id = await ErpDb.LongAsync(connection, null, ErpDb.Positional(sql), cancellationToken, priceName).ConfigureAwait(false);
            if (id > 0)
            {
                var name = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1"), cancellationToken, id).ConfigureAwait(false);
                return (id, name ?? priceName);
            }
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `shop_docpart_prices` (`name`,`load_mode`,`strings_to_left`,`manufacturer_col`,`article_col`,`name_col`,`exist_col`,`price_col`,`time_to_exe_col`,`storage_col`,`min_order_col`,`clean_before`,`file_name_substring`,`encoding`,`separator`,`h_time`)"
                + " VALUES (?, 1, 1, 1, 2, 3, 4, 5, 7, 0, 0, 1, ?, ?, ?, ?)"),
            cancellationToken,
            priceName,
            priceName,
            "utf-8",
            ",",
            "0").ConfigureAwait(false);
        var newId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return newId > 0 ? (newId, priceName) : null;
    }

    /// <summary>PHP <c>epc_price_link_storage_to_list</c>: <c>connection_options.price_id</c> of the same-named warehouse.</summary>
    public static async Task LinkStorageToListAsync(DbConnection connection, string storageName, long priceId, CancellationToken cancellationToken)
    {
        if (storageName.Length == 0 || priceId <= 0)
        {
            return;
        }

        long storageId;
        string raw;
        try
        {
            await using var select = connection.CreateCommand();
            select.CommandText = ErpDb.Positional("SELECT `id`, `connection_options` FROM `shop_storages` WHERE UPPER(`name`) = UPPER(?) LIMIT 1");
            ErpDb.AddParameters(select, storageName);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            storageId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            raw = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return;
        }

        JsonObject options;
        try
        {
            options = raw.Trim().Length > 0 ? JsonNode.Parse(raw) as JsonObject ?? new JsonObject() : new JsonObject();
        }
        catch (JsonException)
        {
            options = new JsonObject();
        }

        options["price_id"] = priceId.ToString(CultureInfo.InvariantCulture);
        options.TryAdd("probability", "100");
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_storages` SET `connection_options` = ? WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            options.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            storageId).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> Failure(string message) => new(StringComparer.Ordinal)
    {
        ["status"] = false,
        ["message"] = message,
    };

    private static long Long(IReadOnlyDictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is not null && long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;
}
