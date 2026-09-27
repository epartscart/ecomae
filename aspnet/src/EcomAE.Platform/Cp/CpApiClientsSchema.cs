using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>PHP <c>epc_api_clients_ensure_table()</c>.</summary>
public static class CpApiClientsSchema
{
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_api_clients` ("
            + " `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,"
            + " `client_key_hash` CHAR(64) NOT NULL,"
            + " `client_key_prefix` VARCHAR(32) NOT NULL DEFAULT '',"
            + " `product` ENUM('catalog','price_pro','both') NOT NULL DEFAULT 'catalog',"
            + " `label` VARCHAR(120) NOT NULL DEFAULT '',"
            + " `contact_email` VARCHAR(190) NOT NULL DEFAULT '',"
            + " `active` TINYINT(1) NOT NULL DEFAULT 1,"
            + " `daily_limit` INT NOT NULL DEFAULT 1000,"
            + " `calls_today` INT NOT NULL DEFAULT 0,"
            + " `calls_reset_date` DATE NULL,"
            + " `allowed_actions_json` TEXT NOT NULL,"
            + " `time_created` INT NOT NULL DEFAULT 0,"
            + " `time_updated` INT NOT NULL DEFAULT 0,"
            + " UNIQUE KEY `client_key_hash` (`client_key_hash`),"
            + " KEY `product_active` (`product`, `active`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_umapi_usage_log` ADD COLUMN `client_id` INT UNSIGNED NULL DEFAULT NULL AFTER `source`,"
            + " ADD KEY `client_date` (`client_id`, `usage_date`)",
            cancellationToken).ConfigureAwait(false);
    }
}
