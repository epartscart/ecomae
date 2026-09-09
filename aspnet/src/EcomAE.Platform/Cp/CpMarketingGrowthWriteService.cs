using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_marketing.php</c> <c>save_review</c> twin of <c>epc_marketing_save_review</c>.
/// Schema-ensure, task, and KPI writes stay Classic.
/// This service does not invent a send.
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

    public static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }
}
