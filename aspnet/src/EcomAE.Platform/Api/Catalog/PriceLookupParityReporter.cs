namespace EcomAE.Platform.Api.Catalog;

public sealed class PriceLookupParityReporter : IPriceLookupParityReporter
{
    private static readonly PriceLookupRequest SampleRequest = new("TOYOTA", "04465-0K020");

    private readonly IPriceLookupService _priceLookupService;

    public PriceLookupParityReporter(IPriceLookupService priceLookupService)
    {
        _priceLookupService = priceLookupService;
    }

    public async ValueTask<PriceLookupParityReport> BuildReportAsync(CancellationToken cancellationToken = default)
    {
        var result = await _priceLookupService.LookupAsync(SampleRequest, cancellationToken);
        var readyForShadowTraffic = result.Status && SampleRequest.IsValid;
        var gaps = new List<string>();

        if (result.Offers.Count == 0)
        {
            gaps.Add("Wire IPriceOfferRepository to production price tables and validate row-level totals against PHP.");
        }

        gaps.Add("Compare request/response schema with PHP api/v1 price endpoints using captured production fixtures.");
        gaps.Add("Apply legacy API-key policy, quota logging, and tenant scoping before public cutover.");

        return new PriceLookupParityReport(
            "PHP price lookup routes and shop_docpart_prices_data",
            result.MigrationStatus,
            SampleRequest.NormalizedBrand,
            SampleRequest.Article,
            readyForShadowTraffic ? "shadow-ready-with-gaps" : "not-ready",
            readyForShadowTraffic,
            gaps);
    }
}
