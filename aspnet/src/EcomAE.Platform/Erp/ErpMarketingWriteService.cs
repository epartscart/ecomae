using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_marketing_create</c> twin. Staff schema ensure and sample seed stay PHP.
/// </summary>
public interface IErpMarketingWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        string? name,
        string? channel,
        decimal budget,
        string? status,
        string? timeStart,
        string? timeEnd,
        string? notes,
        CancellationToken cancellationToken = default);
}

public sealed class ErpMarketingWriteService : IErpMarketingWriteService
{
    public static readonly string[] AllowedStatuses = ["draft", "active", "paused", "completed"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpMarketingWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        string? name,
        string? channel,
        decimal budget,
        string? status,
        string? timeStart,
        string? timeEnd,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var campaign = Clip(name, 255);
        if (campaign.Length == 0)
        {
            campaign = "Campaign";
        }

        var ch = Clip(channel, 64);
        if (ch.Length == 0)
        {
            ch = "digital";
        }

        if (budget < 0m)
        {
            budget = 0m;
        }

        var st = (status ?? string.Empty).Trim();
        if (!AllowedStatuses.Contains(st, StringComparer.Ordinal))
        {
            st = "active";
        }

        var start = ResolveStartUnix(timeStart, now);
        var end = ResolveEndUnix(timeEnd, now);
        var note = (notes ?? string.Empty).Trim();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_marketing_campaigns` (`name`, `channel`, `budget`, `spent`, `leads`, `status`, `time_start`, `time_end`, `notes`, `time_created`) VALUES (?, ?, ?, 0, 0, ?, ?, ?, ?, ?)"),
            cancellationToken,
            campaign, ch, budget, st, start, end, note, now);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Campaign " + campaign + " created.", id);
    }

    public static long ResolveStartUnix(string? raw, long now)
    {
        if (TryUnix(raw, out var unix))
        {
            return unix;
        }

        if (TryDate(raw, out var date))
        {
            return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return now;
    }

    public static long ResolveEndUnix(string? raw, long now)
    {
        if (TryUnix(raw, out var unix))
        {
            return unix;
        }

        if (TryDate(raw, out var date))
        {
            return new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return now + 86400L * 30;
    }

    private static bool TryUnix(string? raw, out long unix)
    {
        unix = 0;
        var text = (raw ?? string.Empty).Trim();
        return text.Length > 0
               && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out unix)
               && unix > 1_000_000_000;
    }

    private static bool TryDate(string? raw, out DateTime date)
    {
        return DateTime.TryParseExact(
            (raw ?? string.Empty).Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
