using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_ctr_save</c> twin. Schema ensure, sign, and OCR stay PHP.
/// </summary>
public interface IErpContractSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        string? code,
        string? title,
        string? counterparty,
        decimal contractValue,
        string? currency,
        string? startDate,
        string? endDate,
        string? bodyText,
        long id,
        CancellationToken cancellationToken = default);
}

public sealed class ErpContractSaveWriteService : IErpContractSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpContractSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        string? code,
        string? title,
        string? counterparty,
        decimal contractValue,
        string? currency,
        string? startDate,
        string? endDate,
        string? bodyText,
        long id,
        CancellationToken cancellationToken = default)
    {
        var ctrCode = Clip(code, 40);
        var ctrTitle = Clip(title, 200);
        if (ctrCode.Length == 0 || ctrTitle.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Code and title are required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (id < 0)
        {
            id = 0;
        }

        if (contractValue < 0)
        {
            contractValue = 0;
        }

        contractValue = decimal.Round(contractValue, 2, MidpointRounding.AwayFromZero);
        var party = Clip(counterparty, 200);
        var ccy = Clip(currency, 3).ToUpperInvariant();
        if (ccy.Length == 0)
        {
            ccy = "AED";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var start = ResolveUnix(startDate);
        var end = ResolveUnix(endDate);
        var body = bodyText ?? string.Empty;

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (id > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "UPDATE `epc_erp_contracts` SET `title`=?, `counterparty`=?, `contract_value`=?, `currency`=?, `start_date`=?, `end_date`=?, `body_text`=?, `version`=`version`+1, `time_updated`=? WHERE `id`=?"),
                    cancellationToken,
                    ctrTitle, party, contractValue, ccy, start, end, body, now, id);
                return ErpSimpleWriteResult.Ok("Contract saved", id);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_contracts` (`code`,`title`,`counterparty`,`contract_value`,`currency`,`start_date`,`end_date`,`status`,`version`,`body_text`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,'draft',1,?,?,?)"),
                cancellationToken,
                ctrCode, ctrTitle, party, contractValue, ccy, start, end, body, now, now);
            var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Contract saved", inserted);
        }
        catch (Exception ex) when (ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Contract code already exists.");
        }
    }

    public static long ResolveUnix(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) && unix > 1_000_000_000)
        {
            return unix;
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
