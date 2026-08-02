namespace EcomAE.Platform.Api.Catalog;

public interface IPriceLookupParityReporter
{
    ValueTask<PriceLookupParityReport> BuildReportAsync(CancellationToken cancellationToken = default);
}
