namespace EcomAE.Platform.Auth;

public interface ILegacyApiClientStore
{
    bool IsConfigured { get; }

    Task<LegacyApiClientRecord?> FindActiveByHashAsync(string sha256Hash, CancellationToken cancellationToken = default);

    Task ResetDailyQuotaIfNeededAsync(LegacyApiClientRecord client, DateOnly today, CancellationToken cancellationToken = default);

    Task<bool> TryConsumeDailyQuotaAsync(LegacyApiClientRecord client, CancellationToken cancellationToken = default);
}
