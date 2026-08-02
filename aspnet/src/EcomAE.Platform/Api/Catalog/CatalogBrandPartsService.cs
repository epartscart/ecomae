using System.Text.RegularExpressions;

namespace EcomAE.Platform.Api.Catalog;

public sealed partial class CatalogBrandPartsService : ICatalogBrandPartsService
{
    private readonly ICatalogBrandPartsRepository _repository;

    public CatalogBrandPartsService(ICatalogBrandPartsRepository repository)
    {
        _repository = repository;
    }

    public async Task<CatalogBrandPartsResult> ListAsync(string? brand, int limit, int offset, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(brand))
        {
            return new CatalogBrandPartsResult(false, string.Empty, 0, "rejected", [], "Brand is required.");
        }

        var brandUpper = brand.Trim().ToUpperInvariant();
        var brandCompact = CompactBrand().Replace(brandUpper, string.Empty);
        var safeLimit = limit is < 1 or > 500 ? 100 : limit;
        var safeOffset = offset < 0 ? 0 : offset;

        var (total, page) = await _repository
            .FindByBrandAsync(brandUpper, brandCompact, safeLimit, safeOffset, cancellationToken)
            .ConfigureAwait(false);

        var data = page.Select(row => (object)new
        {
            manufacturer = row.Manufacturer,
            article_show = row.ArticleShow,
            article = row.Article,
            name = row.Name,
            exist = row.Exist,
            price = row.Price,
            time_to_exe = row.TimeToExe,
            storage = row.Storage
        }).ToArray();

        return new CatalogBrandPartsResult(
            true,
            brand.Trim(),
            total,
            "database",
            data,
            total == 0 ? "No in-stock parts for brand in shop_docpart_prices_data." : string.Empty);
    }

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex CompactBrand();
}
