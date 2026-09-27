using System.Data.Common;
using System.Globalization;
using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live twin of the PHP CP reader behind <c>cp/content/shop/parts_agent/parts_agent_chats.php</c>
/// (<c>epc_agent_cp_list_sessions</c>, <c>epc_agent_cp_enrich_sessions</c>,
/// <c>epc_agent_cp_get_session</c>, <c>epc_agent_cp_stats</c>).
/// Temp-file sync (<c>epc_agent_cp_sync_file_sessions</c>) stays on the Classic twin — it reads the
/// PHP server's chat temp folder, which the ASP.NET host does not own.
/// </summary>
public interface ICpPartsAgentDeskService
{
    Task<CpPartsAgentDeskView> LoadAsync(
        CpPartsAgentDeskQuery query,
        CancellationToken cancellationToken = default);

    Task<CpPartsAgentTranscript?> LoadTranscriptAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<string> ExportCsvAsync(
        CpPartsAgentDeskQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record CpPartsAgentDeskQuery(string Search, string DateFrom, string DateTo, int Limit, int Offset)
{
    public static CpPartsAgentDeskQuery Create(string? search, string? dateFrom, string? dateTo, int? limit, int? offset)
        => new(
            (search ?? string.Empty).Trim(),
            (dateFrom ?? string.Empty).Trim(),
            (dateTo ?? string.Empty).Trim(),
            Math.Clamp(limit ?? 50, 1, 200),
            Math.Max(0, offset ?? 0));

    public bool IsUnfiltered
        => Search.Length == 0 && DateFrom.Length == 0 && DateTo.Length == 0;
}

public sealed record CpPartsAgentSessionRow(
    string SessionId,
    long CreatedAt,
    long UpdatedAt,
    int MessageCount,
    string CountryCode,
    string CountryName,
    string LastUserText,
    string LastAgentText,
    long UserId,
    string ClientIp,
    string CustomerLabel);

public sealed record CpPartsAgentMessageRow(long Id, string Role, string Text, long CreatedAt);

public sealed record CpPartsAgentTranscript(
    CpPartsAgentSessionRow Session,
    IReadOnlyList<CpPartsAgentMessageRow> Messages);

public sealed record CpPartsAgentStats(
    int TotalSessions,
    int SessionsToday,
    int MessagesToday,
    int LoggedInSessions)
{
    public int GuestSessions => Math.Max(0, TotalSessions - LoggedInSessions);
}

public sealed record CpPartsAgentDeskView(
    IReadOnlyList<CpPartsAgentSessionRow> Sessions,
    int Total,
    CpPartsAgentStats Stats,
    CpPartsAgentDeskQuery Query,
    string Source,
    string Message)
{
    public static CpPartsAgentDeskView Empty(CpPartsAgentDeskQuery query, string source, string message)
        => new([], 0, new CpPartsAgentStats(0, 0, 0, 0), query, source, message);

    public int Page => Query.Limit > 0 ? (Query.Offset / Query.Limit) + 1 : 1;

    public int Pages => Query.Limit > 0 ? Math.Max(1, (Total + Query.Limit - 1) / Query.Limit) : 1;
}

public sealed class CpPartsAgentDeskService : ICpPartsAgentDeskService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPartsAgentDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpPartsAgentDeskView> LoadAsync(
        CpPartsAgentDeskQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPartsAgentDeskView.Empty(query, "unconfigured", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CpPartsAgentWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var (where, parameters) = BuildFilter(query);

            var total = await CountAsync(connection, where, parameters, cancellationToken).ConfigureAwait(false);
            var rows = await ListAsync(connection, where, parameters, query.Limit, query.Offset, cancellationToken)
                .ConfigureAwait(false);
            await EnrichCustomersAsync(connection, rows, cancellationToken).ConfigureAwait(false);
            var stats = await StatsAsync(connection, cancellationToken).ConfigureAwait(false);

            return new CpPartsAgentDeskView(rows, total, stats, query, "database", string.Empty);
        }
        catch (DbException ex)
        {
            return CpPartsAgentDeskView.Empty(query, "database-error", ex.Message);
        }
    }

    public async Task<CpPartsAgentTranscript?> LoadTranscriptAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var safe = SafeSessionId(sessionId);
        if (safe.Length == 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CpPartsAgentWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var rows = await ListAsync(connection, "`session_id` = ?", [safe], 1, 0, cancellationToken)
                .ConfigureAwait(false);
            if (rows.Count == 0)
            {
                return null;
            }

            await EnrichCustomersAsync(connection, rows, cancellationToken).ConfigureAwait(false);

            var messages = new List<CpPartsAgentMessageRow>();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                """
                SELECT `id`, `role`, `message_text`, `created_at`
                FROM `epc_parts_agent_message`
                WHERE `session_id` = ?
                ORDER BY `created_at` ASC, `id` ASC
                LIMIT 500
                """);
            ErpDb.AddParameters(cmd, safe);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                messages.Add(new CpPartsAgentMessageRow(
                    ReadLong(reader, 0),
                    ReadText(reader, 1) == "user" ? "user" : "agent",
                    ReadText(reader, 2),
                    ReadLong(reader, 3)));
            }

