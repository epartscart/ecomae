using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hrt_goal_add</c> / ajax <c>hrt_goal_add</c> twin.
/// INSERT <c>epc_hrt_goal</c> and flip a draft review to in_progress.
/// Review save, finalize, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpHrtGoalAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpHrtGoalAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpHrtGoalAddWriteRequest(
    long ReviewId = 0,
    string? Title = null,
    decimal? Weight = null,
    string? Target = null,
    int Rating = 0);

public sealed class ErpHrtGoalAddWriteService : IErpHrtGoalAddWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrtGoalAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpHrtGoalAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ReviewId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Review not found");
        }

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Goal title is required");
        }

        if (request.Rating < 0 || request.Rating > 5)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Rating must be 0-5");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var target = request.Target ?? string.Empty;
        var weight = request.Weight ?? 1m;
        if (weight < 0)
        {
            weight = 0;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_hrt_review", "status", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_hrt_goal", "title", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Performance goal table is not provisioned");
        }

        var statusObj = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_hrt_review` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            request.ReviewId).ConfigureAwait(false);
        if (statusObj is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Review not found");
        }

        var status = Convert.ToString(statusObj, CultureInfo.InvariantCulture) ?? string.Empty;
        if (string.Equals(status, "completed", StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cannot add goals to a completed review");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_hrt_goal` (`review_id`,`title`,`weight`,`target`,`rating`) VALUES (?,?,?,?,?)"),
            cancellationToken,
            request.ReviewId,
            title,
            weight,
            target,
            request.Rating).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        if (string.Equals(status, "draft", StringComparison.Ordinal))
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_hrt_review` SET `status`='in_progress', `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                request.ReviewId).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Goal added", id);
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
