using System.Net;
using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>prices_edit/ajax_operations.php</c> add/save/del/search-delete twins. Table list stays PHP.</summary>
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

    Task<ErpSimpleWriteResult> DeleteSearchAsync(
        long priceId,
        string? article,
        string? manufacturer,
        bool noArticle,
        bool noManufacturer,
        string? searchText,
        CancellationToken cancellationToken = default);
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

    public async Task<ErpSimpleWriteResult> DeleteSearchAsync(
        long priceId,
        string? article,
        string? manufacturer,
        bool noArticle,
        bool noManufacturer,
        string? searchText,
        CancellationToken cancellationToken = default)
    {
        var art = CleanArticle(article);
        var mfr = CleanBrand(manufacturer);
        var tokens = SearchTokens(searchText);
        if (priceId <= 0 && art.Length == 0 && mfr.Length == 0 && !noArticle && !noManufacturer && tokens.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A price list, article, manufacturer, empty-field flag, or search text is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var sql = new StringBuilder("DELETE FROM `shop_docpart_prices_data` WHERE 1=1");
        var args = new List<object?>();
        if (priceId > 0)
        {
            sql.Append(" AND `price_id` = ?");
            args.Add(priceId);
        }

        if (art.Length > 0)
        {
            sql.Append(" AND `article` LIKE ?");
            args.Add(art);
        }

        if (mfr.Length > 0)
        {
            sql.Append(" AND `manufacturer` LIKE ?");
            args.Add(mfr);
        }

        if (noArticle)
        {
            sql.Append(" AND `article` LIKE ?");
            args.Add(string.Empty);
        }

        if (noManufacturer)
        {
            sql.Append(" AND `manufacturer` LIKE ?");
            args.Add(string.Empty);
        }

        if (tokens.Count > 0)
        {
            sql.Append(" AND (");
            AppendTokenGroup(sql, args, "`article`", tokens);
            sql.Append(" OR ");
            AppendTokenGroup(sql, args, "`manufacturer`", tokens);
            sql.Append(" OR ");
            AppendTokenGroup(sql, args, "`name`", tokens);
            sql.Append(')');
        }

        sql.Append(" LIMIT 10000");
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(sql.ToString()),
            cancellationToken,
            args.ToArray()).ConfigureAwait(false);
        return new ErpSimpleWriteResult(
            true,
            "ok",
            "Search delete removed " + writes.ToString(System.Globalization.CultureInfo.InvariantCulture) + " price row(s).",
            writes,
            writes);
    }

    private static void AppendTokenGroup(StringBuilder sql, List<object?> args, string column, IReadOnlyList<string> tokens)
    {
        sql.Append('(');
        for (var i = 0; i < tokens.Count; i++)
        {
            if (i > 0)
            {
                sql.Append(" AND ");
            }

            sql.Append(column).Append(" LIKE ?");
            args.Add("%" + tokens[i] + "%");
        }

        sql.Append(')');
    }

    internal static IReadOnlyList<string> SearchTokens(string? searchText)
    {
        var raw = (searchText ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return [];
        }

        var tokens = new List<string>();
        foreach (var part in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length >= 3)
            {
                tokens.Add(part);
            }
        }

        return tokens;
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
