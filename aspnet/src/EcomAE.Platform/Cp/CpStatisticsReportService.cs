using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// PHP <c>cp/content/shop/statistics</c> twin: article-query rating (<c>stat_article_queries_rating</c>) and
/// per-day time chart (<c>stat_article_queries_time_chart</c>) over <c>shop_stat_article_queries</c> for a date range.
/// </summary>
public interface ICpStatisticsReportService
{
    Task<CpStatisticsReport> BuildAsync(CpStatisticsReportRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpStatisticsReportRequest(string Report, DateOnly From, DateOnly To, string? Article, string? Brand, int Limit)
{
    public static CpStatisticsReportRequest Default(DateOnly today) => new("article_queries_rating", today.AddDays(-29), today, null, null, 100);
}

public sealed record CpStatisticsRatingRow(string Article, string Brand, long Hits, long UniqueIps, long LastSeen);

public sealed record CpStatisticsDayRow(DateOnly Day, long Hits, long UniqueArticles);

public sealed record CpStatisticsReport(
    CpStatisticsReportRequest Request,
    long TotalHits,
    long UniqueArticles,
    long UniqueIps,
    IReadOnlyList<CpStatisticsRatingRow> Rating,
    IReadOnlyList<CpStatisticsDayRow> Days,
    string Source,
    string Message);

public sealed class CpStatisticsReportService : ICpStatisticsReportService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpStatisticsReportService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpStatisticsReport> BuildAsync(CpStatisticsReportRequest request, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(request, 0, 0, 0, [], [], "migration", "TenantRegistry DB is not configured.");
        }

        var from = request.From <= request.To ? request.From : request.To;
        var to = request.From <= request.To ? request.To : request.From;
        var fromTs = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var toTs = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();
        var limit = Math.Clamp(request.Limit, 10, 500);

        var where = new List<string> { "IFNULL(`time`,0) >= ?", "IFNULL(`time`,0) < ?" };
        var args = new List<object?> { fromTs, toTs };
        var article = (request.Article ?? string.Empty).Trim();
        var brand = (request.Brand ?? string.Empty).Trim();
        if (article.Length > 0) { where.Add("`article` LIKE ?"); args.Add("%" + article + "%"); }
        if (brand.Length > 0) { where.Add("`manufacturer` LIKE ?"); args.Add("%" + brand + "%"); }
        var whereSql = " WHERE " + string.Join(" AND ", where);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            long totalHits = 0, uniqueArticles = 0, uniqueIps = 0;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional("SELECT COUNT(*), COUNT(DISTINCT IFNULL(`article`,'')), COUNT(DISTINCT IFNULL(`ip`,'')) FROM `shop_stat_article_queries`" + whereSql);
                ErpDb.AddParameters(cmd, args.ToArray());
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    totalHits = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                    uniqueArticles = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
                    uniqueIps = Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture);
                }
            }

            var rating = new List<CpStatisticsRatingRow>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`article`,''), IFNULL(`manufacturer`,''), COUNT(*) AS hits, COUNT(DISTINCT IFNULL(`ip`,'')), MAX(IFNULL(`time`,0)) FROM `shop_stat_article_queries`"
                    + whereSql + " GROUP BY IFNULL(`article`,''), IFNULL(`manufacturer`,'') ORDER BY hits DESC, 5 DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture));
                ErpDb.AddParameters(cmd, args.ToArray());
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rating.Add(new CpStatisticsRatingRow(
                        reader.GetString(0),
                        reader.GetString(1),
                        Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture)));
                }
            }

            var perDay = new Dictionary<DateOnly, (long Hits, long Articles)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT FROM_UNIXTIME(IFNULL(`time`,0), '%Y-%m-%d') AS d, COUNT(*), COUNT(DISTINCT IFNULL(`article`,'')) FROM `shop_stat_article_queries`"
                    + whereSql + " GROUP BY d ORDER BY d ASC");
                ErpDb.AddParameters(cmd, args.ToArray());
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (DateOnly.TryParseExact(reader.GetValue(0).ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                    {
                        perDay[day] = (Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture), Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture));
                    }
                }
            }

            var days = new List<CpStatisticsDayRow>();
            var span = Math.Min(to.DayNumber - from.DayNumber, 366);
            for (var i = 0; i <= span; i++)
            {
                var day = from.AddDays(i);
                var hit = perDay.TryGetValue(day, out var v) ? v : (0L, 0L);
                days.Add(new CpStatisticsDayRow(day, hit.Item1, hit.Item2));
            }

            return new(request with { From = from, To = to, Limit = limit }, totalHits, uniqueArticles, uniqueIps, rating, days, "shop_stat_article_queries", string.Empty);
        }
        catch (DbException ex)
        {
            return new(request, 0, 0, 0, [], [], "migration", ex.Message);
        }
    }
}
