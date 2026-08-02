namespace EcomAE.Platform.Auth;

public sealed class MigrationLegacyApiUsageLogger : ILegacyApiUsageLogger
{
    public List<LegacyApiUsageLogEntry> Entries { get; } = [];

    public Task LogAsync(LegacyApiUsageLogEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry with
        {
            Action = Truncate(entry.Action, 40),
            Section = Truncate(entry.Section, 20),
            Source = Truncate(string.IsNullOrWhiteSpace(entry.Source) ? "api_client" : entry.Source, 40),
            RequestPath = Truncate(entry.RequestPath, 255),
            Message = Truncate(entry.Message, 255),
            IpAddress = Truncate(entry.IpAddress, 45)
        });
        return Task.CompletedTask;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
