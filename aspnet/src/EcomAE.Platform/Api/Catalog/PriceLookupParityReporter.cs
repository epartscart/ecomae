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
            gaps.Add("Configure TenantRegistry/MySQL for DbPriceOfferRepository or PriceLookup:FixtureCsvPath, then validate row-level totals against PHP.");
        }

        gaps.Add("On CloudPanel: ensure_epc_api_clients_table.sh → issue_smoke_credentials.sh (epc_pricepro_ key) → validate_final_gate_env.sh.");
        gaps.Add("Attach live staging smoke artifacts for /api/v1/price/lookup before exact-route shadow.");
        gaps.Add("Contract-only dry run: python3 scripts/compare_price_lookup_parity.py sample.json sample.json --contract-only.");
        gaps.Add("Compare request/response schema with PHP api/v1 price endpoints using captured production fixtures.");
        gaps.Add("Replay staging X-API-Key (epc_pricepro_) against ASP.NET and PHP, then enable only location = /api/v1/price/lookup shadow.");

        return new PriceLookupParityReport(
            "PHP price lookup routes and shop_docpart_prices_data",
            result.MigrationStatus,
            SampleRequest.NormalizedBrand,
            SampleRequest.NormalizedArticle,
            readyForShadowTraffic ? "shadow-ready-with-gaps" : "not-ready",
            readyForShadowTraffic,
            gaps);
    }
}
