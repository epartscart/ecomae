using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_coll_case_save</c> twin. Schema ensure, promise-to-pay,
/// activity log, dunning run, and credit hold stay PHP.
/// </summary>
public interface IErpCollectionsCaseSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        long customerId,
        string? status,
        decimal balance,
        decimal promiseAmount,
        string? promiseDate,
        string? assignedTo,
        string? notes,
        long companyId,
        long id,
        CancellationToken cancellationToken = default);
}

public sealed class ErpCollectionsCaseSaveWriteService : IErpCollectionsCaseSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCollectionsCaseSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        long customerId,
        string? status,
        decimal balance,
        decimal promiseAmount,
        string? promiseDate,
        string? assignedTo,
        string? notes,
        long companyId,
        long id,
        CancellationToken cancellationToken = default)
    {
        if (customerId < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "customerId must be >= 0.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (id < 0)
        {
            id = 0;
        }

        if (companyId < 0)
        {
            companyId = 0;
        }

        if (balance < 0)
        {
            balance = 0;
        }

        if (promiseAmount < 0)
        {
            promiseAmount = 0;
        }

        balance = decimal.Round(balance, 2, MidpointRounding.AwayFromZero);
        promiseAmount = decimal.Round(promiseAmount, 2, MidpointRounding.AwayFromZero);
        var next = NormalizeStatus(status);
        var who = Clip(assignedTo, 120);
        var note = Clip(notes, 4000);
        var promiseUnix = ResolvePromiseUnix(promiseDate);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_coll_cases` SET `customer_id`=?, `status`=?, `balance`=?, `promise_amount`=?, `promise_date`=?, `assigned_to`=?, `notes`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                customerId, next, balance, promiseAmount, promiseUnix, who, note, now, id);
            return ErpSimpleWriteResult.Ok("Case saved", id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_coll_cases` (`company_id`,`customer_id`,`status`,`balance`,`promise_amount`,`promise_date`,`assigned_to`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId, customerId, next, balance, promiseAmount, promiseUnix, who, note, now, now);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Case saved", inserted);
    }

    public static string NormalizeStatus(string? raw)
    {
        var status = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return ErpCollectionsCaseStatusWriteService.Allowed.Contains(status, StringComparer.Ordinal)
            ? status
            : "new";
    }

    public static long ResolvePromiseUnix(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            return unix < 0 ? 0 : unix;
        }

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return 0;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
