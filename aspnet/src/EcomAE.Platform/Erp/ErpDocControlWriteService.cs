using System.Data.Common;
using System.Globalization;
using System.Net.Mail;
using EcomAE.Platform.Cp;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP twins for the document-control ajax pair still on dry-run:
/// <c>sync_einvoice_seller</c> → <c>epc_dc_sync_seller_from_einvoice</c> and
/// <c>docx_run_reminders</c> → <c>epc_docx_run_reminders</c>.
/// Schema-ensure mirrors PHP (epc_document_control_schema.php / epc_erp_doc_expiry.php).
/// Reminder mail goes through the tenant SMTP path (<see cref="ICpTenantEmailWriteService"/>)
/// instead of PHP <c>mail()</c>; as in PHP, a failed/absent transport skips the document
/// (no reminder rows written, retried next run).
/// </summary>
public interface IErpDocControlWriteService
{
    Task<ErpSimpleWriteResult> SyncSellerFromEinvoiceAsync(int expectedVersion, CancellationToken cancellationToken = default);
    Task<ErpDocxRunRemindersResult> RunRemindersAsync(string tenantHost, CancellationToken cancellationToken = default);
}

public sealed record ErpDocxRunRemindersResult(
    ErpSimpleWriteResult Result,
    int Checked,
    int Sent,
    int Skipped,
    IReadOnlyList<ErpDocxReminderDetail> Details);

public sealed record ErpDocxReminderDetail(long DocId, string Recipient, int Threshold, long DaysLeft, IReadOnlyList<int> Covered);

public sealed class ErpDocControlWriteService : IErpDocControlWriteService
{
    private const string LegalFooter =
        "This document is a Tax Invoice issued in accordance with UAE Federal Tax Authority (FTA) "
        + "requirements and UAE e-invoicing (PINT-AE). Seller VAT Registration Number (TRN) must appear on all tax invoices. "
        + "Retain records for a minimum of 5 years.";

    // PHP epc_einvoice_default_settings() keys starting with seller_ plus the payment keys.
    private static readonly string[] SellerSettingKeys =
    {
        "seller_name", "seller_trn", "seller_tin", "seller_legal_reg_no", "seller_legal_reg_type",
        "seller_authority_name", "seller_address_line1", "seller_city", "seller_emirate",
        "seller_country_code", "seller_phone", "seller_email", "seller_bank_account",
        "payment_means_code", "payment_terms",
    };

    private static readonly (string Key, string Default)[] EinvoiceSettingDefaults =
    {
        ("seller_name", "ePartsCart LLC"), ("seller_trn", ""), ("seller_tin", ""), ("seller_legal_reg_no", ""),
        ("seller_legal_reg_type", "TL"), ("seller_authority_name", "Dubai Economy and Tourism"),
        ("seller_address_line1", ""), ("seller_city", "Dubai"), ("seller_emirate", "Dubai"),
        ("seller_country_code", "AE"), ("seller_phone", ""), ("seller_email", ""), ("seller_bank_account", ""),
        ("payment_means_code", "30"), ("payment_terms", "Within 7 days"),
        ("asp_name", ""), ("asp_api_mode", "manual"), ("asp_api_url", ""), ("asp_api_key", ""),
        ("einvoice_enabled", "1"), ("default_doc_category", "tax_invoice"),
        ("default_payment_due_days", "7"), ("auto_validate", "1"),
    };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpTenantEmailWriteService _mail;
    private readonly TimeProvider _clock;

    public ErpDocControlWriteService(
        IErpWriteConnectionFactory connections,
        ICpTenantEmailWriteService mail,
        TimeProvider clock)
    {
        _connections = connections;
        _mail = mail;
        _clock = clock;
    }

    /// <summary>PHP epc_dc_sync_seller_from_einvoice — fill-empty merge of e-invoice seller settings into epc_document_company.</summary>
    public async Task<ErpSimpleWriteResult> SyncSellerFromEinvoiceAsync(int expectedVersion, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        // MySQL DDL commits implicitly — schema ensure runs before the transaction (as PHP).
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureDocCompanySchemaAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await EnsureEinvoiceSettingsAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await EnsureDocCompanyRowVersionColumnAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var co = await RowAsync(connection, tx, "SELECT * FROM `epc_document_company` WHERE `id` = 1 LIMIT 1", cancellationToken).ConfigureAwait(false)
                ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            var seller = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in SellerSettingKeys)
            {
                var v = await ErpDb.StringAsync(
                    connection, tx,
                    ErpDb.Positional("SELECT `setting_value` FROM `epc_einvoice_settings` WHERE `setting_key` = ? LIMIT 1"),
                    cancellationToken, key).ConfigureAwait(false);
                seller[key] = v ?? EinvoiceSettingDefaults.First(d => d.Key == key).Default;
            }

