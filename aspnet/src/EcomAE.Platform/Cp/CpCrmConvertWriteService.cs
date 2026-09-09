using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> <c>convert_lead</c> twin of <c>epc_crm_convert_lead_to_opportunity</c>.
/// Schema-ensure, quote email, and send stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmConvertWriteService
{
    Task<ErpSimpleWriteResult> ConvertLeadAsync(
        long leadId,
        long ownerUserId,
        CancellationToken cancellationToken = default);
}

public sealed class CpCrmConvertWriteService : ICpCrmConvertWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpCrmConvertWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ConvertLeadAsync(
        long leadId,
        long ownerUserId,
        CancellationToken cancellationToken = default)
    {
        if (leadId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Lead id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var select = connection.CreateCommand();
            select.CommandText = ErpDb.Positional(
                """
                SELECT `company`, `expected_value`
                FROM `epc_crm_leads`
                WHERE `id`=? AND `active`=1
                LIMIT 1
                """);
            ErpDb.AddParameters(select, leadId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("not_found", "Lead not found");
            }

            var company = reader.IsDBNull(0) ? "" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
            var amount = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
            await reader.DisposeAsync().ConfigureAwait(false);

            if (amount < 0)
            {
                amount = 0;
            }

            var title = "Opportunity — " + company.Trim();
            if (title.Length > 255)
            {
                title = title[..255];
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var close = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
            var owner = ownerUserId > 0 ? ownerUserId : 0;
            var notes = "Converted from lead #" + leadId.ToString(CultureInfo.InvariantCulture);

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_crm_opportunities`
                    (`lead_id`, `title`, `stage`, `amount`, `probability`, `close_date`, `owner_user_id`, `linked_user_id`, `notes`, `time_created`, `time_updated`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                leadId, title, "qualified", amount, 30, close, owner, 0, notes, now, now);
            var oppId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_crm_leads`
                    SET `status`='converted', `time_updated`=?
                    WHERE `id`=?
                    """),
                cancellationToken,
                now, leadId);

            return ErpSimpleWriteResult.Ok(
                "Lead converted to opportunity #" + oppId.ToString(CultureInfo.InvariantCulture),
                oppId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM tables are missing — schema-ensure stays Classic.");
        }
    }
}
