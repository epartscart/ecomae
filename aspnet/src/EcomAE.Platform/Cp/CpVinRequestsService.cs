using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Cp;

public sealed record CpVinRequestRow(long Id, long Time, long UserId, bool Viewed);

public sealed record CpVinFilter(int Viewed = -1, string CustomerId = "")
{
    public bool IsEmpty => Viewed == -1 && CustomerId.Length == 0;
}

public sealed record CpVinMessage(long Id, bool IsCustomer, string Text, long Time);

public sealed record CpVinRequest(
    long Id,
    long UserId,
    long Time,
    bool Viewed,
    string TextHtml,
    IReadOnlyList<CpVinMessage> Messages);

/// <summary>
/// Live twin of PHP <c>cp/content/requests/requests.php</c> + <c>request.php</c>
/// (<c>users_vin</c> list with cookie filter/paging, request detail and manager chat).
/// </summary>
public interface ICpVinRequestsService
{
    Task<CpPagedList<CpVinRequestRow>> ListAsync(CpVinFilter filter, int page, CancellationToken cancellationToken = default);

    /// <summary>PHP request.php: marks the request viewed on open, then loads it with its messages.</summary>
    Task<CpVinRequest?> OpenAsync(long vinId, CancellationToken cancellationToken = default);

    /// <summary>PHP request.php: user block shows only when the admin group has usermanager content access.</summary>
    Task<bool> HasUserManagerAccessAsync(IReadOnlyList<int> groupIds, CancellationToken cancellationToken = default);
}

public sealed class CpVinRequestsService : ICpVinRequestsService
{
    public const string FilterCookie = "vin_filter";
    public const string PageCookie = "vin_need_page";

    private readonly IErpWriteConnectionFactory _connections;

    public CpVinRequestsService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP: cookie <c>vin_filter</c> = {"viewed":-1|0|1,"customer_id":""}; query overrides win.</summary>
    public static CpVinFilter ReadFilter(HttpRequest request)
    {
        var viewed = -1;
        var customer = string.Empty;
        var raw = request.Cookies[FilterCookie];
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("viewed", out var v))
                    {
                        viewed = ParseFlag(v);
                    }

                    if (doc.RootElement.TryGetProperty("customer_id", out var c))
                    {
                        customer = c.ValueKind == JsonValueKind.Number ? c.GetRawText() : (c.GetString() ?? string.Empty);
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        var qv = request.Query["viewed"].ToString();
        if (qv.Length > 0 && int.TryParse(qv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qViewed))
        {
            viewed = qViewed;
        }

        if (request.Query.ContainsKey("customer_id"))
        {
            customer = request.Query["customer_id"].ToString();
        }

        if (viewed is not (0 or 1))
        {
            viewed = -1;
        }

        customer = customer.Trim();
        if (customer.Length > 0 && !long.TryParse(customer, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            customer = string.Empty;
        }

        return new CpVinFilter(viewed, customer);
    }

    /// <summary>PHP: cookie <c>vin_need_page</c>; query <c>s_page</c> overrides.</summary>
    public static int ReadPage(HttpRequest request)
    {
        var q = request.Query["s_page"].ToString();
        if (q.Length > 0)
        {
            return int.TryParse(q, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) && p > 0 ? p : 0;
        }

        return int.TryParse(request.Cookies[PageCookie], NumberStyles.Integer, CultureInfo.InvariantCulture, out var c) && c > 0 ? c : 0;
    }

    public static string FilterCookieValue(CpVinFilter filter)
        => JsonSerializer.Serialize(new { viewed = filter.Viewed, customer_id = filter.CustomerId });

    private static int ParseFlag(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n))
        {
            return n;
        }

        return e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : -1;
    }

    public async Task<CpPagedList<CpVinRequestRow>> ListAsync(CpVinFilter filter, int page, CancellationToken cancellationToken = default)
    {
        var rows = new List<CpVinRequestRow>();
        if (!_connections.IsConfigured)
        {
            return new(rows, 0, 0, 0);
        }

        var where = new List<string>();
        var args = new List<object?>();
        if (filter.Viewed is 0 or 1)
        {
            where.Add("`viewed` = ?");
            args.Add(filter.Viewed);
        }

        if (filter.CustomerId.Length > 0)
        {
            where.Add("`user_id` = ?");
            args.Add(long.Parse(filter.CustomerId, CultureInfo.InvariantCulture));
        }

        var whereSql = where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var total = (int)await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `users_vin`" + whereSql), cancellationToken, args.ToArray()).ConfigureAwait(false);
            var pages = CpTemplatesPluginsService.Pages(total);
            page = Math.Clamp(page, 0, Math.Max(0, pages - 1));

            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`time`,0), IFNULL(`user_id`,0), IFNULL(`viewed`,0) FROM `users_vin`" + whereSql +
                " ORDER BY `viewed` ASC, `id` DESC LIMIT ? OFFSET ?");
            args.Add(CpTemplatesPluginsService.PageLimit);
            args.Add(page * CpTemplatesPluginsService.PageLimit);
            ErpDb.AddParameters(c, args.ToArray());
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpVinRequestRow(
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToInt32(r.GetValue(3), CultureInfo.InvariantCulture) == 1));
            }

            return new(rows, total, page, pages);
        }
        catch (DbException)
        {
            return new(rows, 0, 0, 0);
        }
    }

    public async Task<CpVinRequest?> OpenAsync(long vinId, CancellationToken cancellationToken = default)
    {
        if (vinId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(connection, null,
                ErpDb.Positional("UPDATE `users_vin` SET `viewed` = 1 WHERE `id` = ?"), cancellationToken, vinId).ConfigureAwait(false);

            long userId = 0, time = 0;
            var text = string.Empty;
            var found = false;
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`user_id`,0), IFNULL(`time`,0), IFNULL(`text`,'') FROM `users_vin` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(c, vinId);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    found = true;
                    userId = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                    time = Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture);
                    text = r.GetString(2);
                }
            }

            if (!found)
            {
                return null;
            }

            var messages = new List<CpVinMessage>();
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`is_customer`,0), IFNULL(`text`,''), IFNULL(`time`,0) FROM `users_vin_messages` WHERE `vin_id` = ? ORDER BY `time`, `id`");
                ErpDb.AddParameters(c, vinId);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    messages.Add(new CpVinMessage(
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture) == 1,
                        r.GetString(2),
                        Convert.ToInt64(r.GetValue(3), CultureInfo.InvariantCulture)));
                }
            }

            return new CpVinRequest(vinId, userId, time, true, text, messages);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<bool> HasUserManagerAccessAsync(IReadOnlyList<int> groupIds, CancellationToken cancellationToken = default)
    {
        if (groupIds.Count == 0 || !_connections.IsConfigured)
        {
            return false;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var n = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional(
                    "SELECT COUNT(*) FROM `content_access` WHERE `content_id` IN (SELECT `id` FROM `content` WHERE `alias` = 'usermanager' AND `is_frontend` = 0) AND `group_id` = ?"),
                cancellationToken, groupIds[0]).ConfigureAwait(false);
            return n > 0;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
