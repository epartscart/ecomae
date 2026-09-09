using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpNotificationToggleRequest(long NotificationId, string? Type, int SetSend);

public interface ICpNotificationSettingsWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(CpNotificationToggleRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Live PHP <c>notifications.php</c> <c>action=set_send</c>. Template edit, factory restore, and send stay Classic.</summary>
public sealed class CpNotificationSettingsWriteService : ICpNotificationSettingsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpNotificationSettingsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string? NormalizeType(string? type)
    {
        var raw = (type ?? string.Empty).Trim().ToLowerInvariant();
        return raw is "email" or "sms" ? raw : null;
    }

    public static int NormalizeFlag(int setSend)
        => setSend == 0 ? 0 : 1;

    public async Task<ErpSimpleWriteResult> ToggleAsync(CpNotificationToggleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NotificationId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A notification id is required.");
        }

        var type = NormalizeType(request.Type);
        if (type is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Type must be email or sms.");
        }

        var flag = NormalizeFlag(request.SetSend);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "notifications_settings", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "notifications_settings table is not provisioned. Schema ensure stays on the Classic twin.");
            }

            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `notifications_settings` WHERE `id` = ?"),
                cancellationToken,
                request.NotificationId).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Notification setting was not found.");
            }

            if (flag == 1)
            {
                var foreseenCol = type == "email" ? "foreseen_email" : "foreseen_sms";
                var foreseen = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT IFNULL(`" + foreseenCol + "`, 0) FROM `notifications_settings` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    request.NotificationId).ConfigureAwait(false);
                if (foreseen == 0)
                {
                    return ErpSimpleWriteResult.Fail("not_foreseen", "This event does not support that channel.");
                }
            }

            var onCol = type == "email" ? "email_on" : "sms_on";
            var writes = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `notifications_settings` SET `" + onCol + "` = ? WHERE `id` = ?"),
                cancellationToken,
                flag,
                request.NotificationId).ConfigureAwait(false);
            if (writes <= 0)
            {
                return ErpSimpleWriteResult.Fail("unchanged", "Channel flag was not updated.");
            }

            return ErpSimpleWriteResult.Ok(
                flag == 1 ? "Channel enabled." : "Channel disabled.",
                request.NotificationId);
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }
}
