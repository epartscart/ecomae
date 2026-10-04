using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>1-based file columns of a <c>shop_docpart_prices</c> list (0 = not mapped).</summary>
public sealed record PriceListColumnMap(
    int Manufacturer,
    int Article,
    int Name,
    int Exist,
    int Price,
    int TimeToExe = 0,
    int Storage = 0,
    int MinOrder = 0)
{
    public IReadOnlyDictionary<string, int> ByRole => new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["manufacturer"] = Manufacturer,
        ["article"] = Article,
        ["name"] = Name,
        ["exist"] = Exist,
        ["price"] = Price,
        ["time_to_exe"] = TimeToExe,
        ["storage"] = Storage,
        ["min_order"] = MinOrder,
    };
}

/// <summary>A normalized <c>shop_docpart_prices_data</c> row.</summary>
public sealed record PriceImportRow(
    string Manufacturer,
    string Article,
    string ArticleSearch,
    string ArticleShow,
    string Name,
    int Exist,
    decimal Price,
    int TimeToExe,
    string Storage,
    int MinOrder);

public sealed record PriceRowOutcome(PriceImportRow? Row, string? SkipReason, string Details, IReadOnlyDictionary<string, string> Parsed)
{
    public bool Imported => Row is not null;
}

/// <summary>
/// pyprices <c>PriceRecord</c>: Excel-style numeric articles (<c>123.0</c>) lose the fraction while leading zeros
/// survive, <c>article</c> keeps only <c>[a-zA-Z0-9а-яА-Я]</c>, <c>article_show</c> is the raw value, <c>exist</c> is
/// <c>int(float(digits/points/commas))</c> else 0, <c>price</c> is <c>price_parser</c>, and time/min order are <c>int()</c>
/// else 0. Unlike pyprices, rows without an article or without a positive price are reported as skipped instead of
/// being inserted, because the storefront could never show them.
/// </summary>
public static class PriceRecordNormalizer
{
    public const int TextColumnLength = 255;
    public const int ArticleSearchLength = 64;

    /// <summary><c>decimal(15,2)</c> ceiling of <c>shop_docpart_prices_data.price</c>.</summary>
    public const decimal MaxPrice = 9999999999999.99m;

    private static readonly Regex NonArticle = new("[^a-zA-Z0-9а-яА-Я]+", RegexOptions.CultureInvariant);
    private static readonly Regex NonExist = new("[^0-9,.]+", RegexOptions.CultureInvariant);
    private static readonly Regex PlainNumber = new(@"^\s*([+-]?)(\d+)(?:\.(\d*))?\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex PythonFloat = new(@"^[+-]?(\d+(\.\d*)?|\.\d+)$", RegexOptions.CultureInvariant);
    private static readonly Regex PythonInt = new(@"^[+-]?\d+(_\d+)*$", RegexOptions.CultureInvariant);

    public static PriceRowOutcome Normalize(IReadOnlyList<string> cells, PriceListColumnMap columns)
    {
        string Cell(int col) => col > 0 && col <= cells.Count ? cells[col - 1] ?? string.Empty : string.Empty;

        var manufacturerRaw = Cell(columns.Manufacturer);
        var articleRaw = Cell(columns.Article);
        var nameRaw = Cell(columns.Name);
        var existRaw = Cell(columns.Exist);
        var priceRaw = Cell(columns.Price);

        var parsed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["manufacturer"] = manufacturerRaw.Trim(),
            ["article_show"] = articleRaw.Trim(),
            ["name"] = nameRaw.Trim(),
            ["exist"] = existRaw,
            ["price"] = priceRaw,
        };

        if (cells.Count == 0 || cells.All(string.IsNullOrWhiteSpace))
        {
            return new PriceRowOutcome(null, "empty_row", "", parsed);
        }

        var (article, articleShow) = NormalizeArticle(articleRaw);
        parsed["article"] = article;
        if (article.Length == 0)
        {
            return new PriceRowOutcome(null, "empty_article", "Article column value: \"" + articleRaw.Trim() + "\"", parsed);
        }

        var price = PyPricesPriceParser.Parse(priceRaw);
        if (price is null || price <= 0 || price > MaxPrice)
        {
            return new PriceRowOutcome(null, "invalid_price", "Price column value: \"" + priceRaw + "\"", parsed);
        }

        var row = new PriceImportRow(
            Clip(manufacturerRaw.Trim(), TextColumnLength),
            Clip(article, TextColumnLength),
            Clip(DocpartArticle.NormalizeForPrice(article), ArticleSearchLength),
            Clip(articleShow.Trim(), TextColumnLength),
            Clip(nameRaw.Trim(), TextColumnLength),
            ParseExist(existRaw),
            price.Value,
            ParseInt(Cell(columns.TimeToExe)),
            Clip(Cell(columns.Storage).Trim(), TextColumnLength),
            ParseInt(Cell(columns.MinOrder)));
        return new PriceRowOutcome(row, null, "", parsed);
    }

    /// <returns>(<c>article</c>, <c>article_show</c>) as pyprices stores them.</returns>
    public static (string Article, string ArticleShow) NormalizeArticle(string raw)
    {
        raw ??= string.Empty;
        var number = PlainNumber.Match(raw);
        if (number.Success)
        {
            var sign = number.Groups[1].Value == "-" ? "-" : string.Empty;
            var integer = number.Groups[2].Value;
            var fraction = number.Groups[3].Value.TrimEnd('0');
            var show = sign + integer + (fraction.Length > 0 ? "." + fraction : string.Empty);
            return (NonArticle.Replace(show, string.Empty), show);
        }

        return (NonArticle.Replace(raw, string.Empty), raw);
    }

    /// <summary><c>int(float(re.sub('[^0-9,.]+', '', value)))</c>, 0 when Python would raise.</summary>
    public static int ParseExist(string? raw)
    {
        var cleaned = NonExist.Replace(raw ?? string.Empty, string.Empty);
        if (!PythonFloat.IsMatch(cleaned))
        {
            return 0;
        }

        if (!double.TryParse(cleaned, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        return value >= int.MaxValue ? int.MaxValue : (int)Math.Truncate(value);
    }

    /// <summary>Python <c>int(value)</c> (surrounding whitespace, sign and digit underscores allowed), 0 when it would raise.</summary>
    public static int ParseInt(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (!PythonInt.IsMatch(text))
        {
            return 0;
        }

        return long.TryParse(text.Replace("_", string.Empty, StringComparison.Ordinal), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? (int)Math.Clamp(value, int.MinValue, int.MaxValue)
            : 0;
    }

    private static string Clip(string value, int length) => value.Length <= length ? value : value[..length];
}

/// <summary>PHP <c>docpart_normalize_article_for_price</c> (sweep + upper), the value stored in <c>article_search</c>.</summary>
public static class DocpartArticle
{
    private static readonly string[] Sweep = ["\r\n", " ", "-", "_", "`", "/", "'", "\"", "\\", ".", ",", "#", "\r", "\n", "\t"];

    public static string NormalizeForPrice(string? article)
    {
        if (string.IsNullOrEmpty(article))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(article);
        foreach (var token in Sweep)
        {
            builder.Replace(token, string.Empty);
        }

        return builder.ToString().ToUpperInvariant();
    }
}
