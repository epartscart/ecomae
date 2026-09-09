using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> twins of <c>epc_crm_save_lead</c> and <c>epc_crm_delete_lead</c>.
/// Quote email and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmWriteService
{
    Task<ErpSimpleWriteResult> SaveLeadAsync(
        long id,
        string? company,
        string? contactName,
        string? email,
        string? phone,
        string? source,
        string? status,
        long ownerUserId,
        decimal expectedValue,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteLeadAsync(
        long id,
        CancellationToken cancellationToken = default);
}

public sealed class CpCrmWriteService : ICpCrmWriteService
{
    public static readonly HashSet<string> LeadStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "new", "contacted", "qualified", "unqualified", "converted",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpCrmWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveLeadAsync(
        long id,
        string? company,
        string? contactName,
        string? email,
        string? phone,
        string? source,
        string? status,
        long ownerUserId,
        decimal expectedValue,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Lead id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var rowCompany = Clip(company, 255);
        var rowContact = Clip(contactName, 255);
        var rowEmail = Clip(email, 255);
        var rowPhone = Clip(phone, 64);
        var rowSource = Clip(source ?? "web", 64);

        var rowStatus = (status ?? string.Empty).Trim();
        if (!LeadStatuses.Contains(rowStatus))
        {
            rowStatus = "new";
        }
        else
        {
            rowStatus = rowStatus.ToLowerInvariant();
        }

        var owner = ownerUserId > 0 ? ownerUserId : 0;
        var value = expectedValue < 0 ? 0 : expectedValue;
        var rowNotes = (notes ?? string.Empty).Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (id > 0)
            {
                // Open pane omits email/phone and only shows a notes excerpt, so a blank
                // POST must not wipe those columns or steal owner_user_id.
                if (rowEmail.Length == 0)
                {
                    rowEmail = Clip(
                        await ErpDb.StringAsync(
                            connection, null,
                            ErpDb.Positional("SELECT `email` FROM `epc_crm_leads` WHERE `id`=?"),
                            cancellationToken, id),
                        255);
                }

                if (rowPhone.Length == 0)
                {
                    rowPhone = Clip(
                        await ErpDb.StringAsync(
                            connection, null,
                            ErpDb.Positional("SELECT `phone` FROM `epc_crm_leads` WHERE `id`=?"),
                            cancellationToken, id),
                        64);
                }

                if (rowNotes.Length == 0)
                {
                    rowNotes = (await ErpDb.StringAsync(
                        connection, null,
                        ErpDb.Positional("SELECT `notes` FROM `epc_crm_leads` WHERE `id`=?"),
                        cancellationToken, id) ?? string.Empty).Trim();
                }

                if (owner <= 0)
                {
                    owner = await ErpDb.LongAsync(
                        connection, null,
                        ErpDb.Positional("SELECT `owner_user_id` FROM `epc_crm_leads` WHERE `id`=?"),
                        cancellationToken, id);
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_crm_leads`
                        SET `company`=?, `contact_name`=?, `email`=?, `phone`=?, `source`=?, `status`=?,
                            `owner_user_id`=?, `expected_value`=?, `notes`=?, `time_updated`=?
                        WHERE `id`=?
                        """),
                    cancellationToken,
                    rowCompany, rowContact, rowEmail, rowPhone, rowSource, rowStatus,
                    owner, value, rowNotes, now, id);
                return ErpSimpleWriteResult.Ok("Lead saved", id);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_crm_leads`
                    (`company`, `contact_name`, `email`, `phone`, `source`, `status`, `owner_user_id`, `expected_value`, `notes`, `time_created`, `time_updated`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                rowCompany, rowContact, rowEmail, rowPhone, rowSource, rowStatus,
                owner, value, rowNotes, now, now);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Lead saved", created);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM lead table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteLeadAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Lead id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_crm_leads`
                    SET `active`=0, `time_updated`=?
                    WHERE `id`=?
                    """),
                cancellationToken,
                now, id);
            return ErpSimpleWriteResult.Ok("Lead deleted", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM lead table is missing — schema-ensure stays Classic.");
        }
    }

    public static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }
}
