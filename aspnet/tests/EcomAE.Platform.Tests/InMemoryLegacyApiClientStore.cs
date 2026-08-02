using EcomAE.Platform.Auth;

namespace EcomAE.Platform.Tests;

internal sealed class InMemoryLegacyApiClientStore : ILegacyApiClientStore
{
    private readonly Dictionary<string, LegacyApiClientRecord> _clients;

    public InMemoryLegacyApiClientStore(params LegacyApiClientRecord[] clients)
    {
        _clients = clients.ToDictionary(client => client.ClientKeyHash, StringComparer.OrdinalIgnoreCase);
        IsConfigured = true;
    }

    public bool IsConfigured { get; init; }

    public Task<LegacyApiClientRecord?> FindActiveByHashAsync(string sha256Hash, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(sha256Hash, out var client) || !client.Active)
        {
            return Task.FromResult<LegacyApiClientRecord?>(null);
        }

        return Task.FromResult<LegacyApiClientRecord?>(client);
    }

    public Task ResetDailyQuotaIfNeededAsync(LegacyApiClientRecord client, DateOnly today, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(client.ClientKeyHash, out var current) || current.CallsResetDate == today)
        {
            return Task.CompletedTask;
        }

        _clients[client.ClientKeyHash] = current with { CallsToday = 0, CallsResetDate = today };
        return Task.CompletedTask;
    }

    public Task<bool> TryConsumeDailyQuotaAsync(LegacyApiClientRecord client, CancellationToken cancellationToken = default)
    {
        if (!_clients.TryGetValue(client.ClientKeyHash, out var current))
        {
            return Task.FromResult(false);
        }

        var limit = Math.Max(1, current.DailyLimit);
        if (current.CallsToday >= limit)
        {
            return Task.FromResult(false);
        }

        _clients[client.ClientKeyHash] = current with { CallsToday = current.CallsToday + 1 };
        return Task.FromResult(true);
    }
}
