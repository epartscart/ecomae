namespace EcomAE.Platform.Migration;

public interface IPlatformJobsSummaryReporter
{
    Task<PlatformJobsSummary> BuildAsync(int recentLimit, CancellationToken cancellationToken = default);
}
