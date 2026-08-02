namespace EcomAE.Platform.Migration;

public interface IUmapiUsageSummaryReporter
{
    Task<UmapiUsageSummary> BuildAsync(int days, CancellationToken cancellationToken = default);
}
