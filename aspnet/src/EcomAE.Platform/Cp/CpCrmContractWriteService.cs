using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> twin of <c>epc_crm_save_contract</c>.
/// Quote email and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmContractWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmContractSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpCrmContractSaveRequest(
    long Id,
    long CustomerUserId,
    string? Title,
    decimal Amount,
    string? BillingInterval,
    string? NextBillingDate,
    string? Status,
    string? Notes);

public sealed class CpCrmContractWriteService : ICpCrmContractWriteService
{
    public static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "draft", "active", "paused", "ended",
    };

    public static readonly HashSet<string> Intervals = new(StringComparer.Ordinal)
    {
        "monthly", "quarterly", "yearly", "once",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpCrmContractWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim();
        return Statuses.Contains(raw) ? raw : "draft";
    }

    public static string NormalizeInterval(string? interval)
    {
        var raw = (interval ?? string.Empty).Trim();
        return Intervals.Contains(raw) ? raw : "monthly";
    }

    public static string NormalizeTitle(string? title)
    {
        var raw = (title ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            raw = "Contract";
        }

        return raw.Length > 255 ? raw[..255] : raw;
    }

    public static decimal NormalizeAmount(decimal amount)
        => amount < 0 ? 0 : amount;

    public static long ParseNextBilling(string? raw, DateTimeOffset now)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return now.AddMonths(1).ToUnixTimeSeconds();
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) && unix >= 0)
        {
            return unix;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            if (parsed.TimeOfDay == TimeSpan.Zero)
            {
                parsed = parsed.AddHours(12);
            }

            return parsed.ToUnixTimeSeconds();
        }

        return 0;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmContractSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Contract id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var title = NormalizeTitle(request.Title);
        var status = NormalizeStatus(request.Status);
        var interval = NormalizeInterval(request.BillingInterval);
        var amount = NormalizeAmount(request.Amount);
        var customerId = request.CustomerUserId < 0 ? 0 : request.CustomerUserId;
        var next = ParseNextBilling(request.NextBillingDate, DateTimeOffset.UtcNow);
        var notes = (request.Notes ?? string.Empty).Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var exists = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_contracts` WHERE `id`=?"),
                    cancellationToken, request.Id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Contract was not found.");
                }

                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_crm_contracts`
                        SET `customer_user_id`=?, `title`=?, `amount`=?, `billing_interval`=?,
                            `next_billing_date`=?, `status`=?, `notes`=?, `time_updated`=?
                        WHERE `id`=?
                        """),
                    cancellationToken,
                    customerId, title, amount, interval, next, status, notes, now, request.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Contract saved", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_crm_contracts`
                    (`customer_user_id`, `title`, `amount`, `billing_interval`, `next_billing_date`, `status`, `notes`, `time_created`, `time_updated`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                customerId, title, amount, interval, next, status, notes, now, now).ConfigureAwait(false);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Contract saved", created);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM contract table is missing — schema-ensure stays Classic.");
        }
    }
}
