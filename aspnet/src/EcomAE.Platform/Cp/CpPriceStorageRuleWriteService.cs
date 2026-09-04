using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_pm_storage_panel.php</c> twins for rule save (INSERT ON DUPLICATE KEY)
/// and DELETE. Brand/article normalize matches <c>epc_pricing_normalize_*</c>.
/// </summary>
public interface ICpPriceStorageRuleWriteService
{
    Task<ErpSimpleWriteResult> ApplyAsync(
        string? action,
        long ruleId,
        long storageId,
        string? manufacturer,
        string? article,
        string? marginPercent,
        int visible,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(string? kind, long ruleId, CancellationToken cancellationToken = default);
}

public sealed class CpPriceStorageRuleWriteService : ICpPriceStorageRuleWriteService
{
    private static readonly Regex ArticleKeep = new(
        @"[^a-zA-Z0-9А-Яа-яёЁ]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpPriceStorageRuleWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public Task<ErpSimpleWriteResult> ApplyAsync(
        string? action,
        long ruleId,
        long storageId,
        string? manufacturer,
        string? article,
        string? marginPercent,
        int visible,
        CancellationToken cancellationToken = default)
    {
        var kind = (action ?? string.Empty).Trim().ToLowerInvariant();
        return kind switch
        {
            "save_storage_rule" => SaveStorageAsync(storageId, marginPercent, visible, cancellationToken),
            "save_storage_brand_rule" => SaveBrandAsync(storageId, manufacturer, marginPercent, visible, cancellationToken),
            "save_storage_article_rule" => SaveArticleAsync(storageId, manufacturer, article, marginPercent, visible, cancellationToken),
            _ => DeleteAsync(kind, ruleId, cancellationToken)
        };
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        string? kind,
        long ruleId,
        CancellationToken cancellationToken = default)
    {
        if (ruleId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A rule id is required.");
        }

        if (!TryTable(kind, out var table, out var message))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown price-storage rule kind.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `" + table + "` WHERE `id` = ?"),
            cancellationToken,
            ruleId);
        return ErpSimpleWriteResult.Ok(message, ruleId);
    }

    internal static bool TryTable(string? kind, out string table, out string message)
    {
        switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "storage":
            case "delete_storage_rule":
                table = "epc_price_storage_rules";
                message = "Supplier overall rule deleted";
                return true;
            case "brand":
            case "delete_storage_brand_rule":
                table = "epc_price_storage_brand_rules";
                message = "Supplier brand rule deleted";
                return true;
            case "article":
            case "delete_storage_article_rule":
                table = "epc_price_storage_article_rules";
                message = "Supplier article rule deleted";
                return true;
            default:
                table = string.Empty;
                message = string.Empty;
                return false;
        }
    }

    /// <summary>PHP <c>epc_pricing_normalize_brand</c>: <c>mb_strtoupper(trim)</c>.</summary>
    internal static string NormalizeBrand(string? brand)
        => (brand ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>PHP <c>epc_pricing_normalize_article</c>: strip non-alnum (Latin+Cyrillic) then upper.</summary>
    internal static string NormalizeArticle(string? article)
        => ArticleKeep.Replace(article ?? string.Empty, string.Empty).ToUpperInvariant();

    /// <summary>PHP <c>(float) str_replace(',', '.')</c> then range −100…1000.</summary>
    internal static bool TryMargin(string? raw, out string formatted, out string? error)
    {
        formatted = string.Empty;
        error = null;
        var text = (raw ?? string.Empty).Trim().Replace(',', '.');
        if (text.Length == 0
            || !decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var margin))
        {
            error = "Margin must be from -100 to 1000";
            return false;
        }

        if (margin < -100m || margin > 1000m)
        {
            error = "Margin must be from -100 to 1000";
            return false;
        }

        formatted = margin.ToString("0.00", CultureInfo.InvariantCulture);
        return true;
    }

    private async Task<ErpSimpleWriteResult> SaveStorageAsync(
        long storageId,
        string? marginPercent,
        int visible,
        CancellationToken cancellationToken)
    {
        if (storageId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select a supplier / warehouse");
        }

        if (!TryMargin(marginPercent, out var margin, out var marginError))
        {
            return ErpSimpleWriteResult.Fail("invalid", marginError ?? "Supplier margin must be from -100 to 1000");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var flag = visible > 0 ? 1 : 0;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_price_storage_rules` (`storage_id`, `margin_percent`, `visible`, `updated_at`) "
                + "VALUES (?, ?, ?, ?) "
                + "ON DUPLICATE KEY UPDATE `margin_percent` = VALUES(`margin_percent`), `visible` = VALUES(`visible`), `updated_at` = VALUES(`updated_at`)"),
            cancellationToken,
            storageId, margin, flag, now);
        return ErpSimpleWriteResult.Ok("Supplier overall margin saved", storageId);
    }

    private async Task<ErpSimpleWriteResult> SaveBrandAsync(
        long storageId,
        string? manufacturer,
        string? marginPercent,
        int visible,
        CancellationToken cancellationToken)
    {
        var brand = NormalizeBrand(manufacturer);
        if (storageId <= 0 || brand.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select supplier and enter brand");
        }

        if (!TryMargin(marginPercent, out var margin, out var marginError))
        {
            return ErpSimpleWriteResult.Fail("invalid", marginError ?? "Margin must be from -100 to 1000");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var flag = visible > 0 ? 1 : 0;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_price_storage_brand_rules` "
                + "(`storage_id`, `manufacturer`, `margin_percent`, `visible`, `updated_at`) "
                + "VALUES (?, ?, ?, ?, ?) "
                + "ON DUPLICATE KEY UPDATE `margin_percent` = VALUES(`margin_percent`), `visible` = VALUES(`visible`), `updated_at` = VALUES(`updated_at`)"),
            cancellationToken,
            storageId, brand, margin, flag, now);
        return ErpSimpleWriteResult.Ok("Supplier brand rule saved", storageId);
    }

    private async Task<ErpSimpleWriteResult> SaveArticleAsync(
        long storageId,
        string? manufacturer,
        string? article,
        string? marginPercent,
        int visible,
        CancellationToken cancellationToken)
    {
        var brand = NormalizeBrand(manufacturer);
        var sku = NormalizeArticle(article);
        if (storageId <= 0 || brand.Length == 0 || sku.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select supplier, brand, and article");
        }

        if (!TryMargin(marginPercent, out var margin, out var marginError))
        {
            return ErpSimpleWriteResult.Fail("invalid", marginError ?? "Margin must be from -100 to 1000");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var flag = visible > 0 ? 1 : 0;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_price_storage_article_rules` "
                + "(`storage_id`, `manufacturer`, `article`, `margin_percent`, `visible`, `updated_at`) "
                + "VALUES (?, ?, ?, ?, ?, ?) "
                + "ON DUPLICATE KEY UPDATE `margin_percent` = VALUES(`margin_percent`), `visible` = VALUES(`visible`), `updated_at` = VALUES(`updated_at`)"),
            cancellationToken,
            storageId, brand, sku, margin, flag, now);
        return ErpSimpleWriteResult.Ok("Supplier article rule saved", storageId);
    }
}
