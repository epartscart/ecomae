using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Erp;

/// <summary>
/// WhatsApp Click-to-Chat bulk share: recipients come from <c>epc_erp_contacts</c>,
/// templates persist in <c>epc_erp_whatsapp_templates</c>. The page opens
/// wa.me links one recipient at a time — nothing is sent automatically.
/// </summary>
public interface IErpWhatsappShareService
{
    Task<ErpWhatsappShareSnapshot> ReadAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveTemplateAsync(
        string name,
        string body,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteTemplateAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveReminderDaysAsync(
        int reminderDays,
        CancellationToken cancellationToken = default);
}

public sealed record ErpWhatsappRecipient(
    long Id,
    string Name,
    string Company,
    string Phone,
    string Digits,
    string PartyType,
    string ExpiryDate);

public sealed record ErpWhatsappTemplate(long Id, string Name, string Body);

public sealed record ErpWhatsappShareSnapshot(
    IReadOnlyList<ErpWhatsappRecipient> Recipients,
    IReadOnlyList<ErpWhatsappTemplate> Templates,
    int ReminderDays,
    string Source,
    string Message);

public sealed class ErpWhatsappShareService : IErpWhatsappShareService
{
    private static readonly Regex NonDigit = new(@"[^0-9]", RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpWhatsappShareService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>Digits-only international number for wa.me; UAE country code assumed when local.</summary>
    public static string NormalizePhone(string? phone, string? countryCode)
    {
        var digits = NonDigit.Replace(phone ?? string.Empty, string.Empty);
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        var code = NonDigit.Replace(countryCode ?? string.Empty, string.Empty);
        if (code.Length == 0)
        {
            code = DialCode(countryCode);
        }

        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }
        else if (digits.StartsWith("0", StringComparison.Ordinal))
        {
            digits = code + digits[1..];
        }
        else if (digits.Length <= 9 && !digits.StartsWith(code, StringComparison.Ordinal)
                 || digits.Length == 10 && code == "1")
        {
            // Local number missing its dial prefix (10-digit US/CA local included);
            // a 10+ digit number that already carries a different prefix stays as-is.
            digits = code + digits;
        }

        return digits.Length is >= 10 and <= 15 ? digits : string.Empty;
    }

    /// <summary>ISO-2 country code → ITU dialing prefix; UAE default.</summary>
    private static string DialCode(string? iso)
        => (iso ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "AE" => "971", "QA" => "974", "SA" => "966", "BH" => "973", "KW" => "965",
            "OM" => "968", "IN" => "91", "PK" => "92", "PH" => "63", "BD" => "880",
            "GB" => "44", "US" or "CA" => "1", "EG" => "20", "JO" => "962", "LB" => "961",
            "TR" => "90", "LK" => "94", "NP" => "977", "ID" => "62", "MY" => "60",
            _ => "971",
        };

    public static string Personalize(string template, ErpWhatsappRecipient recipient, DateOnly date)
        => (template ?? string.Empty)
            .Replace("{TenantName}", string.IsNullOrWhiteSpace(recipient.Name) ? recipient.Company : recipient.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{MobileNumber}", recipient.Phone, StringComparison.OrdinalIgnoreCase)
            .Replace("{ExpiryDate}", recipient.ExpiryDate, StringComparison.OrdinalIgnoreCase)
            .Replace("{Date}", date.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase);

    private static async Task EnsureTemplatesAsync(DbConnection connection, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_whatsapp_templates` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `name` varchar(190) NOT NULL DEFAULT '',
                `body` text,
                `active` tinyint(1) NOT NULL DEFAULT 1,
                `time_created` int(11) NOT NULL DEFAULT 0,
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_whatsapp_settings` (
                `key` varchar(64) NOT NULL,
                `value` varchar(255) NOT NULL DEFAULT '',
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, ct).ConfigureAwait(false);
    }

    public async Task<ErpWhatsappShareSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], [], 90, "migration", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureTemplatesAsync(connection, cancellationToken).ConfigureAwait(false);

