using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpTenantEmailSaveRequest(
    bool UseTenantSmtp,
    string? SmtpHost,
    string? SmtpPort,
    string? SmtpEncryption,
    string? SmtpUsername,
    string? SmtpPassword,
    string? FromName,
    string? FromEmail);

public interface ICpTenantEmailWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(CpTenantEmailSaveRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SendTestAsync(string? testTo, CancellationToken cancellationToken = default);
}

/// <summary>Live PHP <c>ajax_integrations.php</c> <c>save_tenant_smtp</c> / <c>test_tenant_smtp</c>.</summary>
public sealed class CpTenantEmailWriteService : ICpTenantEmailWriteService
{
    private static readonly JsonSerializerOptions JsonWrite = new()
    {
        WriteIndented = false,
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpTenantEmailWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeEncryption(string? value)
    {
        var enc = (value ?? string.Empty).Trim().ToLowerInvariant();
        return enc is "tls" or "ssl" ? enc : string.Empty;
    }

    public static string NormalizePort(string? value)
    {
        var port = (value ?? string.Empty).Trim();
        return string.IsNullOrEmpty(port) ? "587" : port;
    }

    public static bool IsEmail(string? value)
        => !string.IsNullOrWhiteSpace(value) && MailAddress.TryCreate(value.Trim(), out _);

    public static JsonObject MergeSmtp(JsonNode? root, CpTenantEmailSaveRequest request)
    {
        var obj = root as JsonObject ?? new JsonObject();
        var existing = obj["smtp"] as JsonObject ?? new JsonObject();
        var password = (request.SmtpPassword ?? string.Empty).Trim();
        if (password.Length == 0)
        {
            password = existing["smtp_password"]?.GetValue<string>() ?? string.Empty;
        }

        obj["smtp"] = new JsonObject
        {
            ["use_tenant_smtp"] = request.UseTenantSmtp,
            ["smtp_host"] = (request.SmtpHost ?? string.Empty).Trim(),
            ["smtp_port"] = NormalizePort(request.SmtpPort),
            ["smtp_encryption"] = NormalizeEncryption(request.SmtpEncryption),
            ["smtp_username"] = (request.SmtpUsername ?? string.Empty).Trim(),
            ["from_name"] = (request.FromName ?? string.Empty).Trim(),
            ["from_email"] = (request.FromEmail ?? string.Empty).Trim(),
        };
        if (password.Length > 0)
        {
            ((JsonObject)obj["smtp"]!)["smtp_password"] = password;
        }

        return obj;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(CpTenantEmailSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var id = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `epc_portal_site_settings` ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (id <= 0)
            {
                return ErpSimpleWriteResult.Fail("missing", "Site settings row is missing. Schema ensure stays on the Classic twin.");
            }

            var raw = await ErpDb.StringAsync(
                connection,
                null,
                "SELECT IFNULL(`integrations_json`, '') FROM `epc_portal_site_settings` WHERE `id` = @p0 LIMIT 1",
                cancellationToken,
                id).ConfigureAwait(false) ?? string.Empty;

            JsonNode? root = null;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    root = JsonNode.Parse(raw);
                }
                catch (JsonException)
                {
                    return ErpSimpleWriteResult.Fail("bad_json", "integrations_json is not valid JSON.");
                }
            }

            var merged = MergeSmtp(root, request);
            var json = merged.ToJsonString(JsonWrite);
            var writes = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_portal_site_settings` SET `integrations_json` = ?, `updated_at` = ? WHERE `id` = ?"),
                cancellationToken,
                json,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                id).ConfigureAwait(false);

            return writes > 0
                ? ErpSimpleWriteResult.Ok("SMTP settings saved.", id)
                : ErpSimpleWriteResult.Fail("unchanged", "SMTP settings were not updated.");
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    public async Task<ErpSimpleWriteResult> SendTestAsync(string? testTo, CancellationToken cancellationToken = default)
    {
        var to = (testTo ?? string.Empty).Trim();
        if (!IsEmail(to))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Valid test email required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var raw = await ErpDb.StringAsync(
                connection,
                null,
                "SELECT IFNULL(`integrations_json`, '') FROM `epc_portal_site_settings` ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return ErpSimpleWriteResult.Fail("missing", "Save tenant SMTP first.");
            }

            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("smtp", out var smtp) || smtp.ValueKind != JsonValueKind.Object)
            {
                return ErpSimpleWriteResult.Fail("missing", "Save tenant SMTP first.");
            }

            var useTenant = smtp.TryGetProperty("use_tenant_smtp", out var useEl)
                && (useEl.ValueKind == JsonValueKind.True
                    || (useEl.ValueKind == JsonValueKind.Number && useEl.GetInt32() != 0)
                    || (useEl.ValueKind == JsonValueKind.String && useEl.GetString() is "1" or "true"));
            if (!useTenant)
            {
                return ErpSimpleWriteResult.Fail(
                    "disabled",
                    "Use tenant SMTP is off. Enable and save it here, or send from the Classic twin (platform default).");
            }

            var host = smtp.TryGetProperty("smtp_host", out var hostEl) ? hostEl.GetString() ?? string.Empty : string.Empty;
            var portRaw = smtp.TryGetProperty("smtp_port", out var portEl) ? portEl.GetString() ?? "587" : "587";
            var enc = NormalizeEncryption(smtp.TryGetProperty("smtp_encryption", out var encEl) ? encEl.GetString() : string.Empty);
            var user = smtp.TryGetProperty("smtp_username", out var userEl) ? userEl.GetString() ?? string.Empty : string.Empty;
            var pass = smtp.TryGetProperty("smtp_password", out var passEl) ? passEl.GetString() ?? string.Empty : string.Empty;
            var fromName = smtp.TryGetProperty("from_name", out var fnEl) ? fnEl.GetString() ?? string.Empty : string.Empty;
            var fromEmail = smtp.TryGetProperty("from_email", out var feEl) ? feEl.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(host) || !int.TryParse(portRaw, out var port) || port is < 1 or > 65535)
            {
                return ErpSimpleWriteResult.Fail("invalid", "SMTP host and port are required.");
            }

            if (!IsEmail(fromEmail))
            {
                fromEmail = to;
            }

            using var message = new MailMessage
            {
                From = string.IsNullOrWhiteSpace(fromName) ? new MailAddress(fromEmail) : new MailAddress(fromEmail, fromName),
                Subject = "ECOM AE SMTP test — " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                Body = "<p>This is a test message from tenant CP SMTP settings.</p>",
                IsBodyHtml = true,
            };
            message.To.Add(to);

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enc is "tls" or "ssl",
                Timeout = 15_000,
                DeliveryMethod = SmtpDeliveryMethod.Network,
            };
            if (!string.IsNullOrEmpty(user))
            {
                client.Credentials = new NetworkCredential(user, pass);
            }

            await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Test email sent to " + to, 0);
        }
        catch (JsonException)
        {
            return ErpSimpleWriteResult.Fail("bad_json", "integrations_json is not valid JSON.");
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("send_failed", ex.Message);
        }
    }
}
