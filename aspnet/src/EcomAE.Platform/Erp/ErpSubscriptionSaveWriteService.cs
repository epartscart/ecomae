using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_sub_save</c> twin. Schema ensure and cycle-invoice generate stay PHP.
/// </summary>
public interface IErpSubscriptionSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        string? code,
        string? customer,
        string? planName,
        decimal amount,
        string? currency,
        string? cycle,
        int termMonths,
        string? startDate,
        long id,
        CancellationToken cancellationToken = default);
}

public sealed class ErpSubscriptionSaveWriteService : IErpSubscriptionSaveWriteService
{
    public static readonly string[] AllowedCycles = ["monthly", "quarterly", "annual"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpSubscriptionSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        string? code,
        string? customer,
        string? planName,
        decimal amount,
        string? currency,
        string? cycle,
        int termMonths,
        string? startDate,
        long id,
        CancellationToken cancellationToken = default)
    {
        var subCode = Clip(code, 40);
        var who = Clip(customer, 200);
        if (subCode.Length == 0 || who.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Code and customer are required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (id < 0)
        {
            id = 0;
        }

        if (amount < 0)
        {
            amount = 0;
        }

        amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var plan = Clip(planName, 160);
        var ccy = Clip(currency, 3).ToUpperInvariant();
        if (ccy.Length == 0)
        {
            ccy = "AED";
        }

        var cyc = NormalizeCycle(cycle);
        if (termMonths <= 0)
        {
            termMonths = 12;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var start = ResolveStartUnix(startDate, now);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (id > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "UPDATE `epc_erp_subscriptions` SET `customer`=?, `plan_name`=?, `amount`=?, `currency`=?, `cycle`=?, `term_months`=?, `start_date`=? WHERE `id`=?"),
                    cancellationToken,
                    who, plan, amount, ccy, cyc, termMonths, start, id);
                return ErpSimpleWriteResult.Ok("Subscription saved", id);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_subscriptions` (`code`,`customer`,`plan_name`,`amount`,`currency`,`cycle`,`term_months`,`start_date`,`next_bill_date`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,'active',?)"),
                cancellationToken,
                subCode, who, plan, amount, ccy, cyc, termMonths, start, start, now);
            var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Subscription saved", inserted);
        }
        catch (Exception ex) when (ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Subscription code already exists.");
        }
    }

    public static string NormalizeCycle(string? raw)
    {
        var cycle = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return AllowedCycles.Contains(cycle, StringComparer.Ordinal) ? cycle : "monthly";
    }

    public static long ResolveStartUnix(string? raw, long now)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return now;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) && unix > 1_000_000_000)
        {
            return unix;
        }

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return now;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