        var recipients = new List<ErpWhatsappRecipient>();
        var templates = new List<ErpWhatsappTemplate>();
        var reminderDays = 90;
        try
        {
            if (await ErpDb.TableKnownAsync(connection, "epc_erp_contacts", cancellationToken).ConfigureAwait(false))
            {
                await using var command = connection.CreateCommand();
                var hasRe = await ErpDb.TableKnownAsync(connection, "epc_erp_re_tenants", cancellationToken).ConfigureAwait(false)
                    && await ErpDb.TableKnownAsync(connection, "epc_erp_re_leases", cancellationToken).ConfigureAwait(false);
                var expiryCol = hasRe
                    ? ", IFNULL((SELECT DATE_FORMAT(MAX(l.`end_date`),'%Y-%m-%d') FROM `epc_erp_re_tenants` t JOIN `epc_erp_re_leases` l ON l.`tenant_id`=t.`id` WHERE t.`name`=epc_erp_contacts.`name` AND (IFNULL(epc_erp_contacts.`email`,'')='' OR t.`email`=epc_erp_contacts.`email`)),'')"
                    : ", ''";
                var hasCountry = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "country_code", cancellationToken).ConfigureAwait(false);
                var countryCol = hasCountry ? "epc_erp_contacts.`country_code`" : "''";
                command.CommandText =
                    "SELECT epc_erp_contacts.`id`,epc_erp_contacts.`name`,epc_erp_contacts.`company`,epc_erp_contacts.`phone`,"
                    + countryCol
                    + ",epc_erp_contacts.`party_type`"
                    + expiryCol
                    + " FROM `epc_erp_contacts`"
                    + " WHERE `active`=1 AND `phone` IS NOT NULL AND `phone` <> ''"
                    + " ORDER BY epc_erp_contacts.`name` LIMIT 500";
                var seen = new HashSet<long>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var phone = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
                    var digits = NormalizePhone(phone, reader.IsDBNull(4) ? null : reader.GetString(4));
                    if (!seen.Add(reader.GetInt64(0)))
                    {
                        continue;
                    }
                    recipients.Add(new(
                        reader.GetInt64(0),
                        reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        phone,
                        digits,
                        reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                        reader.IsDBNull(6) ? string.Empty : reader.GetString(6)));
                }
            }

            reminderDays = (int)await ErpDb.LongAsync(connection, null, "SELECT CAST(IFNULL(`value`,'90') AS SIGNED) FROM `epc_erp_whatsapp_settings` WHERE `key`='reminder_days' LIMIT 1", cancellationToken).ConfigureAwait(false);
            if (reminderDays <= 0)
            {
                reminderDays = 90;
            }

            await using var tplCommand = connection.CreateCommand();
            tplCommand.CommandText = "SELECT `id`,`name`,`body` FROM `epc_erp_whatsapp_templates` WHERE `active`=1 ORDER BY `name`";
            await using var tplReader = await tplCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await tplReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                templates.Add(new(
                    tplReader.GetInt64(0),
                    tplReader.IsDBNull(1) ? string.Empty : tplReader.GetString(1),
                    tplReader.IsDBNull(2) ? string.Empty : tplReader.GetString(2)));
            }
        }
        catch (DbException exception)
        {
            return new([], [], 90, "database-error", exception.Message);
        }

        return new(recipients, templates, reminderDays, "database", string.Empty);
    }

    public async Task<ErpSimpleWriteResult> SaveTemplateAsync(
        string name,
        string body,
        CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return new ErpSimpleWriteResult(false, "name_required", "Template name is required.", 0, 0);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureTemplatesAsync(connection, cancellationToken).ConfigureAwait(false);
        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_whatsapp_templates` (`name`,`body`,`time_created`,`time_updated`) VALUES (?,?,UNIX_TIMESTAMP(),UNIX_TIMESTAMP())"),
            cancellationToken,
            name,
            body ?? string.Empty).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "saved", "Template saved.", 0, writes);
    }

    public async Task<ErpSimpleWriteResult> DeleteTemplateAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureTemplatesAsync(connection, cancellationToken).ConfigureAwait(false);
        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_whatsapp_templates` SET `active`=0, `time_updated`=UNIX_TIMESTAMP() WHERE `id`=?"),
            cancellationToken,
            id).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "deleted", "Template removed.", 0, writes);
    }

    public async Task<ErpSimpleWriteResult> SaveReminderDaysAsync(
        int reminderDays,
        CancellationToken cancellationToken = default)
    {
        if (reminderDays < 1 || reminderDays > 365)
        {
            return new ErpSimpleWriteResult(false, "invalid_days", "Reminder days must be 1–365.", 0, 0);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureTemplatesAsync(connection, cancellationToken).ConfigureAwait(false);
        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_whatsapp_settings` (`key`,`value`,`time_updated`) VALUES ('reminder_days',?,UNIX_TIMESTAMP()) ON DUPLICATE KEY UPDATE `value`=VALUES(`value`),`time_updated`=VALUES(`time_updated`)"),
            cancellationToken,
            reminderDays.ToString(System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "saved", "Reminder window saved.", 0, writes);
    }
}
