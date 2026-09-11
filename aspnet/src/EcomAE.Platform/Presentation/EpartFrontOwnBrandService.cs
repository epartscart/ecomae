using EcomAE.Platform.Data;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// "Own Brand" rollup for the epartscart front page — same query as PHP
/// epart_front_render_own_brand() in content/general_pages/epart_catalog_front_links.php
/// (top own-brand lines from shop_docpart_prices_data, hidden when empty).
/// </summary>
public interface IEpartFrontOwnBrandService
{
    Task<IReadOnlyList<EpartOwnBrandRow>> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record EpartOwnBrandRow(string Brand, long Count);

public sealed class EpartFrontOwnBrandService : IEpartFrontOwnBrandService
{
    // PHP: $DP_Config->own_brand_name ?: 'EPC'; trailing wildcard keeps index usage.
    private const string OwnBrandLike = "EPC%";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15); // PHP perf-cache 900s

    private readonly ITenantDbConnectionFactory _connections;
    private readonly ILogger<EpartFrontOwnBrandService> _logger;
    private readonly object _gate = new();
    private (DateTimeOffset ExpiresAt, IReadOnlyList<EpartOwnBrandRow> Rows)? _cache;

    public EpartFrontOwnBrandService(
        ITenantDbConnectionFactory connections,
        ILogger<EpartFrontOwnBrandService> logger)
    {
        _connections = connections;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EpartOwnBrandRow>> GetAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<EpartOwnBrandRow>? stale = null;
        lock (_gate)
        {
            if (_cache is { } hit)
            {
                if (hit.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    return hit.Rows;
                }

                stale = hit.Rows;
            }
        }

        // First paint must not GROUP BY shop_docpart_prices_data — that scan wedges the shop DB
        // and blanks every frontend page. Serve stale/empty and refresh off the request.
        if (ErpFirstPaint.IsActive)
        {
            if (stale is not null)
            {
                ScheduleRefresh();
                return stale;
            }

            ScheduleRefresh();
            return [];
        }

        var rows = await QueryAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _cache = (DateTimeOffset.UtcNow.Add(CacheTtl), rows);
        }

        return rows;
    }

    private void ScheduleRefresh()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var rows = await QueryAsync(CancellationToken.None).ConfigureAwait(false);
                lock (_gate)
                {
                    _cache = (DateTimeOffset.UtcNow.Add(CacheTtl), rows);
                }
            }
            catch
            {
                // Keep last cache; home already painted.
            }
        });
    }

    private async Task<IReadOnlyList<EpartOwnBrandRow>> QueryAsync(CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 3; // PHP sets PDO::ATTR_TIMEOUT 3
            command.CommandText =
                "SELECT d.`brand` AS brand, COUNT(*) AS cnt "
                + "FROM `shop_docpart_prices_data` d "
                + "WHERE d.`brand` LIKE @brandLike "
                + "GROUP BY d.`brand` ORDER BY cnt DESC LIMIT 10";
            var p = command.CreateParameter();
            p.ParameterName = "@brandLike";
            p.Value = OwnBrandLike;
            command.Parameters.Add(p);

            var rows = new List<EpartOwnBrandRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var brand = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(brand))
                {
                    continue;
                }

                rows.Add(new EpartOwnBrandRow(brand, reader.GetInt64(1)));
            }

            return rows;
        }
        catch (Exception ex)
        {
            // Same behaviour as PHP: any DB trouble hides the section (never breaks home).
            _logger.LogWarning(ex, "Own Brand front section query failed — section hidden");
            return [];
        }
    }
}
