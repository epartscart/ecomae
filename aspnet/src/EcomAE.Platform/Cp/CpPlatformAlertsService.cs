using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Super-CP platform alert contacts: where the hourly tenant health monitor
/// emails/WhatsApps page or login failures. Stored in the site DB so the
/// server-side monitor can read the same row.
/// </summary>
public interface ICpPlatformAlertsService
{
    Task<CpPlatformAlertsConfig> ReadAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(
        CpPlatformAlertsConfig config,
        CancellationToken cancellationToken = default);
}

public sealed record CpPlatformAlertsConfig(
    string AlertEmail,
    bool WhatsappEnabled,
    string WhatsappApiToken,
    string WhatsappPhoneNumberId,
    string WhatsappApiVersion,
    string WhatsappToNumber)
{
    public static readonly CpPlatformAlertsConfig Empty = new(string.Empty, false, string.Empty, string.Empty, "v19.0", string.Empty);
}

public sealed class CpPlatformAlertsService : ICpPlatformAlertsService
{
    private const string Table = "ecomae_platform_alert_config";

    private readonly IErpWriteConnectionFactory _connections;

    public CpPlatformAlertsService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    private static async Task EnsureAsync(System.Data.Common.DbConnection connection, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(connection, null, $"""
            CREATE TABLE IF NOT EXISTS `{Table}` (
                `id` int(11) NOT NULL DEFAULT 1,
                `alert_email` varchar(255) NOT NULL DEFAULT '',
                `whatsapp_enabled` tinyint(1) NOT NULL DEFAULT 0,
                `wa_api_token` varchar(255) NOT NULL DEFAULT '',
                `wa_phone_number_id` varchar(64) NOT NULL DEFAULT '',
                `wa_api_version` varchar(16) NOT NULL DEFAULT 'v19.0',
                `wa_to_number` varchar(32) NOT NULL DEFAULT '',
                `time_updated` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, ct).ConfigureAwait(false);
    }

    public async Task<CpPlatformAlertsConfig> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPlatformAlertsConfig.Empty;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT `alert_email`,`whatsapp_enabled`,`wa_api_token`,`wa_phone_number_id`,`wa_api_version`,`wa_to_number` FROM `{Table}` WHERE `id`=1 LIMIT 1";
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return CpPlatformAlertsConfig.Empty;
            }

            return new CpPlatformAlertsConfig(
                reader.GetString(0),
                reader.GetInt32(1) != 0,
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5));
        }
        catch (System.Data.Common.DbException)
        {
            return CpPlatformAlertsConfig.Empty;
        }
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpPlatformAlertsConfig config,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new ErpSimpleWriteResult(false, "migration", "TenantRegistry DB is not configured.", 0, 0);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var sql = ErpDb.Positional($"""
            INSERT INTO `{Table}`
                (`id`,`alert_email`,`whatsapp_enabled`,`wa_api_token`,`wa_phone_number_id`,`wa_api_version`,`wa_to_number`,`time_updated`)
            VALUES (1,?,?,?,?,?,?,?,UNIX_TIMESTAMP())
            ON DUPLICATE KEY UPDATE
                `alert_email`=VALUES(`alert_email`),
                `whatsapp_enabled`=VALUES(`whatsapp_enabled`),
                `wa_api_token`=VALUES(`wa_api_token`),
                `wa_phone_number_id`=VALUES(`wa_phone_number_id`),
                `wa_api_version`=VALUES(`wa_api_version`),
                `wa_to_number`=VALUES(`wa_to_number`),
                `time_updated`=VALUES(`time_updated`)
            """);
        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            sql,
            cancellationToken,
            config.AlertEmail,
            config.WhatsappEnabled ? 1 : 0,
            config.WhatsappApiToken,
            config.WhatsappPhoneNumberId,
            config.WhatsappApiVersion,
            config.WhatsappToNumber).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "saved", "Alert settings saved.", 1, writes);
    }
}
