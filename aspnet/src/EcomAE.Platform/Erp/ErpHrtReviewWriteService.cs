using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>epc_hrt_review_finalize</c> twin (epc_erp_hr_talent.php).</summary>
public interface IErpHrtReviewWriteService
{
    Task<ErpHrtFinalizeResult> FinalizeAsync(long reviewId, CancellationToken cancellationToken = default);
}

public sealed record ErpHrtFinalizeResult(bool Ok, string Message, decimal Overall, int Writes);

public sealed class ErpHrtReviewWriteService : IErpHrtReviewWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly TimeProvider _clock;

    public ErpHrtReviewWriteService(IErpWriteConnectionFactory connections, TimeProvider? clock = null)
    {
        _connections = connections;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ErpHrtFinalizeResult> FinalizeAsync(long reviewId, CancellationToken cancellationToken = default)
    {
        if (reviewId <= 0) return new(false, "Review not found", 0m, 0);
        if (!_connections.IsConfigured) return new(false, "TenantRegistry DB is not configured.", 0m, 0);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        string? status;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `status` FROM `epc_hrt_review` WHERE `id`=?");
            ErpDb.AddParameters(command, reviewId);
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (scalar is null or DBNull) return new(false, "Review not found", 0m, 0);
            status = Convert.ToString(scalar);
        }
        if (status == "completed") return new(false, "Review is already completed", 0m, 0);

        var goals = new List<(decimal Weight, decimal Rating)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `weight`,`rating` FROM `epc_hrt_goal` WHERE `review_id`=? ORDER BY `id` ASC");
            ErpDb.AddParameters(command, reviewId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                goals.Add((Convert.ToDecimal(reader.GetValue(0)), Convert.ToDecimal(reader.GetValue(1))));
            }
        }
        if (goals.Count == 0) return new(false, "Add at least one goal before finalizing", 0m, 0);

        var wsum = 0m;
        var rsum = 0m;
        foreach (var (weight, rating) in goals)
        {
            wsum += weight;
            rsum += weight * rating;
        }
        var overall = wsum <= 0 ? 0m : Math.Round(rsum / wsum, 2, MidpointRounding.AwayFromZero);

        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_hrt_review` SET `status`='completed', `overall_rating`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            overall,
            _clock.GetUtcNow().ToUnixTimeSeconds(),
            reviewId).ConfigureAwait(false);

        return new(true, "Review finalized — overall " + overall.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), overall, writes);
    }

    /// <summary>PHP <c>epc_hrt_ensure_schema</c> — verbatim.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_hrt_job` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `title` varchar(160) NOT NULL DEFAULT '',
            `department` varchar(120) NOT NULL DEFAULT '',
            `headcount` int(11) NOT NULL DEFAULT 1,
            `hired` int(11) NOT NULL DEFAULT 0,
            `status` varchar(20) NOT NULL DEFAULT 'open',
            `hiring_manager` varchar(160) NOT NULL DEFAULT '',
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Job requisitions'", ct).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_hrt_applicant` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `job_id` int(11) NOT NULL DEFAULT 0,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `name` varchar(160) NOT NULL DEFAULT '',
            `email` varchar(160) NOT NULL DEFAULT '',
            `phone` varchar(60) NOT NULL DEFAULT '',
            `stage` varchar(20) NOT NULL DEFAULT 'applied',
            `rating` int(11) NOT NULL DEFAULT 0,
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_job` (`job_id`),
            KEY `x_company` (`company_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Recruitment applicants'", ct).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_hrt_review` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `company_id` int(11) NOT NULL DEFAULT 0,
            `employee_id` int(11) NOT NULL DEFAULT 0,
            `employee_name` varchar(160) NOT NULL DEFAULT '',
            `period` varchar(40) NOT NULL DEFAULT '',
            `status` varchar(20) NOT NULL DEFAULT 'draft',
            `reviewer` varchar(160) NOT NULL DEFAULT '',
            `overall_rating` decimal(5,2) NOT NULL DEFAULT 0.00,
            `notes` text,
            `time_created` int(11) NOT NULL DEFAULT 0,
            `time_updated` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_company` (`company_id`),
            KEY `x_status` (`status`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Performance reviews'", ct).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(connection, null, @"CREATE TABLE IF NOT EXISTS `epc_hrt_goal` (
            `id` int(11) NOT NULL AUTO_INCREMENT,
            `review_id` int(11) NOT NULL DEFAULT 0,
            `title` varchar(200) NOT NULL DEFAULT '',
            `weight` decimal(8,2) NOT NULL DEFAULT 1.00,
            `target` varchar(200) NOT NULL DEFAULT '',
            `rating` int(11) NOT NULL DEFAULT 0,
            PRIMARY KEY (`id`),
            KEY `x_review` (`review_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Performance review goals'", ct).ConfigureAwait(false);
    }
}