            return new CpPartsAgentTranscript(rows[0], messages);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<string> ExportCsvAsync(
        CpPartsAgentDeskQuery query,
        CancellationToken cancellationToken = default)
    {
        var view = await LoadAsync(query with { Limit = 200, Offset = 0 }, cancellationToken).ConfigureAwait(false);
        var csv = new StringBuilder();
        csv.Append('\uFEFF');
        csv.AppendLine("session_id,updated_at_iso,message_count,user_id,customer,country,client_ip,last_user_text,last_agent_text");
        foreach (var row in view.Sessions)
        {
            var updated = row.UpdatedAt > 0
                ? DateTimeOffset.FromUnixTimeSeconds(row.UpdatedAt).UtcDateTime.ToString("o", CultureInfo.InvariantCulture)
                : string.Empty;
            var country = (row.CountryName + " " + row.CountryCode).Trim();
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(row.SessionId),
                Csv(updated),
                Csv(row.MessageCount.ToString(CultureInfo.InvariantCulture)),
                Csv(row.UserId.ToString(CultureInfo.InvariantCulture)),
                Csv(row.CustomerLabel),
                Csv(country),
                Csv(row.ClientIp),
                Csv(row.LastUserText),
                Csv(row.LastAgentText),
            }));
        }

        return csv.ToString();
    }

    /// <summary>PHP <c>preg_replace('/[^a-zA-Z0-9_\-]/', '', $session_id)</c>.</summary>
    public static string SafeSessionId(string? sessionId)
    {
        var raw = sessionId ?? string.Empty;
        var buffer = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-')
            {
                buffer.Append(ch);
            }
        }

        return buffer.Length > 64 ? buffer.ToString(0, 64) : buffer.ToString();
    }

    /// <summary>PHP <c>strtotime($date . ' 00:00:00' | ' 23:59:59')</c> for the CP date filters.</summary>
    public static long? ParseFilterDate(string? value, bool endOfDay)
    {
        if (!DateTime.TryParse(
                (value ?? string.Empty).Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return null;
        }

        var day = new DateTime(parsed.Year, parsed.Month, parsed.Day, 0, 0, 0, DateTimeKind.Utc);
        if (endOfDay)
        {
            day = day.AddDays(1).AddSeconds(-1);
        }

        return new DateTimeOffset(day).ToUnixTimeSeconds();
    }

    private static (string Where, object[] Parameters) BuildFilter(CpPartsAgentDeskQuery query)
    {
        var clauses = new List<string> { "1=1" };
        var parameters = new List<object>();

        var from = ParseFilterDate(query.DateFrom, false);
        if (from.HasValue)
        {
            clauses.Add("`updated_at` >= ?");
            parameters.Add(from.Value);
        }

        var to = ParseFilterDate(query.DateTo, true);
        if (to.HasValue)
        {
            clauses.Add("`updated_at` <= ?");
            parameters.Add(to.Value);
        }

        if (query.Search.Length > 0)
        {
            var like = "%" + query.Search + "%";
            clauses.Add("(`session_id` LIKE ? OR `last_user_text` LIKE ? OR `last_agent_text` LIKE ? OR `client_ip` LIKE ?)");
            parameters.Add(like);
            parameters.Add(like);
            parameters.Add(like);
            parameters.Add(like);
        }

        return (string.Join(" AND ", clauses), parameters.ToArray());
    }

    private static async Task<int> CountAsync(
        DbConnection connection,
        string where,
        object[] parameters,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("SELECT COUNT(*) FROM `epc_parts_agent_session` WHERE " + where);
        ErpDb.AddParameters(cmd, parameters);
        var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<List<CpPartsAgentSessionRow>> ListAsync(
        DbConnection connection,
        string where,
        object[] parameters,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpPartsAgentSessionRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `session_id`, `created_at`, `updated_at`, `message_count`, "
            + "IFNULL(`country_code`,''), IFNULL(`country_name`,''), IFNULL(`last_user_text`,''), "
            + "IFNULL(`last_agent_text`,''), `user_id`, IFNULL(`client_ip`,'') "
            + "FROM `epc_parts_agent_session` WHERE " + where
            + " ORDER BY `updated_at` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture)
            + " OFFSET " + offset.ToString(CultureInfo.InvariantCulture));
        ErpDb.AddParameters(cmd, parameters);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CpPartsAgentSessionRow(
                ReadText(reader, 0),
                ReadLong(reader, 1),
                ReadLong(reader, 2),
                (int)ReadLong(reader, 3),
                ReadText(reader, 4),
                ReadText(reader, 5),
                ReadText(reader, 6),
                ReadText(reader, 7),
                ReadLong(reader, 8),
                ReadText(reader, 9),
                string.Empty));
        }

        return rows;
    }

    /// <summary>PHP <c>epc_agent_cp_enrich_sessions</c>: customer label from `users` + `users_profiles`.</summary>
    private static async Task EnrichCustomersAsync(
        DbConnection connection,
        List<CpPartsAgentSessionRow> rows,
        CancellationToken cancellationToken)
    {
        var ids = rows.Where(row => row.UserId > 0).Select(row => row.UserId).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var placeholders = string.Join(',', ids.Select(_ => "?"));
        var labels = new Dictionary<long, string>();

        await using (var users = connection.CreateCommand())
        {
            users.CommandText = ErpDb.Positional(
                "SELECT `user_id`, IFNULL(`email`,''), IFNULL(`phone`,'') FROM `users` WHERE `user_id` IN ("
                + placeholders + ")");
            ErpDb.AddParameters(users, ids.Cast<object>().ToArray());
            await using var reader = await users.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var email = ReadText(reader, 1);
                var phone = ReadText(reader, 2);
                labels[ReadLong(reader, 0)] = email.Length > 0 ? email : phone;
            }
        }

        await using (var profiles = connection.CreateCommand())
        {
            profiles.CommandText = ErpDb.Positional(
                "SELECT `user_id`, `data_key`, `data_value` FROM `users_profiles` WHERE `user_id` IN ("
                + placeholders + ") AND `data_key` IN ('name','fio','company','firstname','surname')");
            ErpDb.AddParameters(profiles, ids.Cast<object>().ToArray());
            var best = new Dictionary<long, (int Rank, string Value)>();
            await using var reader = await profiles.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var userId = ReadLong(reader, 0);
                var rank = NameRank(ReadText(reader, 1));
                var value = ReadText(reader, 2).Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                if (!best.TryGetValue(userId, out var current) || rank < current.Rank)
                {
                    best[userId] = (rank, value);
                }
            }

            foreach (var (userId, name) in best)
            {
                labels[userId] = name.Value;
            }
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].UserId > 0 && labels.TryGetValue(rows[i].UserId, out var label) && label.Length > 0)
            {
                rows[i] = rows[i] with { CustomerLabel = label };
            }
        }
    }

    private static int NameRank(string key) => key switch
    {
        "name" => 1,
        "fio" => 2,
        "company" => 3,
        "firstname" => 4,
        "surname" => 5,
        _ => 99,
    };

    private static async Task<CpPartsAgentStats> StatsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).ToUnixTimeSeconds();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT (SELECT COUNT(*) FROM `epc_parts_agent_session`), "
            + "(SELECT COUNT(*) FROM `epc_parts_agent_session` WHERE `updated_at` >= ?), "
            + "(SELECT COUNT(*) FROM `epc_parts_agent_message` WHERE `created_at` >= ?), "
            + "(SELECT COUNT(*) FROM `epc_parts_agent_session` WHERE `user_id` > 0)");
        ErpDb.AddParameters(cmd, todayStart, todayStart);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new CpPartsAgentStats(0, 0, 0, 0);
        }

        return new CpPartsAgentStats(
            (int)ReadLong(reader, 0),
            (int)ReadLong(reader, 1),
            (int)ReadLong(reader, 2),
            (int)ReadLong(reader, 3));
    }

    private static string Csv(string value)
        => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal) + "\"";

    private static string ReadText(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static long ReadLong(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}
