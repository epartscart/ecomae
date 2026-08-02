namespace EcomAE.Platform.Migration;

public interface ISurfaceDashboardSummaryReporter
{
    Task<ControlPanelDashboardSummary> BuildControlPanelAsync(CancellationToken cancellationToken = default);

    Task<ErpDashboardSummary> BuildErpAsync(CancellationToken cancellationToken = default);

    Task<BosFleetSummary> BuildBosAsync(CancellationToken cancellationToken = default);

    Task<StorefrontAccountSummary> BuildStorefrontAccountAsync(int userId, CancellationToken cancellationToken = default);
}
