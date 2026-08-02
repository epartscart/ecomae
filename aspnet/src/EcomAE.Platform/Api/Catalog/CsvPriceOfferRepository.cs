namespace EcomAE.Platform.Api.Catalog;

public sealed class CsvPriceOfferRepository : IPriceOfferRepository
{
    private readonly string _csvPath;

    public CsvPriceOfferRepository(string csvPath)
    {
        _csvPath = csvPath;
    }

    public async Task<IReadOnlyCollection<PriceOfferRow>> FindOffersAsync(string normalizedBrand, string normalizedArticle, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_csvPath) || !File.Exists(_csvPath))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(_csvPath, cancellationToken);
        if (lines.Length < 2)
        {
            return [];
        }

        var headers = SplitCsvLine(lines[0]);
        var rows = new List<PriceOfferRow>();
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = SplitCsvLine(line);
            var brand = Get(cells, headers, "brand").Trim().ToUpperInvariant();
            var article = PriceLookupRequest.NormalizeArticle(Get(cells, headers, "article"));
            var articleShow = PriceLookupRequest.NormalizeArticle(Get(cells, headers, "article_show"));
            if (!string.Equals(brand, normalizedBrand, StringComparison.Ordinal)
                || (!string.Equals(article, normalizedArticle, StringComparison.Ordinal) && !string.Equals(articleShow, normalizedArticle, StringComparison.Ordinal)))
            {
                continue;
            }

            if (!decimal.TryParse(Get(cells, headers, "price"), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var price) || price <= 0)
            {
                continue;
            }

            _ = int.TryParse(Get(cells, headers, "stock_hint"), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var stockHint);
            rows.Add(new PriceOfferRow(
                Get(cells, headers, "supplier"),
                brand,
                string.IsNullOrWhiteSpace(articleShow) ? article : articleShow,
                Get(cells, headers, "name"),
                price,
                stockHint,
                Get(cells, headers, "lead_time")));
        }

        return rows.OrderBy(row => row.Price).Take(LegacyPriceLookupSql.DefaultLimit).ToArray();
    }

    private static string Get(string[] cells, string[] headers, string name)
    {
        var index = Array.FindIndex(headers, header => string.Equals(header, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index < cells.Length ? cells[index] : string.Empty;
    }

    private static string[] SplitCsvLine(string line)
    {
        return line.Split(',', StringSplitOptions.TrimEntries);
    }
}
