using System.Text.Json;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Cp;

/// <summary>
/// One gated-deploy record — appended to <c>/etc/ecomae-aspnet/release-history.jsonl</c>
/// by <c>scripts/cloudpanel_production_lock.sh write</c> on every successful publish.
/// </summary>
public sealed record CpReleaseEntry(string Sha, string Branch, string AtUtc)
{
    public string ShortSha => Sha.Length > 9 ? Sha[..9] : Sha;
}

public interface ICpReleaseHistoryService
{
    /// <summary>Newest-first release entries (empty when no history file exists yet).</summary>
    Task<IReadOnlyList<CpReleaseEntry>> RecentAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// <c>refreshed_at_utc</c> stamped into <c>{db}_sandbox.epc_sandbox_meta</c> by
    /// <c>scripts/cloudpanel_sandbox_db_refresh.sh</c>; null when the sandbox DB
    /// or meta table does not exist yet.
    /// </summary>
    Task<string?> SandboxRefreshedAtUtcAsync(string liveDatabaseName, CancellationToken cancellationToken = default);
}

public sealed class CpReleaseHistoryService : ICpReleaseHistoryService
{
    private const string DefaultHistoryPath = "/etc/ecomae-aspnet/release-history.jsonl";

    private readonly ITenantDbConnectionFactory _connections;

    public CpReleaseHistoryService(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<CpReleaseEntry>> RecentAsync(int take, CancellationToken cancellationToken = default)
    {
        var path = Environment.GetEnvironmentVariable("ECOMAE_RELEASE_HISTORY_FILE");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            // The writer places history beside the lock file — honor a custom
            // ECOMAE_PRODUCTION_LOCK_FILE location before the default.
            var lockPath = Environment.GetEnvironmentVariable("ECOMAE_PRODUCTION_LOCK_FILE");
            var besideLock = string.IsNullOrWhiteSpace(lockPath)
                ? null
                : Path.Combine(Path.GetDirectoryName(lockPath) ?? string.Empty, "release-history.jsonl");
            path = besideLock is not null && File.Exists(besideLock)
                ? besideLock
                : File.Exists(DefaultHistoryPath) ? DefaultHistoryPath : null;
        }

        if (path is null)
        {
            return [];
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
            var entries = new List<CpReleaseEntry>(Math.Min(take, lines.Length));
            for (var i = lines.Length - 1; i >= 0 && entries.Count < take; i--)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var sha = root.TryGetProperty("sha", out var s) ? s.GetString() : null;
                    if (string.IsNullOrEmpty(sha))
                    {
                        continue;
                    }

                    var branch = root.TryGetProperty("branch", out var b) ? b.GetString() ?? string.Empty : string.Empty;
                    var at = root.TryGetProperty("at", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                    entries.Add(new CpReleaseEntry(sha, branch, at));
                }
                catch (JsonException)
                {
                    // skip malformed line
                }
            }

            return entries;
        }
        catch
        {
            return [];
        }
    }

    public async Task<string?> SandboxRefreshedAtUtcAsync(string liveDatabaseName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(liveDatabaseName))
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(liveDatabaseName + "_sandbox", cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `meta_value` FROM `epc_sandbox_meta` WHERE `meta_key` = 'refreshed_at_utc' LIMIT 1";
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var text = value?.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
