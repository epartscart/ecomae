using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_super_cp_info_blocks.php</c> twin of <c>epc_scp_info_block_save</c>.
/// Delete and schema-ensure stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpInfoBlocksWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpInfoBlockSaveRequest request,
        CancellationToken cancellationToken = default);
}

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
    int SortOrder);

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

                if (content.Length == 0)
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

            return ErpSimpleWriteResult.Fail("db", "Info-blocks table is missing — schema-ensure stays Classic.");
        }
    }
}
