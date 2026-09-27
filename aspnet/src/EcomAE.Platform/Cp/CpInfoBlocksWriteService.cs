using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_super_cp_info_blocks.php</c> twin of <c>epc_scp_info_block_save</c>
/// , <c>epc_scp_info_block_delete</c>, <c>epc_scp_info_blocks_list</c> and the info-blocks part of
/// <c>epc_scp_platform_ensure_schema</c>.
/// </summary>
public interface ICpInfoBlocksWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpInfoBlockSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<CpInfoBlocksList> ListAsync(
        string placement,
        CancellationToken cancellationToken = default);
}

public sealed record CpInfoBlockRow(
    long Id,
    string BlockKey,
    string Title,
    string Scope,
    string SiteKey,
    string Placement,
    string ContentHtml,
    string Locale,
    bool Active,
    int SortOrder);

public sealed record CpInfoBlocksList(IReadOnlyList<CpInfoBlockRow> Blocks, string Error);

public sealed record CpInfoBlockSaveRequest(
    long Id,
    string? BlockKey,
    string? Title,
    string? Scope,
    string? SiteKey,
    string? Placement,
    string? ContentHtml,
    string? Locale,
    bool Active,
    int SortOrder,
    bool KeepContentWhenBlank = true);

public sealed class CpInfoBlocksWriteService : ICpInfoBlocksWriteService
{
    private static readonly Regex BlockKeySafe = new("[^a-z0-9_-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, string> Placements = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["homepage"] = "Storefront homepage",
        ["footer"] = "Storefront footer",
        ["checkout"] = "Checkout sidebar",
        ["cp_notice"] = "CP dashboard notice",
        ["product_list"] = "Product listing banner",
        ["login"] = "Login / register page",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpInfoBlocksWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeBlockKey(string? raw)
        => BlockKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string NormalizeSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string NormalizeScope(string? raw)
    {
        var scope = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return scope is "platform" or "tenant" ? scope : "platform";
    }

    public static string NormalizePlacement(string? raw)
    {
        var placement = (raw ?? string.Empty).Trim();
        return Placements.ContainsKey(placement) ? placement : "homepage";
    }

    public static string NormalizeLocale(string? raw)
    {
        var locale = (raw ?? string.Empty).Trim();
        if (locale.Length > 8)
        {
            locale = locale[..8];
        }

        return locale.Length == 0 ? "en" : locale;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpInfoBlockSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeBlockKey(request.BlockKey);
        var title = (request.Title ?? string.Empty).Trim();
        if (key.Length == 0 || title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Block key and title are required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var scope = NormalizeScope(request.Scope);
        var siteKey = NormalizeSiteKey(request.SiteKey);
        var placement = NormalizePlacement(request.Placement);
        var locale = NormalizeLocale(request.Locale);
        var content = request.ContentHtml ?? string.Empty;
        var active = request.Active ? 1 : 0;
        var sort = request.SortOrder;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var existing = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_platform_info_blocks` WHERE `id`=? LIMIT 1"),
                    cancellationToken, request.Id).ConfigureAwait(false);
                if (existing <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Info block not found");
                }

                if (content.Length == 0 && request.KeepContentWhenBlank)
                {
                    content = await ErpDb.StringAsync(
                        connection, null,
                        "SELECT IFNULL(`content_html`, '') FROM `epc_platform_info_blocks` WHERE `id` = @p0 LIMIT 1",
                        cancellationToken, request.Id).ConfigureAwait(false) ?? string.Empty;
                }

                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_platform_info_blocks` SET `block_key`=?, `title`=?, `scope`=?, `site_key`=?, `placement`=?, `content_html`=?, `locale`=?, `active`=?, `sort_order`=?, `updated_at`=? WHERE `id`=?"),
                    cancellationToken, key, title, scope, siteKey, placement, content, locale, active, sort, now, request.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Info block saved.", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_platform_info_blocks` (`block_key`, `title`, `scope`, `site_key`, `placement`, `content_html`, `locale`, `active`, `sort_order`, `updated_at`, `created_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken, key, title, scope, siteKey, placement, content, locale, active, sort, now, now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Info block saved.", id);
        }
        catch (DbException ex)
        {
            var msg = ex.Message ?? string.Empty;
            if (msg.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Duplicate block key for this scope/locale");
            }

            return ErpSimpleWriteResult.Fail("db", "Platform database unavailable.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Info block id is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `epc_platform_info_blocks` WHERE `id`=?"),
                cancellationToken, id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Info block deleted.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Platform database unavailable.");
        }
    }

    public const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS `epc_platform_info_blocks` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
          `block_key` VARCHAR(64) NOT NULL,
          `title` VARCHAR(200) NOT NULL,
          `scope` VARCHAR(16) NOT NULL DEFAULT 'platform',
          `site_key` VARCHAR(64) NOT NULL DEFAULT '',
          `placement` VARCHAR(64) NOT NULL DEFAULT 'homepage',
          `content_html` MEDIUMTEXT NULL,
          `locale` VARCHAR(8) NOT NULL DEFAULT 'en',
          `active` TINYINT(1) NOT NULL DEFAULT 1,
          `sort_order` INT NOT NULL DEFAULT 0,
          `created_at` INT NOT NULL DEFAULT 0,
          `updated_at` INT NOT NULL DEFAULT 0,
          UNIQUE KEY `block_unique` (`block_key`, `scope`, `site_key`, `locale`),
          KEY `placement_active` (`placement`, `active`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """;

    /// <summary>PHP list order: filtered by placement → sort, title; otherwise placement, sort, title.</summary>
    public static string ListSql(bool filtered)
        => filtered
            ? "SELECT `id`, `block_key`, `title`, `scope`, `site_key`, `placement`, IFNULL(`content_html`,''), `locale`, `active`, `sort_order` FROM `epc_platform_info_blocks` WHERE `placement` = @p0 ORDER BY `sort_order` ASC, `title` ASC"
            : "SELECT `id`, `block_key`, `title`, `scope`, `site_key`, `placement`, IFNULL(`content_html`,''), `locale`, `active`, `sort_order` FROM `epc_platform_info_blocks` ORDER BY `placement` ASC, `sort_order` ASC, `title` ASC";

    public async Task<CpInfoBlocksList> ListAsync(
        string placement,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], "TenantRegistry DB is not configured.");
        }

        var filter = (placement ?? string.Empty).Trim();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ListSql(filter.Length > 0);
            if (filter.Length > 0)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@p0";
                parameter.Value = filter;
                command.Parameters.Add(parameter);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<CpInfoBlockRow>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpInfoBlockRow(
                    Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
                    Text(reader, 1),
                    Text(reader, 2),
                    Text(reader, 3),
                    Text(reader, 4),
                    Text(reader, 5),
                    Text(reader, 6),
                    Text(reader, 7),
                    !reader.IsDBNull(8) && Convert.ToInt64(reader.GetValue(8), System.Globalization.CultureInfo.InvariantCulture) != 0,
                    reader.IsDBNull(9) ? 0 : Convert.ToInt32(reader.GetValue(9), System.Globalization.CultureInfo.InvariantCulture)));
            }

            return new(rows, string.Empty);
        }
        catch (DbException)
        {
            return new([], "Platform database unavailable.");
        }
    }

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
        => await ErpDb.ExecuteAsync(connection, null, SchemaSql, cancellationToken).ConfigureAwait(false);
}
