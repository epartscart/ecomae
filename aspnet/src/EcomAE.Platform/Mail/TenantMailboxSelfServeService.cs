using System.Globalization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Mail;

public interface ITenantMailboxSelfServe
{
    Task<PlatformMailWriteResult> WriteAsync(TenantMailboxSelfServeRequest request, CancellationToken cancellationToken);
}

public sealed record TenantMailboxSelfServeRequest(
    string Action,
    string RequestHost,
    string? LocalPart,
    string? Address,
    string? Password,
    string? GroupsJson,
    string? To = null,
    string? Subject = null,
    string? Body = null,
    string? LoginEmail = null,
    bool AllowPlatformErp = false);

public sealed class TenantMailboxSelfServeService : ITenantMailboxSelfServe
{
    private readonly IPlatformMailService _mail;
    private readonly ICpUserWriteService _users;
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IMailboxComposer _composer;

    public TenantMailboxSelfServeService(
        IPlatformMailService mail,
        ICpUserWriteService users,
        IErpWriteConnectionFactory connections,
        IMailboxComposer composer)
    {
        _mail = mail;
        _users = users;
        _connections = connections;
        _composer = composer;
    }

    public async Task<PlatformMailWriteResult> WriteAsync(TenantMailboxSelfServeRequest request, CancellationToken cancellationToken)
    {
        if (!TenantMailboxGuard.TryBind(
                request.Action,
                null,
                request.Address,
                request.LocalPart,
                request.RequestHost,
                out var domain,
                out var siteKey,
                out var address,
                out var errorCode,
                out var errorMessage,
                request.AllowPlatformErp))
        {
            return new PlatformMailWriteResult(false, 0, errorCode, errorMessage);
        }

        if (string.Equals(request.Action, "send", StringComparison.OrdinalIgnoreCase))
        {
            var sent = await _composer.SendAsync(
                siteKey,
                superCp: false,
                address.Length > 0 ? address : request.Address,
                request.To,
                request.Subject,
                request.Body,
                cancellationToken,
                request.LoginEmail,
                request.AllowPlatformErp).ConfigureAwait(false);
            return new PlatformMailWriteResult(sent.Ok, sent.Ok ? 1 : 0, sent.Code, sent.Message);
        }

        var action = string.Equals(request.Action, "create_staff", StringComparison.OrdinalIgnoreCase)
            ? "create_mailbox"
            : request.Action;
        var mail = await _mail.WriteAsync(
            new PlatformMailWriteRequest(action, domain, siteKey, request.LocalPart, address, request.Password),
            cancellationToken).ConfigureAwait(false);
        if (!mail.Succeeded || !string.Equals(request.Action, "create_staff", StringComparison.OrdinalIgnoreCase))
        {
            return mail;
        }

        if (string.IsNullOrWhiteSpace(request.Password) || !PlatformMailNormalize.IsEmail(address))
        {
            return new PlatformMailWriteResult(false, mail.Writes, "password_required", "Set a password so this tenant can create both the mailbox and the CP login.");
        }

        var groupsJson = string.IsNullOrWhiteSpace(request.GroupsJson)
            ? await BackendGroupsJsonAsync(cancellationToken).ConfigureAwait(false)
            : request.GroupsJson;
        var staff = await _users.CreateAsync(
            address,
            1,
            null,
            0,
            request.Password,
            1,
            1,
            """[{"name":"name","value":"Tenant mailbox user"}]""",
            groupsJson,
            cancellationToken).ConfigureAwait(false);
        if (!staff.Succeeded && !string.Equals(staff.Code, "exists", StringComparison.OrdinalIgnoreCase))
        {
            return new PlatformMailWriteResult(false, mail.Writes, staff.Code, staff.Message);
        }

        return new PlatformMailWriteResult(
            true,
            mail.Writes + Math.Max(staff.Writes, 0),
            "ok",
            "Mailbox and CP login ready for " + address + " on site_key " + siteKey + ".");
    }

    private async Task<string> BackendGroupsJsonAsync(CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return "[3]";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 8";
        var ids = new List<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(Convert.ToInt32(reader[0], CultureInfo.InvariantCulture));
        }

        if (ids.Count == 0)
        {
            ids.Add(3);
        }

        return "[" + string.Join(",", ids.Select(id => id.ToString(CultureInfo.InvariantCulture))) + "]";
    }
}
