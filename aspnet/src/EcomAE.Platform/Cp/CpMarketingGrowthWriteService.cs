using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_marketing.php</c> twins of <c>epc_marketing_save_review</c>,
/// <c>epc_marketing_toggle_task</c>, and <c>epc_marketing_save_kpi</c>.
/// Schema-ensure stays Classic. This service does not invent a send.
/// </summary>
public interface ICpMarketingGrowthWriteService
{
    Task<ErpSimpleWriteResult> SaveReviewAsync(
        string? strategyKey,
        string? reviewType,
        int score,
        string? notes,
        long createdBy,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ToggleTaskAsync(
        string? strategyKey,
        string? taskKey,
        bool done,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveKpiAsync(
        string? strategyKey,
        string? kpiKey,
        string? value,
        string? note,
        long recordedBy,
        CancellationToken cancellationToken = default);
}

public sealed class CpMarketingGrowthWriteService : ICpMarketingGrowthWriteService
{
    public static readonly HashSet<string> StrategyKeys = new(StringComparer.Ordinal)
    {
        "measurement",
        "seo",
        "paid_ads",
        "marketplaces",
        "whatsapp_social",
        "trust",
        "international",
        "email_retention",
        "partnerships",
        "quick_wins",
    };

    public static readonly HashSet<string> ReviewTypes = new(StringComparer.Ordinal)
    {
        "weekly",
        "monthly",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpMarketingGrowthWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveReviewAsync(
        string? strategyKey,
        string? reviewType,
        int score,
        string? notes,
        long createdBy,
        CancellationToken cancellationToken = default)
    {
        var strategy = Clip(strategyKey, 64);
        if (strategy.Length == 0 || !StrategyKeys.Contains(strategy))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid strategy");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var type = Clip(reviewType, 32);
        if (type.Length == 0 || !ReviewTypes.Contains(type))
        {
            type = "weekly";
        }

        var rowScore = Math.Clamp(score, 0, 5);
        var rowNotes = (notes ?? string.Empty).Trim();
        var owner = createdBy > 0 ? createdBy : 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_marketing_reviews`
                    (`strategy_key`, `review_type`, `score`, `notes`, `created_at`, `created_by`)
                    VALUES (?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                strategy, type, rowScore, rowNotes, now, owner);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Review saved", created);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Marketing review table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> ToggleTaskAsync(
        string? strategyKey,
        string? taskKey,
        bool done,
        CancellationToken cancellationToken = default)
    {
        var strategy = Clip(strategyKey, 64);
        var task = Clip(taskKey, 128);
        if (strategy.Length == 0 || !StrategyKeys.Contains(strategy) || task.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid task");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_marketing_task_progress` (`strategy_key`, `task_key`, `is_done`, `done_at`, `updated_at`)
                    VALUES (?, ?, ?, ?, ?)
                    ON DUPLICATE KEY UPDATE `is_done` = VALUES(`is_done`), `done_at` = VALUES(`done_at`), `updated_at` = VALUES(`updated_at`)
                    """),
                cancellationToken,
                strategy, task, done ? 1 : 0, done ? now : null, now);
            return ErpSimpleWriteResult.Ok(done ? "Task marked done" : "Task reopened", 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Marketing task table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> SaveKpiAsync(
        string? strategyKey,
        string? kpiKey,
        string? value,
        string? note,
        long recordedBy,
        CancellationToken cancellationToken = default)
    {
        var strategy = Clip(strategyKey, 64);
        var kpi = Clip(kpiKey, 128);
        if (strategy.Length == 0 || !StrategyKeys.Contains(strategy) || kpi.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid KPI");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var raw = (value ?? string.Empty).Trim();
        decimal? dec = null;
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            dec = parsed;
        }

        var text = raw.Length > 512 ? raw[..512] : raw;
        var rowNote = (note ?? string.Empty).Trim();
        var owner = recordedBy > 0 ? recordedBy : 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_marketing_kpi_log`
                    (`strategy_key`, `kpi_key`, `value_decimal`, `value_text`, `note`, `recorded_at`, `recorded_by`)
                    VALUES (?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                strategy, kpi, dec, text, rowNote, now, owner);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("KPI recorded", created);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Marketing KPI table is missing — schema-ensure stays Classic.");
        }
    }

    public static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }
}
