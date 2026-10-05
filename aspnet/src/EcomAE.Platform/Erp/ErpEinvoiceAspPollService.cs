using System.Data.Common;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// PHP ajax `einvoice_poll_asp` twin (epc_einvoice_poll_all_pending in content/shop/finance/epc_einvoice.php):
/// polls ASP submissions in submitted/retry_pending status, retries failed submissions with the stored
/// (or rebuilt) XML, applies accepted/rejected/pending transitions, exponential backoff (300 * 2^n, cap 3600),
/// and logs every transition into epc_einvoice_events. No transaction, exactly as PHP (per-statement autocommit).
/// </summary>
public interface IErpEinvoiceAspPollService
{
    Task<ErpEinvoiceAspPollResult> PollAsync(CancellationToken cancellationToken = default);
}

public sealed record ErpEinvoiceAspPollResult(int Polled, int Accepted, int Rejected, int Pending, int Errors);

public sealed class ErpEinvoiceAspPollService : IErpEinvoiceAspPollService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IHttpClientFactory _http;
    private readonly TimeProvider _clock;

    public ErpEinvoiceAspPollService(
        IErpWriteConnectionFactory connections,
        IHttpClientFactory http,
        TimeProvider clock)
    {
        _connections = connections;
        _http = http;
        _clock = clock;
    }

    public async Task<ErpEinvoiceAspPollResult> PollAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new ErpEinvoiceAspPollResult(0, 0, 0, 0, 0);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAspSubmissionsSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = _clock.GetUtcNow().ToUnixTimeSeconds();

        var submissions = await RowsAsync(
            connection,
            ErpDb.Positional(
                "SELECT s.*, d.`uuid`, d.`invoice_number`"
                + " FROM `epc_einvoice_asp_submissions` s"
                + " JOIN `epc_einvoice_documents` d ON d.`id` = s.`document_id`"
                + " WHERE s.`status` IN ('submitted','retry_pending')"
                + " AND s.`next_poll_at` <= ?"
                + " AND s.`retry_count` < s.`max_retries`"
                + " ORDER BY s.`next_poll_at` ASC"
                + " LIMIT 50"),
            cancellationToken, now).ConfigureAwait(false);

        var polled = 0;
        var accepted = 0;
        var rejected = 0;
        var pending = 0;
        var errors = 0;
        var apiKey = (await ReadSettingAsync(connection, "asp_api_key", cancellationToken).ConfigureAwait(false)).Trim();
        var aspUrl = (await ReadSettingAsync(connection, "asp_api_url", cancellationToken).ConfigureAwait(false)).Trim();

        foreach (var sub in submissions)
        {
            polled++;
            var docId = Convert.ToInt64(sub["document_id"], CultureInfo.InvariantCulture);
            var submissionId = sub["submission_id"]?.ToString() ?? string.Empty;
            var subId = Convert.ToInt64(sub["id"], CultureInfo.InvariantCulture);
            var retryCount = Convert.ToInt32(sub["retry_count"], CultureInfo.InvariantCulture);
            var maxRetries = Convert.ToInt32(sub["max_retries"], CultureInfo.InvariantCulture);
            if (sub["status"]?.ToString() == "retry_pending")
            {
                var doc = await GetDocumentAsync(connection, docId, cancellationToken).ConfigureAwait(false);
                if (doc is null)
                {
                    continue;
                }

                var xml = doc["xml_content"]?.ToString() ?? string.Empty;
                if (xml.Length == 0)
                {
                    xml = await RebuildXmlAsync(connection, doc, cancellationToken).ConfigureAwait(false);
                }

                var retryResult = await AspApiPostAsync(aspUrl, apiKey, xml, doc["uuid"]?.ToString() ?? string.Empty, "RETRY-" + subId, cancellationToken).ConfigureAwait(false);
                var newRetryCount = retryCount + 1;
                if (retryResult.Ok)
                {
                    submissionId = retryResult.SubmissionId;
                    await ErpDb.ExecuteAsync(
                        connection, null,
                        "UPDATE `epc_einvoice_asp_submissions` SET `submission_id` = @p0, `status` = 'submitted', `retry_count` = @p1, `next_poll_at` = @p2, `updated_at` = @p3 WHERE `id` = @p4",
                        cancellationToken, submissionId, newRetryCount, now + 300, now, subId).ConfigureAwait(false);
                    await ErpDb.ExecuteAsync(
                        connection, null,
                        "UPDATE `epc_einvoice_documents` SET `status` = 'submitted', `asp_reference` = @p0, `time_updated` = @p1 WHERE `id` = @p2",
                        cancellationToken, submissionId, now, docId).ConfigureAwait(false);
                    await LogEventAsync(connection, docId, "asp_retry_ok", "submitted", "Retry #" + newRetryCount + " succeeded", new { submission_id = submissionId }, now, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var backoff = Math.Min(3600, 300 * (int)Math.Pow(2, newRetryCount));
                    await ErpDb.ExecuteAsync(
                        connection, null,
                        "UPDATE `epc_einvoice_asp_submissions` SET `retry_count` = @p0, `next_poll_at` = @p1, `updated_at` = @p2, `response_json` = @p3 WHERE `id` = @p4",
                        cancellationToken, newRetryCount, now + backoff, now, JsonSerializer.Serialize(retryResult), subId).ConfigureAwait(false);
                    if (newRetryCount >= maxRetries)
                    {
                        await ErpDb.ExecuteAsync(connection, null, "UPDATE `epc_einvoice_asp_submissions` SET `status` = 'error' WHERE `id` = @p0", cancellationToken, subId).ConfigureAwait(false);
                        await ErpDb.ExecuteAsync(connection, null, "UPDATE `epc_einvoice_documents` SET `status` = 'rejected', `time_updated` = @p0 WHERE `id` = @p1", cancellationToken, now, docId).ConfigureAwait(false);
                        await LogEventAsync(connection, docId, "asp_retry_exhausted", "error", "Max retries reached", new { retries = newRetryCount }, now, cancellationToken).ConfigureAwait(false);
                        errors++;
                    }
                    else
                    {
                        await LogEventAsync(connection, docId, "asp_retry_fail", "error", "Retry #" + newRetryCount + " failed: " + (retryResult.Error ?? string.Empty), null, now, cancellationToken).ConfigureAwait(false);
                    }
                }

                continue;
            }

            if (submissionId.Length == 0)
            {
                continue;
            }

            var poll = await AspApiGetStatusAsync(aspUrl, apiKey, submissionId, cancellationToken).ConfigureAwait(false);
            if (!poll.Ok)
            {
                var backoff = Math.Min(3600, 300 * (int)Math.Pow(2, retryCount));
                await ErpDb.ExecuteAsync(
                    connection, null,
                    "UPDATE `epc_einvoice_asp_submissions` SET `last_poll_at` = @p0, `next_poll_at` = @p1, `updated_at` = @p2 WHERE `id` = @p3",
                    cancellationToken, now, now + backoff, now, subId).ConfigureAwait(false);
                errors++;
                continue;
            }

            if (poll.Status is "accepted" or "valid" or "delivered" or "cleared")
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    "UPDATE `epc_einvoice_asp_submissions` SET `status` = 'accepted', `last_poll_at` = @p0, `updated_at` = @p1, `response_json` = @p2 WHERE `id` = @p3",
                    cancellationToken, now, now, poll.RawJson, subId).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection, null,
                    "UPDATE `epc_einvoice_documents` SET `status` = 'accepted', `fta_report_status` = @p0, `time_updated` = @p1 WHERE `id` = @p2",
                    cancellationToken, poll.FtaStatus.Length > 0 ? poll.FtaStatus : "reported", now, docId).ConfigureAwait(false);
                await LogEventAsync(connection, docId, "asp_accepted", "accepted", "Invoice accepted by ASP/FTA", new { fta_status = poll.FtaStatus, accepted_at = poll.AcceptedAt }, now, cancellationToken).ConfigureAwait(false);
                accepted++;
            }
            else if (poll.Status is "rejected" or "invalid" or "failed")
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    "UPDATE `epc_einvoice_asp_submissions` SET `status` = 'rejected', `last_poll_at` = @p0, `updated_at` = @p1, `response_json` = @p2 WHERE `id` = @p3",
                    cancellationToken, now, now, poll.RawJson, subId).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection, null,
                    "UPDATE `epc_einvoice_documents` SET `status` = 'rejected', `fta_report_status` = @p0, `time_updated` = @p1 WHERE `id` = @p2",
                    cancellationToken, "rejected", now, docId).ConfigureAwait(false);
                await LogEventAsync(connection, docId, "asp_rejected", "rejected", "Invoice rejected: " + poll.Message, new { errors = poll.ErrorsJson }, now, cancellationToken).ConfigureAwait(false);
                rejected++;
            }
            else
            {
                // Still pending — exponential backoff (5min → 10min → 20min → …, cap 1h), as PHP.
                var pollCount = retryCount + 1;
                var backoff = Math.Min(3600, 300 * (int)Math.Pow(2, pollCount - 1));
                await ErpDb.ExecuteAsync(
                    connection, null,
                    "UPDATE `epc_einvoice_asp_submissions` SET `last_poll_at` = @p0, `next_poll_at` = @p1, `retry_count` = @p2, `updated_at` = @p3 WHERE `id` = @p4",
                    cancellationToken, now, now + backoff, pollCount, now, subId).ConfigureAwait(false);
                pending++;
            }
        }

        return new ErpEinvoiceAspPollResult(polled, accepted, rejected, pending, errors);
    }

    private async Task<Dictionary<string, object?>?> GetDocumentAsync(DbConnection c, long id, CancellationToken ct)
    {
        var doc = await FirstRowAsync(
            c, ErpDb.Positional("SELECT * FROM `epc_einvoice_documents` WHERE `id` = ? AND `active` = 1 LIMIT 1"),
            ct, id).ConfigureAwait(false);
        return doc;
    }

    /// <summary>epc_einvoice_build_xml equivalent for a retry: rebuild from the stored doc + lines.</summary>
    private static async Task<string> RebuildXmlAsync(DbConnection c, Dictionary<string, object?> doc, CancellationToken ct)
    {
        var docId = Convert.ToInt64(doc["id"], CultureInfo.InvariantCulture);
        var lineRows = await StaticRowsAsync(
            c, ErpDb.Positional("SELECT * FROM `epc_einvoice_lines` WHERE `document_id` = ? ORDER BY `line_no`"),
            ct, docId).ConfigureAwait(false);
        var lines = lineRows.Select(l =>
        {
            var qty = Convert.ToDecimal(l["quantity"], CultureInfo.InvariantCulture);
            var lineNet = Convert.ToDecimal(l["line_net"], CultureInfo.InvariantCulture);
            var unitNet = qty > 0m ? decimal.Round(lineNet / qty, 4, MidpointRounding.AwayFromZero) : 0m;
            return new ErpInvoiceFromOrderLine(
                Convert.ToInt32(l["line_no"], CultureInfo.InvariantCulture),
                l["item_name"]?.ToString() ?? string.Empty,
                l["item_description"]?.ToString() ?? string.Empty,
                l["item_type"]?.ToString() ?? string.Empty,
                qty,
                l["uom_code"]?.ToString() ?? string.Empty,
                unitNet,
                unitNet,
                lineNet,
                l["tax_category"]?.ToString() ?? string.Empty,
                Convert.ToDecimal(l["tax_rate"], CultureInfo.InvariantCulture),
                Convert.ToDecimal(l["tax_amount"], CultureInfo.InvariantCulture),
                Convert.ToDecimal(l["gross_amount"], CultureInfo.InvariantCulture));
        }).ToList();

        var seller = JsonToDict(doc["seller_json"]?.ToString());
        var buyer = JsonToDict(doc["buyer_json"]?.ToString());
        var taxCategory = lines.Count > 0 ? lines[0].TaxCategory : "S";
        var taxRate = lines.Count > 0 ? lines[0].TaxRate : 0m;
        return ErpInvoiceFromOrderWriteService.BuildInvoiceXml(
            doc["uuid"]?.ToString() ?? string.Empty,
            doc["invoice_number"]?.ToString() ?? string.Empty,
            Convert.ToInt64(doc["issue_date"], CultureInfo.InvariantCulture),
            Convert.ToInt64(doc["payment_due_date"], CultureInfo.InvariantCulture),
            doc["payment_means_code"]?.ToString(),
            doc["bank_account"]?.ToString(),
            seller,
            buyer,
            lines,
            Convert.ToDecimal(doc["subtotal_ex_vat"], CultureInfo.InvariantCulture),
            Convert.ToDecimal(doc["total_vat"], CultureInfo.InvariantCulture),
            Convert.ToDecimal(doc["total_incl_vat"], CultureInfo.InvariantCulture),
            Convert.ToDecimal(doc["paid_amount"], CultureInfo.InvariantCulture),
            Convert.ToDecimal(doc["amount_due"], CultureInfo.InvariantCulture),
            taxCategory,
            taxRate);
    }

    private static Dictionary<string, string> JsonToDict(string? json)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return dict;
        }

        try
        {
            using var parsed = JsonDocument.Parse(json);
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
            {
                return dict;
            }

            foreach (var prop in parsed.RootElement.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? (prop.Value.GetString() ?? string.Empty) : prop.Value.ToString();
            }
        }
        catch (JsonException)
        {
        }

        return dict;
    }

    private sealed record AspPostResult(bool Ok, string? Error, int HttpStatus, string SubmissionId, string? ResponseBody);

    /// <summary>PHP epc_einvoice_asp_api_post — POST {url}/invoices with the XML body and invoice headers.</summary>
    private async Task<AspPostResult> AspApiPostAsync(string url, string apiKey, string xml, string uuid, string reference, CancellationToken ct)
    {
        var submitUrl = url.TrimEnd('/') + "/invoices";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, submitUrl);
            request.Content = new StringContent(xml, Encoding.UTF8, "application/xml");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.TryAddWithoutValidation("X-Invoice-UUID", uuid);
            request.Headers.TryAddWithoutValidation("X-Submission-Reference", reference);

            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var status = (int)response.StatusCode;

            if (status >= 200 && status < 300)
            {
                var submissionId = reference;
                try
                {
                    using var parsed = JsonDocument.Parse(body);
                    var root = parsed.RootElement;
                    submissionId =
                        root.TryGetProperty("submissionId", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString()! :
                        root.TryGetProperty("submission_id", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString()! :
                        root.TryGetProperty("id", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()! :
                        reference;
                }
                catch (JsonException)
                {
                }

                return new AspPostResult(true, null, status, submissionId, body);
            }

            var error = "HTTP " + status;
            try
            {
                using var parsed = JsonDocument.Parse(body);
                var root = parsed.RootElement;
                error =
                    root.TryGetProperty("message", out var m) ? m.ToString() :
                    root.TryGetProperty("error", out var e) ? e.ToString() :
                    root.TryGetProperty("detail", out var d) ? d.ToString() :
                    error;
            }
            catch (JsonException)
            {
            }

            return new AspPostResult(false, error, status, string.Empty, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new AspPostResult(false, "HTTP client error: " + ex.Message, 0, string.Empty, null);
        }
    }

    private sealed record AspPollResult(bool Ok, string? Error, int HttpStatus, string Status, string FtaStatus, string Message, string? ErrorsJson, string AcceptedAt, string? RawJson);

    /// <summary>PHP epc_einvoice_asp_api_get_status — GET {url}/invoices/{submissionId}/status.</summary>
    private async Task<AspPollResult> AspApiGetStatusAsync(string url, string apiKey, string submissionId, CancellationToken ct)
    {
        var statusUrl = url.TrimEnd('/') + "/invoices/" + Uri.EscapeDataString(submissionId) + "/status";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, statusUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var status = (int)response.StatusCode;

            if (status >= 200 && status < 300)
            {
                try
                {
                    using var parsed = JsonDocument.Parse(body);
                    var root = parsed.RootElement;
                    var st = root.TryGetProperty("status", out var s) ? s.ToString() : root.TryGetProperty("invoiceStatus", out var i2) ? i2.ToString() : "unknown";
                    var fta = root.TryGetProperty("ftaStatus", out var f) ? f.ToString() : root.TryGetProperty("fta_status", out var f2) ? f2.ToString() : string.Empty;
                    var msg = root.TryGetProperty("message", out var m) ? m.ToString() : string.Empty;
                    string? errs = null;
                    if (root.TryGetProperty("errors", out var errsEl))
                    {
                        errs = errsEl.GetRawText();
                    }
                    else if (root.TryGetProperty("validationErrors", out var ve))
                    {
                        errs = ve.GetRawText();
                    }

                    var acc = root.TryGetProperty("acceptedAt", out var a) ? a.ToString() : root.TryGetProperty("accepted_at", out var a2) ? a2.ToString() : string.Empty;
                    return new AspPollResult(true, null, status, st.ToLowerInvariant(), fta, msg, errs, acc, body);
                }
                catch (JsonException)
                {
                    return new AspPollResult(false, "HTTP " + status, status, string.Empty, string.Empty, string.Empty, null, string.Empty, null);
                }
            }

            return new AspPollResult(false, "HTTP " + status, status, string.Empty, string.Empty, string.Empty, null, string.Empty, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new AspPollResult(false, "HTTP client error: " + ex.Message, 0, string.Empty, string.Empty, string.Empty, null, string.Empty, null);
        }
    }

    private static async Task LogEventAsync(DbConnection c, long docId, string type, string status, string message, object? payload, long now, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(
            c, null,
            "INSERT INTO `epc_einvoice_events` (`document_id`, `event_type`, `status`, `message`, `payload_json`, `time_created`) VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
            ct, docId, type, status, message,
            payload is null ? "{}" : JsonSerializer.Serialize(payload), now).ConfigureAwait(false);
    }

    private static async Task<string> ReadSettingAsync(DbConnection c, string key, CancellationToken ct)
        => (await ErpDb.StringAsync(
            c, null, ErpDb.Positional("SELECT `setting_value` FROM `epc_einvoice_settings` WHERE `setting_key` = ? LIMIT 1"),
            ct, key).ConfigureAwait(false)) ?? string.Empty;

    private static async Task EnsureAspSubmissionsSchemaAsync(DbConnection c, CancellationToken ct)
    {
        try
        {
            await ErpDb.ExecuteAsync(
                c, null,
                "CREATE TABLE IF NOT EXISTS `epc_einvoice_asp_submissions` ("
                + "`id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,"
                + "`document_id` INT NOT NULL,"
                + "`submission_id` VARCHAR(128) NOT NULL DEFAULT '',"
                + "`asp_api_url` VARCHAR(512) NOT NULL DEFAULT '',"
                + "`asp_api_key_hash` VARCHAR(64) NOT NULL DEFAULT '',"
                + "`status` ENUM('pending','submitted','accepted','rejected','error','retry_pending') NOT NULL DEFAULT 'pending',"
                + "`retry_count` INT NOT NULL DEFAULT 0,"
                + "`max_retries` INT NOT NULL DEFAULT 3,"
                + "`last_poll_at` INT NOT NULL DEFAULT 0,"
                + "`next_poll_at` INT NOT NULL DEFAULT 0,"
                + "`response_json` TEXT,"
                + "`created_at` INT NOT NULL DEFAULT 0,"
                + "`updated_at` INT NOT NULL DEFAULT 0,"
                + "INDEX `idx_status_poll` (`status`, `next_poll_at`),"
                + "INDEX `idx_document` (`document_id`)"
                + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                ct).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static async Task<Dictionary<string, object?>?> FirstRowAsync(DbConnection c, string sql, CancellationToken ct, params object[] ps)
    {
        var rows = await StaticRowsAsync(c, sql, ct, ps).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    private static async Task<List<Dictionary<string, object?>>> RowsAsync(DbConnection c, string sql, CancellationToken ct, params object[] ps)
        => await StaticRowsAsync(c, sql, ct, ps).ConfigureAwait(false);

    private static async Task<List<Dictionary<string, object?>>> StaticRowsAsync(DbConnection c, string sql, CancellationToken ct, params object[] ps)
    {
        var list = new List<Dictionary<string, object?>>();
        await using var cmd = c.CreateCommand();
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
