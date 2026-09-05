using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>crosses/ajax_operations.php</c> save/delete/add/search-delete twins.</summary>
public interface ICpCrossWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        string? article,
        string? manufacturerArticle,
        string? analog,
        string? manufacturerAnalog,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddAsync(
        string? article,
        string? manufacturerArticle,
        string? analog,
        string? manufacturerAnalog,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteSearchAsync(
        string? article,
        string? manufacturer,
        bool emptyOnly,
        long idFrom,
        long idBefore,
        CancellationToken cancellationToken = default);
}

public sealed class CpCrossWriteService : ICpCrossWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpCrossWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        string? article,
        string? manufacturerArticle,
        string? analog,
        string? manufacturerAnalog,
        CancellationToken cancellationToken = default)
    {
        var art = CleanArticle(article);
        var analogArt = CleanArticle(analog);
        var mfr = CleanBrand(manufacturerArticle);
        var analogMfr = CleanBrand(manufacturerAnalog);
        if (id <= 0 || art.Length == 0 || analogArt.Length == 0 || mfr.Length == 0 || analogMfr.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A cross id and all four article/brand fields are required.");
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
                "UPDATE `shop_docpart_articles_analogs_list` SET `article` = ?, `manufacturer_article` = ?, `analog` = ?, `manufacturer_analog` = ? WHERE `id` = ?"),
            cancellationToken,
            art, mfr, analogArt, analogMfr, id);
        return ErpSimpleWriteResult.Ok("Cross saved.", id);
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A cross id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_docpart_articles_analogs_list` WHERE `id` = ?"),
            cancellationToken,
            id);
        return ErpSimpleWriteResult.Ok("Cross deleted.", id);
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        string? article,
        string? manufacturerArticle,
        string? analog,
        string? manufacturerAnalog,
        CancellationToken cancellationToken = default)
    {
        var art = CleanArticle(article);
        var analogArt = CleanArticle(analog);
        if (art.Length == 0 || analogArt.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Article and analog are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var mfr = CleanBrandUpper(manufacturerArticle);
        var analogMfr = CleanBrandUpper(manufacturerAnalog);
        if (mfr.Length == 0)
        {
            mfr = await ResolveBrandAsync(connection, art, partnerBrand: analogMfr, fallbackBrand: null, cancellationToken)
                .ConfigureAwait(false);
        }

        if (analogMfr.Length == 0)
        {
            analogMfr = await ResolveBrandAsync(connection, analogArt, partnerBrand: mfr, fallbackBrand: mfr, cancellationToken)
                .ConfigureAwait(false);
        }

        if (mfr.Length == 0 || analogMfr.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Could not resolve both brands for this cross pair.");
        }

        if (string.Equals(art, analogArt, StringComparison.Ordinal) && string.Equals(mfr, analogMfr, StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A part cannot be crossed to itself.");
        }

        var writes = 0;
        writes += await PersistPairAsync(connection, art, mfr, analogArt, analogMfr, cancellationToken).ConfigureAwait(false);
        writes += await PersistPairAsync(connection, analogArt, analogMfr, art, mfr, cancellationToken).ConfigureAwait(false);
        if (writes <= 0)
        {
            return ErpSimpleWriteResult.Fail("already", "That cross pair is already stored.");
        }

        return new ErpSimpleWriteResult(true, "ok", "Cross added.", writes, writes);
    }

    public async Task<ErpSimpleWriteResult> DeleteSearchAsync(
        string? article,
        string? manufacturer,
        bool emptyOnly,
        long idFrom,
        long idBefore,
        CancellationToken cancellationToken = default)
    {
        var art = CleanArticle(article);
        var mfr = CleanBrandUpper(manufacturer);
        if (art.Length == 0 && !emptyOnly && idFrom <= 0 && idBefore <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A search article, empty-only flag, or id range is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sql = new StringBuilder("DELETE FROM `shop_docpart_articles_analogs_list` WHERE 1=1");
        var args = new List<object?>();
        if (art.Length > 0)
        {
            if (mfr.Length > 0)
            {
                sql.Append(" AND ((`article` = ? AND `manufacturer_article` = ?) OR (`analog` = ? AND `manufacturer_analog` = ?))");
                args.Add(art);
                args.Add(mfr);
                args.Add(art);
                args.Add(mfr);
            }
            else
            {
                sql.Append(" AND (`article` = ? OR `analog` = ?)");
                args.Add(art);
                args.Add(art);
            }
        }

        if (emptyOnly)
        {
            sql.Append(" AND (`article` = '' OR `manufacturer_article` = '' OR `analog` = '' OR `manufacturer_analog` = '')");
        }

        if (idFrom > 0)
        {
            sql.Append(" AND `id` >= ?");
            args.Add(idFrom);
        }

        if (idBefore > 0)
        {
            sql.Append(" AND `id` <= ?");
            args.Add(idBefore);
        }

        sql.Append(" LIMIT 50000");
        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(sql.ToString()),
            cancellationToken,
            args.ToArray()).ConfigureAwait(false);
        return new ErpSimpleWriteResult(
            true,
            "ok",
            "Search delete removed " + writes.ToString(System.Globalization.CultureInfo.InvariantCulture) + " pair(s).",
            writes,
            writes);
    }

    public static string InferBrandFromArticle(string? article)
    {
        var norm = CleanArticle(article);
        if (norm.StartsWith("90915", StringComparison.Ordinal))
        {
            return "TOYOTA";
        }

        if (norm.StartsWith("15400", StringComparison.Ordinal))
        {
            return "HONDA";
        }

        if (norm.StartsWith("15208", StringComparison.Ordinal) || norm.StartsWith("22040", StringComparison.Ordinal))
        {
            return "NISSAN";
        }

        if (norm.StartsWith("26300", StringComparison.Ordinal) || norm.StartsWith("28113", StringComparison.Ordinal))
        {
            return "HYUNDAI";
        }

        if ((norm.Length >= 3 && norm.StartsWith("06", StringComparison.Ordinal) && char.IsLetterOrDigit(norm[2]))
            || norm.StartsWith("1K0", StringComparison.Ordinal)
            || norm.StartsWith("5W0", StringComparison.Ordinal)
            || norm.StartsWith("8E0", StringComparison.Ordinal))
        {
            return "VAG";
        }

        if (norm.StartsWith("A000", StringComparison.Ordinal)
            || (norm.StartsWith('A') && norm.Length is >= 10 and <= 11 && norm.Skip(1).All(char.IsDigit)))
        {
            return "MERCEDES-BENZ";
        }

        if (norm.StartsWith("B6Y1", StringComparison.Ordinal)
            || norm.StartsWith("PE01", StringComparison.Ordinal)
            || norm.StartsWith("LF05", StringComparison.Ordinal))
        {
            return "MAZDA";
        }

        if (norm.StartsWith("12279", StringComparison.Ordinal)
            || norm.StartsWith("12280", StringComparison.Ordinal)
            || norm.StartsWith("12281", StringComparison.Ordinal)
            || norm.StartsWith("13101", StringComparison.Ordinal)
            || norm.StartsWith("13102", StringComparison.Ordinal)
            || norm.StartsWith("13103", StringComparison.Ordinal)
            || norm.StartsWith("13104", StringComparison.Ordinal)
            || norm.StartsWith("13105", StringComparison.Ordinal)
            || norm.StartsWith("13106", StringComparison.Ordinal))
        {
            return "TOYOTA";
        }

        if (norm.StartsWith("46256", StringComparison.Ordinal) || norm.StartsWith("45114", StringComparison.Ordinal))
        {
            return "TEIKIN";
        }

        return string.Empty;
    }

    internal static string CleanBrandUpper(string? value)
    {
        var clean = CleanBrand(value);
        return clean.Length == 0 ? string.Empty : clean.ToUpperInvariant();
    }

    private static async Task<string> ResolveBrandAsync(
        System.Data.Common.DbConnection connection,
        string article,
        string? partnerBrand,
        string? fallbackBrand,
        CancellationToken cancellationToken)
    {
        var fallback = CleanBrandUpper(fallbackBrand);
        if (fallback.Length > 0)
        {
            return fallback;
        }

        try
        {
            var fromPrice = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("""
                    SELECT UPPER(TRIM(`manufacturer`)) FROM `shop_docpart_prices_data`
                    WHERE REPLACE(REPLACE(REPLACE(UPPER(`article`),' ',''),'-',''),'.','') = ?
                      AND TRIM(`manufacturer`) <> ''
                    GROUP BY UPPER(TRIM(`manufacturer`))
                    ORDER BY COUNT(*) DESC LIMIT 1
                    """),
                cancellationToken,
                article);
            var priceBrand = CleanBrandUpper(fromPrice);
            if (priceBrand.Length > 0)
            {
                return priceBrand;
            }
        }
        catch (System.Data.Common.DbException)
        {
        }

        try
        {
            var fromCross = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("""
                    SELECT CASE
                        WHEN REPLACE(REPLACE(REPLACE(UPPER(`article`),' ',''),'-',''),'.','') = ? THEN `manufacturer_article`
                        ELSE `manufacturer_analog`
                    END
                    FROM `shop_docpart_articles_analogs_list`
                    WHERE REPLACE(REPLACE(REPLACE(UPPER(`article`),' ',''),'-',''),'.','') = ?
                       OR REPLACE(REPLACE(REPLACE(UPPER(`analog`),' ',''),'-',''),'.','') = ?
                    ORDER BY `id` DESC LIMIT 1
                    """),
                cancellationToken,
                article, article, article);
            var crossBrand = CleanBrandUpper(fromCross);
            if (crossBrand.Length > 0)
            {
                return crossBrand;
            }
        }
        catch (System.Data.Common.DbException)
        {
        }

        var partner = CleanBrandUpper(partnerBrand);
        if (partner.Length > 0)
        {
            return partner;
        }

        return InferBrandFromArticle(article);
    }

    private static async Task<int> PersistPairAsync(
        System.Data.Common.DbConnection connection,
        string article,
        string manufacturer,
        string analog,
        string analogManufacturer,
        CancellationToken cancellationToken)
    {
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("""
                SELECT `id` FROM `shop_docpart_articles_analogs_list`
                WHERE `article` = ? AND `manufacturer_article` = ? AND `analog` = ? AND `manufacturer_analog` = ?
                LIMIT 1
                """),
            cancellationToken,
            article, manufacturer, analog, analogManufacturer);
        if (exists > 0)
        {
            return 0;
        }

        try
        {
            return await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    INSERT INTO `shop_docpart_articles_analogs_list`
                    (`article`, `article_search`, `manufacturer_article`, `analog`, `analog_search`, `manufacturer_analog`)
                    VALUES (?,?,?,?,?,?)
                    """),
                cancellationToken,
                article, article, manufacturer, analog, analog, analogManufacturer);
        }
        catch (System.Data.Common.DbException)
        {
            return await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    INSERT INTO `shop_docpart_articles_analogs_list`
                    (`article`, `manufacturer_article`, `analog`, `manufacturer_analog`)
                    VALUES (?,?,?,?)
                    """),
                cancellationToken,
                article, manufacturer, analog, analogManufacturer);
        }
    }

    internal static string CleanArticle(string? value)
    {
        var raw = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is ' ' or '-' or '_' or '`' or '/' or '\'' or '"' or '\\' or '.' or ',' or '#'
                or '\r' or '\n' or '\t' or '\0' || char.IsControl(ch))
            {
                continue;
            }

            builder.Append(ch);
        }

        var clean = builder.ToString();
        return clean.Length > 255 ? clean[..255] : clean;
    }

    internal static string CleanBrand(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is '#' or '`' or '\'' or '"' or '\r' or '\n' or '\t' or '\0' || char.IsControl(ch))
            {
                continue;
            }

            builder.Append(ch);
        }

        var clean = builder.ToString().Trim();
        return clean.Length > 255 ? clean[..255] : clean;
    }
}
