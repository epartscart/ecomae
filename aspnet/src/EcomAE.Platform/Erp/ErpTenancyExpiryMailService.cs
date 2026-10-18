using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpTenancyExpiryMail
{
    Task<ErpTenancyExpiryBook> LoadAsync(DateOnly today, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveTemplateAsync(ErpTenancyExpiryTemplate template, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PrepareAsync(DateOnly today, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PrepareOneAsync(long leaseId, DateOnly today, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SendDueAsync(DateOnly today, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SendOneAsync(long noticeId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> UpdateNoticeAsync(long noticeId, string toEmails, string replyEmails, string subject, string body, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SkipAsync(long noticeId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> FileReplyAsync(long noticeId, string fromEmail, string body, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AttachToNoticeAsync(long noticeId, string title, string docType, Microsoft.AspNetCore.Http.IFormFile? file, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AttachToTenantAsync(long tenantId, string title, string docType, Microsoft.AspNetCore.Http.IFormFile? file, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> IncludeOnNoticeAsync(long noticeId, long documentId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RemoveDocumentAsync(long documentId, CancellationToken cancellationToken = default);

    Task<ErpTenancyDocumentOpen?> OpenAsync(long documentId, CancellationToken cancellationToken = default);
}

public sealed class ErpTenancyExpiryMailService : IErpTenancyExpiryMail
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly Cp.ICpTenantEmailWriteService _mail;

    public ErpTenancyExpiryMailService(IErpWriteConnectionFactory connections, Cp.ICpTenantEmailWriteService mail)
    {
        _connections = connections;
        _mail = mail;
    }

    public async Task<ErpTenancyExpiryBook> LoadAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpTenancyExpiryBook.Empty("The company database is not configured.");
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var template = await TemplateAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var notices = await NoticesAsync(connection, cancellationToken).ConfigureAwait(false);
        var known = notices.Select(notice => notice.LeaseId).ToHashSet();
        var leases = await LeasesAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var waiting = new List<ErpTenancyExpiryWatch>();
        var ended = new List<ErpTenancyExpiryWatch>();
        foreach (var lease in leases)
        {
            if (!ErpTenancyExpiryMail.IsActive(lease.Status) || known.Contains(lease.LeaseId))
            {
                continue;
            }

            var window = ErpTenancyExpiryMail.Window(lease.End, today);
            if (window == ErpTenancyExpiryWindow.Waiting && waiting.Count < 40)
            {
                waiting.Add(Watch(lease, today));
            }
            else if (window == ErpTenancyExpiryWindow.Ended && lease.End >= today.AddDays(-90) && ended.Count < 20)
            {
                ended.Add(Watch(lease, today));
            }
        }

        waiting.Sort((left, right) => left.EndDate.CompareTo(right.EndDate));
        ended.Sort((left, right) => right.EndDate.CompareTo(left.EndDate));
        var tenantIds = notices.Select(notice => notice.TenantId)
            .Concat(waiting.Select(row => row.TenantId))
            .Concat(ended.Select(row => row.TenantId))
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        var documents = await DocumentsAsync(connection, tenantIds, cancellationToken).ConfigureAwait(false);
        var links = await LinksAsync(connection, notices.Select(notice => notice.Id).ToArray(), cancellationToken).ConfigureAwait(false);
        var previews = ErpTenancyExpiryMail.PreviewsFor(leases, template, known, today);
        return new ErpTenancyExpiryBook(template, notices, waiting, ended, "", documents, links, previews);
    }

    public async Task<ErpSimpleWriteResult> SaveTemplateAsync(ErpTenancyExpiryTemplate template, CancellationToken cancellationToken = default)
    {
        var subject = ErpTenancyExpiryMail.CleanSubject(string.IsNullOrWhiteSpace(template.Subject) ? ErpTenancyExpiryMail.DefaultSubject : template.Subject);
        var body = (string.IsNullOrWhiteSpace(template.Body) ? ErpTenancyExpiryMail.DefaultBody : template.Body).Trim();
        if (body.Length > 8000)
        {
            body = body[..8000];
        }

        var reply = ErpTenancyExpiryMail.Join(ErpTenancyExpiryMail.Addresses(template.ReplyEmails));
        return await WriteAsync(async (connection, tx, ct) =>
        {
            var writes = await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_expiry_mail` SET `subject`=?, `body`=?, `reply_emails`=?, `auto_send`=?, `updated_at`=? WHERE `id`=1"),
                ct, subject, body, reply, template.AutoSend ? 1 : 0, ErpTenancyExpiryMail.GulfStamp(DateTime.UtcNow)).ConfigureAwait(false);
            if (writes == 0)
            {
                return ErpSimpleWriteResult.Fail("missing", "The draft is not on file yet.");
            }
            var note = reply.Length == 0
                ? "Draft saved. No landlord reply address is set, so a reply comes back only to the sending mailbox."
                : "Draft saved. A tenant reply goes to " + reply + ".";
            if (template.AutoSend)
            {
                note += " Ready notices are sent when this page is opened.";
            }

            return ErpSimpleWriteResult.Ok(note, 1);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> PrepareAsync(DateOnly today, CancellationToken cancellationToken = default)
        => await WriteAsync(async (connection, tx, ct) =>
        {
            var counts = await PrepareCoreAsync(connection, tx, today, null, false, ct).ConfigureAwait(false);
            var message = counts.Due == 0
                ? "No tenancy is inside the three-month window."
                : counts.Due.ToString(CultureInfo.InvariantCulture) + (counts.Due == 1 ? " tenancy is" : " tenancies are") + " inside the three-month window.";
            return new ErpSimpleWriteResult(true, "ok", message, 0, counts.Inserted);
        }, cancellationToken).ConfigureAwait(false);

    public async Task<ErpSimpleWriteResult> PrepareOneAsync(long leaseId, DateOnly today, CancellationToken cancellationToken = default)
    {
        if (leaseId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the tenancy.");
        }

        return await WriteAsync(async (connection, tx, ct) =>
        {
            var leases = await LeasesAsync(connection, tx, ct).ConfigureAwait(false);
            var lease = leases.FirstOrDefault(item => item.LeaseId == leaseId);
            if (lease is null || !ErpTenancyExpiryMail.IsActive(lease.Status))
            {
                return ErpSimpleWriteResult.Fail("invalid", "That tenancy is not active.");
            }

            if (!ErpTenancyExpiryMail.CanPrepareManually(lease.Status, lease.End, today))
            {
                return ErpSimpleWriteResult.Fail("invalid", "This tenancy is not inside the three-month window yet. The notice is prepared on " + ErpTenancyExpiryMail.NoticeDueOn(lease.End).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".");
            }

            var existing = await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT IFNULL(`id`,0) FROM `epc_erp_re_expiry_notices` WHERE `lease_id`=? LIMIT 1"),
                ct, leaseId).ConfigureAwait(false);
            if (existing > 0)
            {
                return ErpSimpleWriteResult.Ok("This tenancy already has a notice.", existing);
            }

            var counts = await PrepareCoreAsync(connection, tx, today, leaseId, true, ct).ConfigureAwait(false);
            return counts.Inserted > 0
                ? ErpSimpleWriteResult.Ok("The notice is ready to send.", leaseId)
                : ErpSimpleWriteResult.Fail("invalid", "The notice was not prepared.");
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> SendDueAsync(DateOnly today, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(today, cancellationToken).ConfigureAwait(false);
        if (!prepared.Succeeded)
        {
            return prepared;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "The company database is not configured.");
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var ids = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id` FROM `epc_erp_re_expiry_notices` WHERE `status`='ready' ORDER BY `end_date`, `id`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(ReadLong(reader, 0));
            }
        }

        var sent = 0;
        var failed = 0;
        var firstError = "";
        foreach (var id in ids)
        {
            var result = await SendOneAsync(id, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                sent++;
            }
            else
            {
                failed++;
                if (firstError.Length == 0)
                {
                    firstError = result.Message;
                }
            }
        }

        var needs = await ErpDb.LongAsync(connection, null,
            "SELECT COUNT(*) FROM `epc_erp_re_expiry_notices` WHERE `status`='needs_email'",
            cancellationToken).ConfigureAwait(false);
        var message = SendSummary(sent, failed, (int)needs, firstError);
        return failed > 0 && sent == 0
            ? ErpSimpleWriteResult.Fail("send_failed", message)
            : new ErpSimpleWriteResult(true, "ok", message, 0, sent);
    }

    public async Task<ErpSimpleWriteResult> SendOneAsync(long noticeId, CancellationToken cancellationToken = default)
    {
        if (noticeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the notice to send.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "The company database is not configured.");
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        var notice = await NoticeAsync(connection, null, noticeId, cancellationToken).ConfigureAwait(false);
        if (notice is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "That notice is not on file.");
        }

        if (!ErpTenancyExpiryMail.CanSend(notice.Status))
        {
            return ErpSimpleWriteResult.Fail("invalid", "This notice is " + ErpTenancyExpiryMail.StatusLabel(notice.Status).ToLowerInvariant() + " and is not sent again.");
        }

        var to = ErpTenancyExpiryMail.Addresses(notice.ToEmails);
        if (to.Count == 0)
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                "UPDATE `epc_erp_re_expiry_notices` SET `status`='needs_email', `send_error`=? WHERE `id`=?"),
                cancellationToken, "Add at least one tenant email.", noticeId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("needs_email", "Add at least one tenant email before sending.");
        }

        var reply = ErpTenancyExpiryMail.Join(ErpTenancyExpiryMail.Addresses(notice.ReplyEmails));
        var files = await DocumentFilesAsync(connection, noticeId, cancellationToken).ConfigureAwait(false);
        foreach (var file in files)
        {
            if (!File.Exists(file.Path))
            {
                var missing = "The document " + file.Name + " is not in storage, so the notice was not sent.";
                await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                    "UPDATE `epc_erp_re_expiry_notices` SET `status`='failed', `send_error`=? WHERE `id`=? AND `status` IN ('ready','failed')"),
                    cancellationToken, missing, noticeId).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("attachment_missing", missing);
            }
        }

        var sent = await _mail.SendAsync(new Cp.CpTenantEmailMessage(
            ErpTenancyExpiryMail.Join(to),
            notice.Subject,
            ErpTenancyExpiryMail.ToHtml(notice.Body),
            null,
            null,
            reply,
            reply,
            files.Count == 0 ? null : files.Select(file => new Cp.CpTenantEmailAttachment(file.Path, file.Name)).ToArray()), cancellationToken).ConfigureAwait(false);
        if (!sent.Succeeded)
        {
            var error = ErpTenancyExpiryMail.SendFailure(sent.Code, sent.Message);
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                "UPDATE `epc_erp_re_expiry_notices` SET `status`='failed', `send_error`=? WHERE `id`=? AND `status` IN ('ready','failed')"),
                cancellationToken, error, noticeId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail(sent.Code, error);
        }

        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
            "UPDATE `epc_erp_re_expiry_notices` SET `status`='sent', `sent_at`=?, `send_error`='' WHERE `id`=?"),
            cancellationToken, ErpTenancyExpiryMail.GulfStamp(DateTime.UtcNow), noticeId).ConfigureAwait(false);
        var where = reply.Length == 0 ? "the sending mailbox" : reply;
        var attached = files.Count == 0 ? "" : " " + files.Count.ToString(CultureInfo.InvariantCulture) + (files.Count == 1 ? " document is attached." : " documents are attached.");
        return ErpSimpleWriteResult.Ok("Notice sent to " + ErpTenancyExpiryMail.Join(to) + ". A reply goes to " + where + "." + attached, noticeId);
    }

    public async Task<ErpSimpleWriteResult> UpdateNoticeAsync(
        long noticeId, string toEmails, string replyEmails, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (noticeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the notice to change.");
        }

        var to = ErpTenancyExpiryMail.Join(ErpTenancyExpiryMail.Addresses(toEmails));
        var reply = ErpTenancyExpiryMail.Join(ErpTenancyExpiryMail.Addresses(replyEmails));
        var cleanSubject = ErpTenancyExpiryMail.CleanSubject(subject);
        var cleanBody = (body ?? "").Trim();
        if (cleanSubject.Length == 0 || cleanBody.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "The notice needs a subject and a message.");
        }

        if (cleanBody.Length > 8000)
        {
            cleanBody = cleanBody[..8000];
        }

        return await WriteAsync(async (connection, tx, ct) =>
        {
            var notice = await NoticeAsync(connection, tx, noticeId, ct).ConfigureAwait(false);
            if (notice is null)
            {
                return ErpSimpleWriteResult.Fail("invalid", "That notice is not on file.");
            }

            if (!ErpTenancyExpiryMail.CanEdit(notice.Status))
            {
                return ErpSimpleWriteResult.Fail("invalid", "This notice was already sent. File the reply underneath it.");
            }

            var status = to.Length == 0 ? "needs_email" : "ready";
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_expiry_notices` SET `to_emails`=?, `reply_emails`=?, `subject`=?, `body`=?, `status`=?, `customized`=1, `send_error`='' WHERE `id`=?"),
                ct, to, reply, cleanSubject, cleanBody, status, noticeId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_tenants` t JOIN `epc_erp_re_leases` l ON l.`tenant_id`=t.`id` SET t.`notice_emails`=? WHERE l.`id`=?"),
                ct, to, notice.LeaseId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                to.Length == 0 ? "Notice saved. It still needs a tenant email." : "Notice saved for " + to + ".",
                noticeId);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> SkipAsync(long noticeId, CancellationToken cancellationToken = default)
    {
        if (noticeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the notice to skip.");
        }

        return await WriteAsync(async (connection, tx, ct) =>
        {
            var notice = await NoticeAsync(connection, tx, noticeId, ct).ConfigureAwait(false);
            if (notice is null)
            {
                return ErpSimpleWriteResult.Fail("invalid", "That notice is not on file.");
            }

            if (!ErpTenancyExpiryMail.CanSkip(notice.Status))
            {
                return ErpSimpleWriteResult.Fail("invalid", "A sent notice stays on file.");
            }

            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_expiry_notices` SET `status`='skipped' WHERE `id`=?"),
                ct, noticeId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("This notice will not be sent.", noticeId);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> FileReplyAsync(long noticeId, string fromEmail, string body, CancellationToken cancellationToken = default)
    {
        if (noticeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the notice the reply belongs to.");
        }

        var from = ErpTenancyExpiryMail.Addresses(fromEmail);
        if (from.Count != 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Enter the one email address the tenant replied from.");
        }

        var text = (body ?? "").Trim();
        if (text.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Paste the reply.");
        }

        if (text.Length > 8000)
        {
            text = text[..8000];
        }

        return await WriteAsync(async (connection, tx, ct) =>
        {
            var notice = await NoticeAsync(connection, tx, noticeId, ct).ConfigureAwait(false);
            if (notice is null)
            {
                return ErpSimpleWriteResult.Fail("invalid", "That notice is not on file.");
            }

            if (string.Equals(notice.Status, "skipped", StringComparison.OrdinalIgnoreCase))
            {
                return ErpSimpleWriteResult.Fail("invalid", "This notice was skipped.");
            }

            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_expiry_replies` (`notice_id`,`from_email`,`body`,`filed_at`) VALUES (?,?,?,?)"),
                ct, noticeId, from[0], text, ErpTenancyExpiryMail.GulfStamp(DateTime.UtcNow)).ConfigureAwait(false);
            if (notice.Status is "sent" or "replied")
            {
                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                    "UPDATE `epc_erp_re_expiry_notices` SET `status`='replied' WHERE `id`=?"),
                    ct, noticeId).ConfigureAwait(false);
            }

            return ErpSimpleWriteResult.Ok("Reply from " + from[0] + " is filed on this notice.", noticeId);
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<ErpSimpleWriteResult> AttachToNoticeAsync(
        long noticeId, string title, string docType, Microsoft.AspNetCore.Http.IFormFile? file, CancellationToken cancellationToken = default)
    {
        string? written = null;
        return FinishAttachAsync(WriteAsync(async (connection, tx, ct) =>
        {
            if (noticeId <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Choose the notice.");
            }

            var tenantId = await ErpDb.LongAsync(connection, tx, ErpDb.Positional(
                "SELECT IFNULL(l.`tenant_id`,0) FROM `epc_erp_re_expiry_notices` n JOIN `epc_erp_re_leases` l ON l.`id`=n.`lease_id` WHERE n.`id`=? LIMIT 1"),
                ct, noticeId).ConfigureAwait(false);
            if (tenantId <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "That notice has no tenant.");
            }

            var saved = await StoreDocumentAsync(connection, tx, tenantId, title, docType, file, ct).ConfigureAwait(false);
            written = saved.Path;
            if (!saved.Result.Succeeded)
            {
                return saved.Result;
            }

            var status = await ErpDb.StringAsync(connection, tx, ErpDb.Positional(
                "SELECT `status` FROM `epc_erp_re_expiry_notices` WHERE `id`=? LIMIT 1"),
                ct, noticeId).ConfigureAwait(false);
            if (status is "ready" or "failed" or "needs_email")
            {
                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                    "INSERT IGNORE INTO `epc_erp_re_expiry_files` (`notice_id`,`document_id`,`created_at`) VALUES (?,?,?)"),
                    ct, noticeId, saved.Result.Id, ErpTenancyExpiryMail.GulfStamp(DateTime.UtcNow)).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Attached " + saved.Result.Message + " to this tenant and this notice.", saved.Result.Id);
            }

            return ErpSimpleWriteResult.Ok("Saved " + saved.Result.Message + " on this tenant. This notice was already sent, so the file stays with the tenant.", saved.Result.Id);
        }, cancellationToken), () => written);
    }

    public Task<ErpSimpleWriteResult> AttachToTenantAsync(
        long tenantId, string title, string docType, Microsoft.AspNetCore.Http.IFormFile? file, CancellationToken cancellationToken = default)
    {
        string? written = null;
        return FinishAttachAsync(WriteAsync(async (connection, tx, ct) =>
        {
            if (tenantId <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Choose the tenant.");
            }

            var exists = await ErpDb.LongAsync(connection, tx, ErpDb.Positional(
                "SELECT IFNULL(`id`,0) FROM `epc_erp_re_tenants` WHERE `id`=? LIMIT 1"),
                ct, tenantId).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "That tenant is not on file.");
            }

            var saved = await StoreDocumentAsync(connection, tx, tenantId, title, docType, file, ct).ConfigureAwait(false);
            written = saved.Path;
            return saved.Result.Succeeded
                ? ErpSimpleWriteResult.Ok("Attached " + saved.Result.Message + " to this tenant.", saved.Result.Id)
                : saved.Result;
        }, cancellationToken), () => written);
    }

    private static async Task<ErpSimpleWriteResult> FinishAttachAsync(Task<ErpSimpleWriteResult> write, Func<string?> path)
    {
        try
        {
            var result = await write.ConfigureAwait(false);
            if (!result.Succeeded)
            {
                TryDelete(path());
            }

            return result;
        }
        catch
        {
            TryDelete(path());
            throw;
        }
    }

    public Task<ErpSimpleWriteResult> IncludeOnNoticeAsync(long noticeId, long documentId, CancellationToken cancellationToken = default)
        => WriteAsync(async (connection, tx, ct) =>
        {
            if (noticeId <= 0 || documentId <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Choose the notice and the document.");
            }

            var tenantId = await ErpDb.LongAsync(connection, tx, ErpDb.Positional(
                "SELECT IFNULL(l.`tenant_id`,0) FROM `epc_erp_re_expiry_notices` n JOIN `epc_erp_re_leases` l ON l.`id`=n.`lease_id` WHERE n.`id`=? LIMIT 1"),
                ct, noticeId).ConfigureAwait(false);
            var owner = await ErpDb.LongAsync(connection, tx, ErpDb.Positional(
                "SELECT IFNULL(`related_id`,0) FROM `epc_erp_re_documents` WHERE `id`=? AND `related_kind`='tenant' LIMIT 1"),
                ct, documentId).ConfigureAwait(false);
            if (tenantId <= 0 || owner != tenantId)
            {
                return ErpSimpleWriteResult.Fail("invalid", "That document belongs to a different tenant.");
            }

            var status = await ErpDb.StringAsync(connection, tx, ErpDb.Positional(
                "SELECT `status` FROM `epc_erp_re_expiry_notices` WHERE `id`=? LIMIT 1"),
                ct, noticeId).ConfigureAwait(false);
            if (status is not ("ready" or "failed" or "needs_email"))
            {
                return ErpSimpleWriteResult.Fail("invalid", "This notice was already sent, so another document is not added to it.");
            }

            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "INSERT IGNORE INTO `epc_erp_re_expiry_files` (`notice_id`,`document_id`,`created_at`) VALUES (?,?,?)"),
                ct, noticeId, documentId, ErpTenancyExpiryMail.GulfStamp(DateTime.UtcNow)).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("That document will go with this notice.", documentId);
        }, cancellationToken);

    public async Task<ErpSimpleWriteResult> RemoveDocumentAsync(long documentId, CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose the document to remove.");
        }

        string? path = null;
        var result = await WriteAsync(async (connection, tx, ct) =>
        {
            var key = await ErpDb.StringAsync(connection, tx, ErpDb.Positional(
                "SELECT `storage_key` FROM `epc_erp_re_documents` WHERE `id`=? AND `related_kind`='tenant' LIMIT 1"),
                ct, documentId).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(key))
            {
                return ErpSimpleWriteResult.Fail("invalid", "That tenant document is not on file.");
            }

            path = ErpTenancyDocumentFiles.Resolve(ErpTenancyDocumentFiles.Root(), key);
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "DELETE FROM `epc_erp_re_expiry_files` WHERE `document_id`=?"),
                ct, documentId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "DELETE FROM `epc_erp_re_documents` WHERE `id`=? AND `related_kind`='tenant'"),
                ct, documentId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("The document is removed from this tenant.", documentId);
        }, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded && path is not null && File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return result;
    }

    public async Task<ErpTenancyDocumentOpen?> OpenAsync(long documentId, CancellationToken cancellationToken = default)
    {
        if (documentId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        string key;
        string name;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `storage_key`, `file_name` FROM `epc_erp_re_documents` WHERE `id`=? AND `related_kind`='tenant' LIMIT 1");
            ErpDb.AddParameters(command, documentId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            key = ReadText(reader, 0);
            name = ReadText(reader, 1);
        }

        var path = ErpTenancyDocumentFiles.Resolve(ErpTenancyDocumentFiles.Root(), key);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        var download = ErpTenancyDocumentFiles.DisplayName(name);
        return new ErpTenancyDocumentOpen(path, download, ErpTenancyDocumentFiles.ContentType(download));
    }

    private async Task<PrepareCounts> PrepareCoreAsync(
        DbConnection connection, DbTransaction tx, DateOnly today, long? onlyLeaseId, bool manualEnded, CancellationToken cancellationToken)
    {
        var template = await TemplateAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        var leases = await LeasesAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        var heads = await HeadsAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        var due = 0;
        var inserted = 0;
        foreach (var lease in leases)
        {
            if (ErpTenancyExpiryMail.AcceptsAutomaticNotice(lease.Status, lease.End, today))
            {
                due++;
            }

            if (onlyLeaseId is not null && lease.LeaseId != onlyLeaseId.Value)
            {
                continue;
            }

            var automatic = ErpTenancyExpiryMail.AcceptsAutomaticNotice(lease.Status, lease.End, today);
            var manual = manualEnded && ErpTenancyExpiryMail.CanPrepareManually(lease.Status, lease.End, today);
            if (!automatic && !manual)
            {
                continue;
            }

            var draft = ErpTenancyExpiryMail.Draft(lease, template);
            if (!heads.TryGetValue(lease.LeaseId, out var head))
            {
                try
                {
                    await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                        "INSERT INTO `epc_erp_re_expiry_notices` (`lease_id`,`due_on`,`end_date`,`to_emails`,`reply_emails`,`subject`,`body`,`status`,`send_error`,`customized`,`created_at`) VALUES (?,?,?,?,?,?,?,?,'',0,?)"),
                        cancellationToken,
                        draft.LeaseId,
                        draft.DueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        draft.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        draft.ToEmails,
                        draft.ReplyEmails,
                        draft.Subject,
                        draft.Body,
                        draft.Status,
                        ErpTenancyExpiryMail.GulfStamp(DateTime.UtcNow)).ConfigureAwait(false);
                    inserted++;
                }
                catch (DbException ex) when (ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
                {
                }

                continue;
            }

            if (head.Status is "sent" or "replied" or "skipped")
            {
                continue;
            }

            if (head.Customized)
            {
                if (head.EndDate != draft.EndDate)
                {
                    await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                        "UPDATE `epc_erp_re_expiry_notices` SET `due_on`=?, `end_date`=? WHERE `id`=?"),
                        cancellationToken,
                        draft.DueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        draft.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        head.Id).ConfigureAwait(false);
                }

                continue;
            }

            var status = head.Status == "failed" ? "failed" : draft.Status;
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "UPDATE `epc_erp_re_expiry_notices` SET `due_on`=?, `end_date`=?, `to_emails`=?, `reply_emails`=?, `subject`=?, `body`=?, `status`=? WHERE `id`=? AND `customized`=0 AND `status` IN ('ready','needs_email','failed') AND (`due_on`<>? OR `end_date`<>? OR `to_emails`<>? OR `reply_emails`<>? OR `subject`<>? OR `body`<>? OR `status`<>?)"),
                cancellationToken,
                draft.DueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                draft.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                draft.ToEmails,
                draft.ReplyEmails,
                draft.Subject,
                draft.Body,
                status,
                head.Id,
                draft.DueOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                draft.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                draft.ToEmails,
                draft.ReplyEmails,
                draft.Subject,
                draft.Body,
                status).ConfigureAwait(false);
        }

        return new PrepareCounts(due, inserted);
    }

    private static async Task<ErpTenancyExpiryTemplate> TemplateAsync(DbConnection connection, DbTransaction? tx, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT `subject`, `body`, `reply_emails`, `auto_send` FROM `epc_erp_re_expiry_mail` WHERE `id`=1 LIMIT 1";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ErpTenancyExpiryMail.DefaultTemplate;
        }

        var subject = ReadText(reader, 0);
        var body = ReadText(reader, 1);
        return new ErpTenancyExpiryTemplate(
            subject.Length == 0 ? ErpTenancyExpiryMail.DefaultSubject : subject,
            body.Length == 0 ? ErpTenancyExpiryMail.DefaultBody : body,
            ReadText(reader, 2),
            ReadLong(reader, 3) != 0);
    }

    private static async Task<IReadOnlyList<ErpTenancyLeaseFact>> LeasesAsync(DbConnection connection, DbTransaction? tx, CancellationToken cancellationToken)
    {
        var rows = new List<ErpTenancyLeaseFact>();
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            SELECT l.`id`, l.`code`, l.`status`, l.`start_date`, l.`end_date`, l.`annual_rent`,
                   IFNULL(t.`name`,''), IFNULL(t.`email`,''), IFNULL(t.`notice_emails`,''),
                   IFNULL(p.`name`,''), IFNULL(u.`code`,''), IFNULL(t.`id`,0)
            FROM `epc_erp_re_leases` l
            LEFT JOIN `epc_erp_re_tenants` t ON t.`id`=l.`tenant_id`
            LEFT JOIN `epc_erp_properties` p ON p.`id`=l.`property_id`
            LEFT JOIN `epc_erp_re_units` u ON u.`id`=l.`unit_id` AND l.`unit_id`>0
            WHERE l.`status`='active'
            ORDER BY l.`end_date`, l.`id`
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpTenancyLeaseFact(
                ReadLong(reader, 0),
                ReadText(reader, 1),
                ReadText(reader, 2),
                ReadDate(reader, 3),
                ReadDate(reader, 4),
                ReadMoney(reader, 5),
                ReadText(reader, 6),
                ReadText(reader, 7),
                ReadText(reader, 8),
                ReadText(reader, 9),
                ReadText(reader, 10),
                ReadLong(reader, 11)));
        }

        return rows;
    }

    private static async Task<Dictionary<long, NoticeHead>> HeadsAsync(DbConnection connection, DbTransaction? tx, CancellationToken cancellationToken)
    {
        var rows = new Dictionary<long, NoticeHead>();
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT `id`, `lease_id`, `status`, `customized`, `end_date` FROM `epc_erp_re_expiry_notices`";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var leaseId = ReadLong(reader, 1);
            rows[leaseId] = new NoticeHead(ReadLong(reader, 0), leaseId, ReadText(reader, 2), ReadLong(reader, 3) != 0, ReadDate(reader, 4));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ErpTenancyExpiryNotice>> NoticesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var notices = new List<ErpTenancyExpiryNotice>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT n.`id`, n.`lease_id`, n.`due_on`, n.`end_date`, n.`to_emails`, n.`reply_emails`, n.`subject`, n.`body`, n.`status`,
                       IFNULL(DATE_FORMAT(n.`sent_at`,'%Y-%m-%d %H:%i'),''), n.`send_error`,
                       IFNULL(l.`code`,''), IFNULL(t.`name`,''), IFNULL(p.`name`,''), IFNULL(u.`code`,''), n.`customized`, IFNULL(l.`tenant_id`,0)
                FROM `epc_erp_re_expiry_notices` n
                LEFT JOIN `epc_erp_re_leases` l ON l.`id`=n.`lease_id`
                LEFT JOIN `epc_erp_re_tenants` t ON t.`id`=l.`tenant_id`
                LEFT JOIN `epc_erp_properties` p ON p.`id`=l.`property_id`
                LEFT JOIN `epc_erp_re_units` u ON u.`id`=l.`unit_id` AND l.`unit_id`>0
                ORDER BY n.`end_date`, n.`id`
                LIMIT 80
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                notices.Add(new ErpTenancyExpiryNotice(
                    ReadLong(reader, 0),
                    ReadLong(reader, 1),
                    ReadDate(reader, 2),
                    ReadDate(reader, 3),
                    ReadText(reader, 4),
                    ReadText(reader, 5),
                    ReadText(reader, 6),
                    ReadText(reader, 7),
                    ReadText(reader, 8),
                    ReadText(reader, 9),
                    ReadText(reader, 10),
                    ReadText(reader, 11),
                    ReadText(reader, 12),
                    ReadText(reader, 13),
                    ReadText(reader, 14),
                    ReadLong(reader, 15) != 0,
                    [],
                    ReadLong(reader, 16)));
            }
        }

        var replies = new List<ErpTenancyExpiryReply>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT `id`, `notice_id`, `from_email`, `body`, IFNULL(DATE_FORMAT(`filed_at`,'%Y-%m-%d %H:%i'),'')
                FROM `epc_erp_re_expiry_replies`
                ORDER BY `id`
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                replies.Add(new ErpTenancyExpiryReply(ReadLong(reader, 0), ReadLong(reader, 1), ReadText(reader, 2), ReadText(reader, 3), ReadText(reader, 4)));
            }
        }

        return notices.Select(notice => notice with
        {
            Replies = replies.Where(reply => reply.NoticeId == notice.Id).ToArray(),
        }).ToArray();
    }

    private static async Task<ErpTenancyExpiryNotice?> NoticeAsync(DbConnection connection, DbTransaction? tx, long noticeId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = ErpDb.Positional(
            "SELECT n.`id`, n.`lease_id`, n.`due_on`, n.`end_date`, n.`to_emails`, n.`reply_emails`, n.`subject`, n.`body`, n.`status`, IFNULL(DATE_FORMAT(n.`sent_at`,'%Y-%m-%d %H:%i'),''), n.`send_error`, '', '', '', '', n.`customized` FROM `epc_erp_re_expiry_notices` n WHERE n.`id`=? LIMIT 1");
        ErpDb.AddParameters(command, noticeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ErpTenancyExpiryNotice(
            ReadLong(reader, 0), ReadLong(reader, 1), ReadDate(reader, 2), ReadDate(reader, 3),
            ReadText(reader, 4), ReadText(reader, 5), ReadText(reader, 6), ReadText(reader, 7),
            ReadText(reader, 8), ReadText(reader, 9), ReadText(reader, 10), "", "", "", "",
            ReadLong(reader, 15) != 0, []);
    }

    private async Task<ErpSimpleWriteResult> WriteAsync(
        Func<DbConnection, DbTransaction, CancellationToken, Task<ErpSimpleWriteResult>> work,
        CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "The company database is not configured.");
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var result = await work(connection, tx, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpRealEstateWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<StoredDocument> StoreDocumentAsync(
        DbConnection connection,
        DbTransaction tx,
        long tenantId,
        string title,
        string docType,
        Microsoft.AspNetCore.Http.IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
        {
            return StoredDocument.Fail(ErpSimpleWriteResult.Fail("invalid", "Choose one document to attach."));
        }

        if (file.Length > ErpTenancyDocumentFiles.MaxBytes)
        {
            return StoredDocument.Fail(ErpSimpleWriteResult.Fail("invalid", "Each document must be 8 MB or smaller."));
        }

        var display = ErpTenancyDocumentFiles.DisplayName(file.FileName);
        var extension = ErpTenancyDocumentFiles.ExtensionOf(display);
        if (!ErpTenancyDocumentFiles.AllowedExtension(extension))
        {
            return StoredDocument.Fail(ErpSimpleWriteResult.Fail("invalid", "Attach a PDF, JPG, PNG or WEBP."));
        }

        var type = ErpTenancyDocumentFiles.DocumentType(docType);
        var storedName = ErpTenancyDocumentFiles.StoredName(extension);
        var key = ErpTenancyDocumentFiles.StorageKey(tenantId, storedName);
        var path = ErpTenancyDocumentFiles.Resolve(ErpTenancyDocumentFiles.Root(), key);
        if (path is null)
        {
            return StoredDocument.Fail(ErpSimpleWriteResult.Fail("invalid", "The document could not be stored."));
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return StoredDocument.Fail(ErpSimpleWriteResult.Fail("io", "The document folder is not writable."));
        }

        try
        {
            var rawCode = "TD" + tenantId.ToString(CultureInfo.InvariantCulture) + storedName;
            var code = rawCode.Length <= 40 ? rawCode : rawCode[..40];
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_documents` (`code`,`doc_type`,`related_kind`,`related_id`,`title`,`status`,`storage_key`,`file_name`,`version_no`,`sensitive`) VALUES (?,?,'tenant',?,?,'uploaded',?,?,1,?)"),
                cancellationToken,
                code,
                type,
                tenantId,
                ErpTenancyDocumentFiles.TitleOrName(title, display),
                key,
                display,
                ErpTenancyDocumentFiles.Sensitive(type)).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
            return new StoredDocument(ErpSimpleWriteResult.Ok(display, id), path);
        }
        catch (Exception)
        {
            TryDelete(path);
            throw;
        }
    }

    private static async Task<IReadOnlyList<ErpTenancyExpiryDocument>> DocumentsAsync(DbConnection connection, long[] tenantIds, CancellationToken cancellationToken)
    {
        if (tenantIds.Length == 0)
        {
            return [];
        }

        var marks = string.Join(",", tenantIds.Select((_, index) => "@p" + index.ToString(CultureInfo.InvariantCulture)));
        var rows = new List<ErpTenancyExpiryDocument>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `related_id`, `title`, `doc_type`, `file_name` FROM `epc_erp_re_documents` WHERE `related_kind`='tenant' AND `related_id` IN (" + marks + ") ORDER BY `id`";
        ErpDb.AddParameters(command, tenantIds.Cast<object>().ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpTenancyExpiryDocument(ReadLong(reader, 0), ReadLong(reader, 1), ReadText(reader, 2), ReadText(reader, 3), ReadText(reader, 4)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ErpTenancyExpiryFileLink>> LinksAsync(DbConnection connection, long[] noticeIds, CancellationToken cancellationToken)
    {
        if (noticeIds.Length == 0)
        {
            return [];
        }

        var marks = string.Join(",", noticeIds.Select((_, index) => "@p" + index.ToString(CultureInfo.InvariantCulture)));
        var rows = new List<ErpTenancyExpiryFileLink>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `notice_id`, `document_id` FROM `epc_erp_re_expiry_files` WHERE `notice_id` IN (" + marks + ")";
        ErpDb.AddParameters(command, noticeIds.Cast<object>().ToArray());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpTenancyExpiryFileLink(ReadLong(reader, 0), ReadLong(reader, 1)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<Cp.CpTenantEmailAttachment>> DocumentFilesAsync(DbConnection connection, long noticeId, CancellationToken cancellationToken)
    {
        var rows = new List<Cp.CpTenantEmailAttachment>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT d.`storage_key`, d.`file_name` FROM `epc_erp_re_expiry_files` f JOIN `epc_erp_re_documents` d ON d.`id`=f.`document_id` WHERE f.`notice_id`=? AND d.`related_kind`='tenant' ORDER BY f.`id`");
        ErpDb.AddParameters(command, noticeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var path = ErpTenancyDocumentFiles.Resolve(ErpTenancyDocumentFiles.Root(), ReadText(reader, 0));
            if (path is not null)
            {
                rows.Add(new Cp.CpTenantEmailAttachment(path, ErpTenancyDocumentFiles.DisplayName(ReadText(reader, 1))));
            }
        }

        return rows;
    }

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record StoredDocument(ErpSimpleWriteResult Result, string? Path)
    {
        public static StoredDocument Fail(ErpSimpleWriteResult result) => new(result, null);
    }

    private static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_re_expiry_mail` (
              `id` TINYINT NOT NULL,
              `subject` VARCHAR(200) NOT NULL,
              `body` MEDIUMTEXT NOT NULL,
              `reply_emails` VARCHAR(1000) NOT NULL DEFAULT '',
              `auto_send` TINYINT NOT NULL DEFAULT 0,
              `updated_at` DATETIME NULL,
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_re_expiry_notices` (
              `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
              `lease_id` BIGINT NOT NULL,
              `due_on` DATE NOT NULL,
              `end_date` DATE NOT NULL,
              `to_emails` VARCHAR(1000) NOT NULL DEFAULT '',
              `reply_emails` VARCHAR(1000) NOT NULL DEFAULT '',
              `subject` VARCHAR(200) NOT NULL DEFAULT '',
              `body` MEDIUMTEXT NOT NULL,
              `status` VARCHAR(20) NOT NULL DEFAULT 'ready',
              `sent_at` DATETIME NULL,
              `send_error` VARCHAR(255) NOT NULL DEFAULT '',
              `customized` TINYINT NOT NULL DEFAULT 0,
              `created_at` DATETIME NOT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `uk_expiry_notice_lease` (`lease_id`),
              KEY `idx_expiry_notice_status` (`status`,`end_date`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_re_documents` (
              `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
              `code` VARCHAR(40) NOT NULL,
              `doc_type` VARCHAR(40) NOT NULL DEFAULT 'other',
              `related_kind` VARCHAR(40) NOT NULL DEFAULT 'property',
              `related_id` BIGINT NOT NULL DEFAULT 0,
              `title` VARCHAR(190) NOT NULL,
              `expiry_date` DATE DEFAULT NULL,
              `status` VARCHAR(40) NOT NULL DEFAULT 'uploaded',
              `storage_key` VARCHAR(255) NOT NULL DEFAULT '',
              `file_name` VARCHAR(190) NOT NULL DEFAULT '',
              `version_no` INT NOT NULL DEFAULT 1,
              `sensitive` TINYINT NOT NULL DEFAULT 0,
              `import_id` BIGINT NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_re_expiry_files` (
              `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
              `notice_id` BIGINT NOT NULL,
              `document_id` BIGINT NOT NULL,
              `created_at` DATETIME NOT NULL,
              PRIMARY KEY (`id`),
              UNIQUE KEY `uk_expiry_file` (`notice_id`,`document_id`),
              KEY `idx_expiry_file_doc` (`document_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_erp_re_expiry_replies` (
              `id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
              `notice_id` BIGINT NOT NULL,
              `from_email` VARCHAR(120) NOT NULL,
              `body` MEDIUMTEXT NOT NULL,
              `filed_at` DATETIME NOT NULL,
              PRIMARY KEY (`id`),
              KEY `idx_expiry_reply` (`notice_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        if (!await ErpDb.ColumnKnownAsync(connection, "epc_erp_re_tenants", "notice_emails", cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await ErpDb.ExecuteAsync(connection, null,
                    "ALTER TABLE `epc_erp_re_tenants` ADD COLUMN `notice_emails` VARCHAR(500) NOT NULL DEFAULT ''",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
            }
        }

        var seeded = await ErpDb.LongAsync(connection, null,
            "SELECT COUNT(*) FROM `epc_erp_re_expiry_mail` WHERE `id`=1",
            cancellationToken).ConfigureAwait(false);
        if (seeded == 0)
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(
                "INSERT INTO `epc_erp_re_expiry_mail` (`id`,`subject`,`body`,`reply_emails`,`auto_send`) VALUES (1,?,?, '', 0)"),
                cancellationToken, ErpTenancyExpiryMail.DefaultSubject, ErpTenancyExpiryMail.DefaultBody).ConfigureAwait(false);
        }
    }

    private static string SendSummary(int sent, int failed, int needsEmail, string firstError)
    {
        var parts = new List<string>();
        if (sent > 0)
        {
            parts.Add("Sent " + sent.ToString(CultureInfo.InvariantCulture) + ".");
        }

        if (failed > 0)
        {
            parts.Add("Could not send " + failed.ToString(CultureInfo.InvariantCulture) + ".");
        }

        if (needsEmail > 0)
        {
            parts.Add(needsEmail.ToString(CultureInfo.InvariantCulture) + " still need a tenant email.");
        }

        if (parts.Count == 0)
        {
            parts.Add("No notice is waiting to send.");
        }

        if (firstError.Length > 0)
        {
            parts.Add(firstError);
        }

        return string.Join(" ", parts);
    }

    private static ErpTenancyExpiryWatch Watch(ErpTenancyLeaseFact lease, DateOnly today)
        => new(
            lease.LeaseId,
            lease.LeaseCode,
            lease.TenantName,
            lease.PropertyName,
            lease.UnitCode,
            lease.End,
            ErpTenancyExpiryMail.NoticeDueOn(lease.End),
            ErpTenancyExpiryMail.Window(lease.End, today),
            lease.TenantId);

    private readonly record struct PrepareCounts(int Due, int Inserted);

    private static long ReadLong(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static decimal ReadMoney(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static string ReadText(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? "" : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? "";

    private static DateOnly ReadDate(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return default;
        }

        var value = reader.GetValue(ordinal);
        if (value is DateTime dateTime)
        {
            return DateOnly.FromDateTime(dateTime);
        }

        if (value is DateOnly date)
        {
            return date;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : default;
    }

    private sealed record NoticeHead(long Id, long LeaseId, string Status, bool Customized, DateOnly EndDate);
}
