using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> twin of <c>epc_crm_save_quote</c>.
/// Accept (order stub), quote email, and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmQuoteWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmQuoteSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpCrmQuoteSaveRequest(
    long Id,
    long OpportunityId,
    long LeadId,
    long CustomerUserId,
    string? QuoteNumber,
    string? Status,
    string? Notes,
    string? LineDescription,
    decimal LineQty,
    decimal LineUnitPrice);

public sealed class CpCrmQuoteWriteService : ICpCrmQuoteWriteService
{
    public static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "draft", "sent", "accepted", "rejected",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpCrmQuoteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim();
        return Statuses.Contains(raw) ? raw : "draft";
    }

    public static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }

    public static string NextQuoteNumber(long countPlusOne, DateTimeOffset now)
    {
        var n = countPlusOne < 1 ? 1 : countPlusOne;
        return "Q-" + now.ToString("yyyyMM", CultureInfo.InvariantCulture) + "-" + n.ToString("0000", CultureInfo.InvariantCulture);
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmQuoteSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quote id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var oppId = request.OpportunityId < 0 ? 0 : request.OpportunityId;
        var leadId = request.LeadId < 0 ? 0 : request.LeadId;
        var custId = request.CustomerUserId < 0 ? 0 : request.CustomerUserId;
        var status = NormalizeStatus(request.Status);
        var notes = (request.Notes ?? string.Empty).Trim();
        var number = Clip(request.QuoteNumber, 32);
        var lineDesc = Clip(request.LineDescription, 512);
        var qty = request.LineQty < 0.001m ? 1 : request.LineQty;
        var price = request.LineUnitPrice < 0 ? 0 : request.LineUnitPrice;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (oppId > 0)
            {
                await using var select = connection.CreateCommand();
                select.CommandText = ErpDb.Positional(
                    "SELECT `lead_id`, `linked_user_id` FROM `epc_crm_opportunities` WHERE `id`=? LIMIT 1");
                ErpDb.AddParameters(select, oppId);
                await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (leadId <= 0)
                    {
                        leadId = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                    }

                    if (custId <= 0)
                    {
                        custId = reader.IsDBNull(1) ? 0L : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
                    }
                }
            }

            long quoteId;
            if (request.Id > 0)
            {
                var exists = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_quotes` WHERE `id`=?"),
                    cancellationToken,
                    request.Id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Quote was not found.");
                }

                if (number.Length == 0)
                {
                    number = Clip(
                        await ErpDb.StringAsync(
                            connection, null,
                            ErpDb.Positional("SELECT `quote_number` FROM `epc_crm_quotes` WHERE `id`=?"),
                            cancellationToken, request.Id),
                        32);
                }

                if (notes.Length == 0)
                {
                    notes = (await ErpDb.StringAsync(
                        connection, null,
                        ErpDb.Positional("SELECT `notes` FROM `epc_crm_quotes` WHERE `id`=?"),
                        cancellationToken, request.Id) ?? string.Empty).Trim();
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_crm_quotes`
                        SET `opportunity_id`=?, `lead_id`=?, `customer_user_id`=?, `quote_number`=?, `status`=?, `notes`=?, `time_updated`=?
                        WHERE `id`=?
                        """),
                    cancellationToken,
                    oppId, leadId, custId, number, status, notes, now, request.Id).ConfigureAwait(false);
                quoteId = request.Id;
            }
            else
            {
                if (number.Length == 0)
                {
                    var count = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_quotes`"),
                        cancellationToken).ConfigureAwait(false);
                    number = NextQuoteNumber(count + 1, DateTimeOffset.UtcNow);
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        INSERT INTO `epc_crm_quotes`
                        (`opportunity_id`, `lead_id`, `customer_user_id`, `quote_number`, `status`, `subtotal`, `notes`, `time_created`, `time_updated`)
                        VALUES (?, ?, ?, ?, ?, 0, ?, ?, ?)
                        """),
                    cancellationToken,
                    oppId, leadId, custId, number, status, notes, now, now).ConfigureAwait(false);
                quoteId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            }

            if (lineDesc.Length > 0)
            {
                var sort = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT IFNULL(MAX(`sort_order`), 0) + 1 FROM `epc_crm_quote_lines` WHERE `quote_id`=?"),
                    cancellationToken,
                    quoteId).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        INSERT INTO `epc_crm_quote_lines` (`quote_id`, `description`, `qty`, `unit_price`, `sort_order`)
                        VALUES (?, ?, ?, ?, ?)
                        """),
                    cancellationToken,
                    quoteId, lineDesc, qty, price, sort).ConfigureAwait(false);
            }

            var sum = await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(SUM(`qty` * `unit_price`), 0) FROM `epc_crm_quote_lines` WHERE `quote_id`=?"),
                cancellationToken,
                quoteId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_crm_quotes` SET `subtotal`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                sum, now, quoteId).ConfigureAwait(false);

            return ErpSimpleWriteResult.Ok("Quote saved", quoteId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM quote table is missing — schema-ensure stays Classic.");
        }
    }
}