            string Fill(object? current, string incoming)
            {
                var c = (current?.ToString() ?? string.Empty).Trim();
                return c.Length > 0 ? c : incoming.Trim();
            }

            var legalFooter = (co.TryGetValue("legal_footer", out var lf) ? lf?.ToString() : string.Empty)?.Trim() ?? string.Empty;
            if (legalFooter.Length == 0)
            {
                legalFooter = LegalFooter;
            }

            // PHP epc_erp_version_assert_and_bump on epc_document_company id=1.
            await BumpRowVersionAsync(connection, tx, expectedVersion, cancellationToken).ConfigureAwait(false);

            var now = _clock.GetUtcNow().ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection, tx,
                "UPDATE `epc_document_company` SET `legal_name`=@p0, `trade_name`=@p1, `address_line1`=@p2, `city`=@p3, `country`=@p4,"
                + " `trn`=@p5, `phone`=@p6, `email`=@p7, `website`=@p8, `bank_name`=@p9, `bank_iban`=@p10, `legal_footer`=@p11, `updated_at`=@p12 WHERE `id` = @p13",
                cancellationToken,
                Fill(co.TryGetValue("legal_name", out var ln) ? ln : null, seller["seller_name"]),
                Fill(co.TryGetValue("trade_name", out var tn) ? tn : null, seller["seller_name"]),
                Fill(co.TryGetValue("address_line1", out var a1) ? a1 : null, seller["seller_address_line1"]),
                Fill(co.TryGetValue("city", out var ci) ? ci : null, seller["seller_city"]),
                Fill(co.TryGetValue("country", out var cn) ? cn : null, "United Arab Emirates"),
                Fill(co.TryGetValue("trn", out var trn) ? trn : null, seller["seller_trn"]),
                Fill(co.TryGetValue("phone", out var ph) ? ph : null, seller["seller_phone"]),
                Fill(co.TryGetValue("email", out var em) ? em : null, seller["seller_email"]),
                Fill(co.TryGetValue("website", out var ws) ? ws : null, "https://www.epartscart.com"),
                Fill(co.TryGetValue("bank_name", out var bn) ? bn : null, "Bank account"),
                Fill(co.TryGetValue("bank_iban", out var bi) ? bi : null, seller["seller_bank_account"]),
                legalFooter,
                now,
                1).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Imported seller details from E-Invoicing settings", 1, 1);
        }
        catch (ErpWriteException)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    /// <summary>PHP epc_docx_run_reminders — one e-mail per document covering all due thresholds, logged once per (doc, threshold).</summary>
    public async Task<ErpDocxRunRemindersResult> RunRemindersAsync(string tenantHost, CancellationToken cancellationToken = default)
    {
        var fail = new ErpDocxRunRemindersResult(ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured."), 0, 0, 0, Array.Empty<ErpDocxReminderDetail>());
        if (!_connections.IsConfigured)
        {
            return fail;
        }

        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureDocExpirySchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        // PHP: epc_docx_run_reminders($db, epc_erp_active_company_id($db))
        var companyId = await ErpFinAdvancedCompany.ResolveAsync(connection, 0, cancellationToken).ConfigureAwait(false);
        var rows = await DocListAsync(connection, companyId, now, cancellationToken).ConfigureAwait(false);
        var checkedCount = 0;
        var sent = 0;
        var skipped = 0;
        var details = new List<ErpDocxReminderDetail>();

        foreach (var row in rows)
        {
            var docId = Convert.ToInt64(row["id"], CultureInfo.InvariantCulture);
            checkedCount++;
            var expiry = Convert.ToInt64(row["expiry_date"], CultureInfo.InvariantCulture);
            if (expiry <= 0)
            {
                skipped++;
                continue;
            }

            var reminderDays = ParseReminderDays(row["reminder_days"]?.ToString() ?? string.Empty);
            var already = await RemindersSentAsync(connection, docId, cancellationToken).ConfigureAwait(false);
            var due = DueThresholds(expiry, reminderDays, now, already);
            if (due.Count == 0)
            {
                skipped++;
                continue;
            }

            var recipient = ReminderRecipient(row, tenantHost);
            if (recipient.Length == 0)
            {
                skipped++;
                continue;
            }

            var daysLeft = expiry <= 0 ? 0 : (long)Math.Floor((expiry - now) / 86400.0);
            var urgent = due[0];
            var when = daysLeft < 0
                ? "EXPIRED " + Math.Abs(daysLeft).ToString(CultureInfo.InvariantCulture) + " day(s) ago"
                : "expires in " + daysLeft.ToString(CultureInfo.InvariantCulture) + " day(s)";
            var title = (row["title"]?.ToString() ?? string.Empty);
            var subject = "[Document expiry] " + (title.Length > 0 ? title : row["doc_type"]?.ToString() ?? string.Empty) + " — " + when;
            var body = "Document expiry reminder\n\n"
                + "Document : " + title + "\n"
                + "Type     : " + (row["doc_type"]?.ToString() ?? string.Empty) + "\n"
                + "Category : " + (row["category"]?.ToString() ?? string.Empty) + "\n"
                + "Ref no   : " + (row["ref_no"]?.ToString() ?? string.Empty) + "\n"
                + "Owner    : " + (row["owner"]?.ToString() ?? string.Empty) + "\n"
                + "Issuer   : " + (row["issuer"]?.ToString() ?? string.Empty) + "\n"
                + "Expiry   : " + (expiry > 0 ? DateTimeOffset.FromUnixTimeSeconds(expiry).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "—") + "\n"
                + "Status   : " + when.ToUpperInvariant() + "\n\n"
                + "Please action the renewal before the expiry date.\n";

            var ok = await SendReminderAsync(recipient, subject, body, cancellationToken).ConfigureAwait(false);
            if (ok)
            {
                await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    foreach (var d in due)
                    {
                        await TryExecAsync(
                            connection, tx,
                            "INSERT IGNORE INTO `epc_erp_doc_expiry_reminders` (`doc_id`,`threshold_days`,`days_left`,`recipient`,`channel`,`sent_at`) VALUES (@p0,@p1,@p2,@p3,'email',@p4)",
                            cancellationToken, docId, d, daysLeft, recipient, now).ConfigureAwait(false);
                    }

                    await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (DbException)
                {
                    await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }

                sent++;
                details.Add(new ErpDocxReminderDetail(docId, recipient, urgent, daysLeft, due));
            }
            else
            {
                skipped++;
            }
        }

        return new ErpDocxRunRemindersResult(
            new ErpSimpleWriteResult(
                true, "ok",
                "Reminders: " + sent.ToString(CultureInfo.InvariantCulture) + " sent, "
                    + checkedCount.ToString(CultureInfo.InvariantCulture) + " checked, "
                    + skipped.ToString(CultureInfo.InvariantCulture) + " not due",
                0, sent),
            checkedCount, sent, skipped, details);
    }

    private async Task<bool> SendReminderAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        // PHP @mail() hand-off: reminder rows are only written when the send succeeds,
        // so unconfigured/failed transport lands in skipped (retried on the next run).
        var sent = await _mail.SendAsync(new CpTenantEmailMessage(to, subject, body), cancellationToken).ConfigureAwait(false);
        return sent.Succeeded;
    }

    /// <summary>PHP epc_docx_reminder_recipient — owner email, else epc_admin_notify_email ≈ admin@host (www. stripped).</summary>
    private static string ReminderRecipient(Dictionary<string, object?> row, string tenantHost)
    {
        var email = (row["owner_email"]?.ToString() ?? string.Empty).Trim();
        if (email.Length > 0 && MailAddress.TryCreate(email, out _))
        {
            return email;
        }

        var host = tenantHost.Trim();
        if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        return host.Length > 0 ? "admin@" + host : string.Empty;
    }

    private async Task<List<Dictionary<string, object?>>> DocListAsync(DbConnection c, long companyId, long now, CancellationToken ct)
    {
        var sql = "SELECT * FROM `epc_erp_doc_expiry` WHERE `active`=1";
        var args = new List<object>();
        if (companyId > 0)
        {
            sql += " AND `company_id`=@p0";
            args.Add(companyId);
        }

        sql += " ORDER BY (`expiry_date`=0), `expiry_date` ASC, `id` DESC";
        return await RowsAsync(c, null, sql, ct, args.ToArray()).ConfigureAwait(false);
    }

    private async Task<List<int>> RemindersSentAsync(DbConnection c, long docId, CancellationToken ct)
    {
        var rows = await RowsAsync(
            c, null,
            ErpDb.Positional("SELECT `threshold_days` FROM `epc_erp_doc_expiry_reminders` WHERE `doc_id`=?"),
            ct, docId).ConfigureAwait(false);
        return rows.Select(r => Convert.ToInt32(r["threshold_days"], CultureInfo.InvariantCulture)).ToList();
    }

    /// <summary>PHP epc_docx_parse_reminder_days — positive ints, distinct, descending.</summary>
    private static List<int> ParseReminderDays(string csv)
    {
        var set = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        foreach (var part in csv.Split(',', StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
            {
                set.Add(n);
            }
        }

        return set.ToList();
    }

    /// <summary>PHP epc_docx_due_thresholds — unsent thresholds where days_left &lt;= threshold, ascending.</summary>
    private static List<int> DueThresholds(long expiry, List<int> reminderDays, long now, List<int> sent)
    {
        if (expiry <= 0)
        {
            return new List<int>();
        }

        var left = (long)Math.Floor((expiry - now) / 86400.0);
        var sentSet = sent.ToHashSet();
        var due = reminderDays.Where(d => d > 0 && left <= d && !sentSet.Contains(d)).ToList();
        due.Sort();
        return due;
    }

    /// <summary>epc_erp_schema_add_column_if_missing: row_version int NOT NULL DEFAULT 1 (DDL — outside tx).</summary>
    private async Task EnsureDocCompanyRowVersionColumnAsync(DbConnection c, CancellationToken ct)
    {
        var hasCol = await ErpFinAdvancedCompany.ColumnExistsAsync(c, "epc_document_company", "row_version", ct).ConfigureAwait(false);
        if (!hasCol)
        {
            await TryExecAsync(c, null, "ALTER TABLE `epc_document_company` ADD COLUMN `row_version` int(11) NOT NULL DEFAULT 1", ct).ConfigureAwait(false);
        }
    }

    private async Task BumpRowVersionAsync(DbConnection c, DbTransaction tx, int expectedVersion, CancellationToken ct)
    {
        if (expectedVersion <= 0)
        {
            await ErpDb.ExecuteAsync(c, tx, "UPDATE `epc_document_company` SET `row_version` = `row_version` + 1 WHERE `id` = 1", ct).ConfigureAwait(false);
            return;
        }

        var bumped = await ErpDb.ExecuteAsync(
            c, tx,
            ErpDb.Positional("UPDATE `epc_document_company` SET `row_version` = `row_version` + 1 WHERE `id` = ? AND `row_version` = ?"),
            ct, 1, expectedVersion).ConfigureAwait(false);
        if (bumped >= 1)
        {
            return;
        }

        var current = await ErpDb.LongAsync(c, tx, "SELECT `row_version` FROM `epc_document_company` WHERE `id` = 1 LIMIT 1", ct).ConfigureAwait(false);
        throw new ErpWriteException(
            "Version conflict — another user saved this record"
            + (current > 0
                ? " (their version " + current.ToString(CultureInfo.InvariantCulture)
                    + ", yours " + expectedVersion.ToString(CultureInfo.InvariantCulture) + ")"
                : string.Empty)
            + ". Reload and re-apply your changes.");
    }

    private async Task EnsureDocCompanySchemaAsync(DbConnection c, DbTransaction? tx, CancellationToken ct)
    {
        await TryExecAsync(
            c, tx,
            "CREATE TABLE IF NOT EXISTS `epc_document_company` ("
            + "`id` TINYINT UNSIGNED NOT NULL DEFAULT 1 PRIMARY KEY,"
            + "`legal_name` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`trade_name` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`address_line1` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`address_line2` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`city` VARCHAR(120) NOT NULL DEFAULT '',"
            + "`country` VARCHAR(80) NOT NULL DEFAULT 'United Arab Emirates',"
            + "`trn` VARCHAR(32) NOT NULL DEFAULT '',"
            + "`phone` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`email` VARCHAR(120) NOT NULL DEFAULT '',"
            + "`website` VARCHAR(120) NOT NULL DEFAULT '',"
            + "`logo_path` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`bank_name` VARCHAR(120) NOT NULL DEFAULT '',"
            + "`bank_iban` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`legal_footer` TEXT NULL,"
            + "`updated_at` INT NOT NULL DEFAULT 0"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            ct).ConfigureAwait(false);

        var count = await ErpDb.LongAsync(c, tx, "SELECT COUNT(*) FROM `epc_document_company`", ct).ConfigureAwait(false);
        if (count == 0)
        {
            await TryExecAsync(
                c, tx,
                "INSERT INTO `epc_document_company`"
                + " (`id`, `legal_name`, `trade_name`, `address_line1`, `city`, `country`, `trn`, `phone`, `email`, `website`, `legal_footer`, `updated_at`)"
                + " VALUES (1, '', '', '', 'Dubai', 'United Arab Emirates', '', '', '', '',"
                + " 'This document is issued in accordance with UAE Federal Tax Authority (FTA) requirements. VAT Registration Number (TRN) must appear on all tax invoices. Retain records for minimum 5 years.',"
                + " UNIX_TIMESTAMP())",
                ct).ConfigureAwait(false);
        }
    }

    private async Task EnsureEinvoiceSettingsAsync(DbConnection c, DbTransaction? tx, CancellationToken ct)
    {
        await TryExecAsync(
            c, tx,
            "CREATE TABLE IF NOT EXISTS `epc_einvoice_settings` ("
            + "`setting_key` varchar(64) NOT NULL,"
            + "`setting_value` text,"
            + "`time_updated` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`setting_key`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='UAE e-invoice seller & ASP settings'",
            ct).ConfigureAwait(false);
        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        foreach (var (key, value) in EinvoiceSettingDefaults)
        {
            await TryExecAsync(
                c, tx,
                ErpDb.Positional("INSERT INTO `epc_einvoice_settings` (`setting_key`, `setting_value`, `time_updated`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `setting_key` = `setting_key`"),
                ct, key, value, now).ConfigureAwait(false);
        }
    }

    private async Task EnsureDocExpirySchemaAsync(DbConnection c, CancellationToken ct)
    {
        await TryExecAsync(
            c, null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_doc_expiry` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`company_id` int(11) NOT NULL DEFAULT 0,"
            + "`category` varchar(24) NOT NULL DEFAULT 'other',"
            + "`doc_type` varchar(120) NOT NULL DEFAULT '',"
            + "`title` varchar(200) NOT NULL DEFAULT '',"
            + "`ref_no` varchar(120) NOT NULL DEFAULT '',"
            + "`owner` varchar(200) NOT NULL DEFAULT '',"
            + "`owner_email` varchar(200) NOT NULL DEFAULT '',"
            + "`issuer` varchar(200) NOT NULL DEFAULT '',"
            + "`issue_date` int(11) NOT NULL DEFAULT 0,"
            + "`expiry_date` int(11) NOT NULL DEFAULT 0,"
            + "`reminder_days` varchar(120) NOT NULL DEFAULT '90,60,30,7',"
            + "`attachment_path` varchar(255) NOT NULL DEFAULT '',"
            + "`note` text,"
            + "`source_module` varchar(32) NOT NULL DEFAULT '',"
            + "`source_ref_id` int(11) NOT NULL DEFAULT 0,"
            + "`active` tinyint(1) NOT NULL DEFAULT 1,"
            + "`time_created` int(11) NOT NULL DEFAULT 0,"
            + "`time_updated` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`),"
            + "KEY `x_company` (`company_id`),"
            + "KEY `x_expiry` (`expiry_date`),"
            + "KEY `x_source` (`source_module`,`source_ref_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Document expiry register'",
            ct).ConfigureAwait(false);
        await TryExecAsync(
            c, null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_doc_expiry_reminders` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`doc_id` int(11) NOT NULL,"
            + "`threshold_days` int(11) NOT NULL,"
            + "`days_left` int(11) NOT NULL DEFAULT 0,"
            + "`recipient` varchar(200) NOT NULL DEFAULT '',"
            + "`channel` varchar(16) NOT NULL DEFAULT 'email',"
            + "`sent_at` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`),"
            + "UNIQUE KEY `x_doc_threshold` (`doc_id`,`threshold_days`),"
            + "KEY `x_doc` (`doc_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Document expiry reminder log'",
            ct).ConfigureAwait(false);
    }

    /// <summary>ErpDb.TryExecuteAsync has no transaction overload — swallow DbException like PHP CREATE/ALTER best-effort.</summary>
    private static async Task TryExecAsync(DbConnection c, DbTransaction? tx, string sql, CancellationToken ct, params object[] ps)
    {
        try
        {
            await ErpDb.ExecuteAsync(c, tx, sql, ct, ps).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static async Task<Dictionary<string, object?>?> RowAsync(DbConnection c, DbTransaction? tx, string sql, CancellationToken ct, params object[] ps)
    {
        var rows = await RowsAsync(c, tx, sql, ct, ps).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    private static async Task<List<Dictionary<string, object?>>> RowsAsync(DbConnection c, DbTransaction? tx, string sql, CancellationToken ct, params object[] ps)
    {
        var list = new List<Dictionary<string, object?>>();
        await using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        ErpDb.AddParameters(cmd, ps);
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < r.FieldCount; i++)
            {
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            }

            list.Add(row);
        }

        return list;
    }
}
