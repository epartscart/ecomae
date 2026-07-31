namespace EcomAE.Platform.Auth;

public interface ILegacyApiUsageLogger
{
    Task LogAsync(LegacyApiUsageLogEntry entry, CancellationToken cancellationToken = default);
}
