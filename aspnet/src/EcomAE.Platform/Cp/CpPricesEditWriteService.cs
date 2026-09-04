using System.Net;
using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>prices_edit/ajax_operations.php</c> add/save/del twins. Table list and del_search stay PHP.</summary>
public interface ICpPricesEditWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        long priceId,
        string? article,
        string? manufacturer,
        string? name,
        int exist,
        decimal price,
        int timeToExe,
        string? storage,
        int minOrder,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        long priceId,
        string? article,
        string? manufacturer,
        string? name,
        int exist,
        decimal price,
        int timeToExe,
        string? storage,
        int minOrder,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class CpPricesEditWriteService : ICpPricesEditWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPricesEditWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        long priceId,
        string? article,
        string? manufacturer,
        string? name,
        int exist,
        decimal price,
        int timeToExe,
        string? storage,
        int minOrder,
        CancellationToken cancellationToken = default)
    {
        var row = TryBuildRow(priceId, article, manufacturer, name, exist, price, timeToExe, storage, minOrder);
        if (row is null || priceId < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Article and manufacturer are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `shop_docpart_prices_data` (`price_id`, `manufacturer`, `article`, `article_show`, `name`, `exist`, `price`, `time_to_exe`, `storage`, `min_order`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            row.PriceId, row.Manufacturer, row.Article, row.ArticleShow, row.Name, row.Exist, row.Price, row.TimeToExe, row.Storage, row.MinOrder);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Price row added.", id);
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        long priceId,
        string? article,
        string? manufacturer,
        string? name,
        int exist,
        decimal price,
        int timeToExe,
        string? storage,
        int minOrder,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0 || priceId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A row id and price list id are required.");
        }

        var row = TryBuildRow(priceId, article, manufacturer, name, exist, price, timeToExe, storage, minOrder);
        if (row is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Article, manufacturer, and a price list id are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `shop_docpart_prices_data` SET `price_id`=?,`manufacturer`=?,`article`=?,`article_show`=?,`name`=?,`exist`=?,`price`=?,`time_to_exe`=?,`storage`=?,`min_order`=? WHERE `id` = ?"),
            cancellationToken,
            row.PriceId, row.Manufacturer, row.Article, row.ArticleShow, row.Name, row.Exist, row.Price, row.TimeToExe, row.Storage, row.MinOrder, id);
        return ErpSimpleWriteResult.Ok("Price row saved.", id);
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A price row id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_docpart_prices_data` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            id);
        return ErpSimpleWriteResult.Ok("Price row deleted.", id);
    }

    private static PriceRow? TryBuildRow(
        long priceId,
        string? article,
        string? manufacturer,
        string? name,
        int exist,
        decimal price,
        int timeToExe,
        string? storage,
        int minOrder)
    {
        var art = CleanArticle(article);
        var mfr = CleanBrand(manufacturer);
        if (art.Length == 0 || mfr.Length == 0)
        {
            return null;
        }

        if (exist < 0 || timeToExe < 0 || minOrder < 0 || price < 0)
        {
            return null;
        }

        var show = WebUtility.HtmlEncode(art);
        var cleanName = CleanName(name);
        var cleanStorage = WebUtility.HtmlEncode((storage ?? string.Empty).Trim());
        if (cleanStorage.Length > 255)
        {
            cleanStorage = cleanStorage[..255];
        }

        var money = decimal.Round(price, 2, MidpointRounding.AwayFromZero);
        return new PriceRow(priceId, mfr, art, show, cleanName, exist, money, timeToExe, cleanStorage, minOrder);
    }

    internal static string CleanArticle(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        var clean = builder.ToString().ToUpperInvariant();
        return clean.Length > 64 ? clean[..64] : clean;
    }

    internal static string CleanBrand(string? value)
    {
        var raw = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var encoded = WebUtility.HtmlEncode(raw);
        return encoded.Length > 255 ? encoded[..255] : encoded;
    }

    internal static string CleanName(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is '"' or '\\' or '\'' or '\n' or '\r' or '\t' or '\0' || char.IsControl(ch))
            {
                continue;
            }

            builder.Append(ch);
        }

        var encoded = WebUtility.HtmlEncode(builder.ToString().Trim());
        return encoded.Length > 255 ? encoded[..255] : encoded;
    }

    private sealed record PriceRow(
        long PriceId,
        string Manufacturer,
        string Article,
        string ArticleShow,
        string Name,
        int Exist,
        decimal Price,
        int TimeToExe,
        string Storage,
        int MinOrder);
}
