using System.Net;
using System.Text;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live twins of PHP <c>send_vin_email.php</c> (users_vin INSERT) and
/// <c>ajax_send_message.php</c> (customer path). Captcha, photo upload, and
/// manager email notify stay on Classic — same way PHP notify can fail and still
/// write the row after a successful INSERT attempt.
/// </summary>
public interface IStorefrontVinRequestWriteService
{
    Task<StorefrontVinRequestWriteResult> CreateAsync(
        int userId,
        IReadOnlyDictionary<string, string> fields,
        string? parts,
        CancellationToken cancellationToken = default);

    Task<StorefrontVinRequestWriteResult> SendMessageAsync(
        int userId,
        long vinId,
        string? text,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontVinRequestWriteResult(
    bool Ok,
    string Code,
    string Message,
    long Id,
    int Writes)
{
    public object ToPayload(object session) => new
    {
        ok = Ok,
        surface = "storefront",
        writes = Writes,
        writesBlocked = false,
        cutoverAllowed = false,
        phpAuthoritative = false,
        validation_code = Code,
        message = Message,
        request_id = Id,
        session
    };
}

public sealed class StorefrontVinRequestWriteService : IStorefrontVinRequestWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontVinRequestWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<StorefrontVinRequestWriteResult> CreateAsync(
        int userId,
        IReadOnlyDictionary<string, string> fields,
        string? parts,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Fail("auth", "Forbidden");
        }

        var map = Normalize(fields);
        foreach (var field in PhpSellerRequest.Fields.Where(f => f.Required))
        {
            if (!map.TryGetValue(field.Name, out var value) || value.Length == 0)
            {
                return Fail("invalid", "Forbidden");
            }
        }

        var vin = map.GetValueOrDefault("client_vin", "");
        if (vin.Length is < 11 or > 17)
        {
            return Fail("invalid_vin", "Enter a VIN of 11–17 characters.");
        }

        var email = map.GetValueOrDefault("client_email", "");
        if (!email.Contains('@', StringComparison.Ordinal))
        {
            return Fail("invalid", "Forbidden");
        }

        var partsText = (parts ?? string.Empty).Trim();
        if (partsText.Length == 0)
        {
            return Fail("invalid", "Forbidden");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        var html = BuildRequestHtml(map, partsText);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "INSERT INTO `users_vin` (`text`,`user_id`,`time`,`viewed`,`viewed_customer`) VALUES (?,?,?,?,?)"),
                cancellationToken,
                html, (long)userId, now, 0, 1).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "INSERT INTO `users_vin_messages` (`vin_id`,`is_customer`,`text`,`time`) VALUES (?,?,?,?)"),
                cancellationToken,
                id, 1, partsText, now).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new StorefrontVinRequestWriteResult(true, "ok", "Request sent", id, 1);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<StorefrontVinRequestWriteResult> SendMessageAsync(
        int userId,
        long vinId,
        string? text,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Fail("auth", "Forbidden");
        }

        if (vinId <= 0)
        {
            return Fail("invalid", "Forbidden");
        }

        var body = (text ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            return Fail("invalid", "Forbidden");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var owner = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `users_vin` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            vinId).ConfigureAwait(false);
        if (owner != userId)
        {
            return Fail("forbidden", "Forbidden");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "INSERT INTO `users_vin_messages` (`vin_id`,`is_customer`,`text`,`time`) VALUES (?,?,?,?)"),
                cancellationToken,
                vinId, 1, body, now).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `users_vin` SET `viewed`=0 WHERE `id`=?"),
                cancellationToken,
                vinId).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new StorefrontVinRequestWriteResult(true, "ok", "Message sent", vinId, 1);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static string BuildRequestHtml(IReadOnlyDictionary<string, string> fields, string parts)
    {
        var sb = new StringBuilder();
        sb.Append("<table class=\"table\" style=\"width:100%;border-spacing: 0px;\">");
        sb.Append("<thead style=\"text-align:left;\"><tr><th>Field</th><th>Value</th></tr></thead><tbody>");
        foreach (var field in PhpSellerRequest.Fields)
        {
            var value = fields.GetValueOrDefault(field.Name, "");
            sb.Append("<tr><td>")
                .Append(WebUtility.HtmlEncode(field.Label))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(value))
                .Append("</td></tr>");
        }

        sb.Append("<tr><td>Parts needed</td><td>")
            .Append(WebUtility.HtmlEncode(parts))
            .Append("</td></tr></tbody></table>");
        return sb.ToString();
    }

    private static Dictionary<string, string> Normalize(IReadOnlyDictionary<string, string>? fields)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (fields is null)
        {
            return map;
        }

        foreach (var pair in fields)
        {
            map[pair.Key] = (pair.Value ?? string.Empty).Trim();
        }

        return map;
    }

    private static StorefrontVinRequestWriteResult Fail(string code, string message)
        => new(false, code, message, 0, 0);
}
