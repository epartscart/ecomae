using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// PHP <c>epc_portal_db_ensure()</c> (content/general_pages/epc_portal_db.php) for the
/// <c>epc_portal_site_settings</c> table: created lazily on every portal settings page entry,
/// then widened additively column by column.
/// </summary>
public static class CpPortalSchema
{
    private static readonly (string Column, string Definition)[] SiteSettingsColumns =
    [
        ("domain_path", "VARCHAR(255) NOT NULL DEFAULT '' AFTER `tagline`"),
        ("contact_json", "TEXT NULL AFTER `domain_path`"),
        ("cp_menu_json", "TEXT NULL AFTER `theme_json`"),
        ("theme_template", "VARCHAR(32) NOT NULL DEFAULT 'classic' AFTER `industry_code`"),
        ("access_mode", "VARCHAR(16) NOT NULL DEFAULT 'full' AFTER `theme_template`"),
        ("erp_modules_json", "TEXT NULL AFTER `access_mode`"),
        ("cp_default_lang", "VARCHAR(8) NOT NULL DEFAULT 'en' AFTER `erp_modules_json`"),
        ("country_code", "CHAR(2) NOT NULL DEFAULT 'AE' AFTER `cp_default_lang`"),
        ("integrations_json", "TEXT NULL AFTER `cp_menu_json`"),
    ];

    public static async Task EnsureSiteSettingsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_portal_site_settings` ("
            + " `host` VARCHAR(120) NOT NULL PRIMARY KEY,"
            + " `industry_code` VARCHAR(32) NOT NULL DEFAULT 'auto_parts',"
            + " `system_name` VARCHAR(120) NOT NULL DEFAULT '',"
            + " `hub_name` VARCHAR(120) NOT NULL DEFAULT '',"
            + " `tagline` VARCHAR(255) NOT NULL DEFAULT '',"
            + " `domain_path` VARCHAR(255) NOT NULL DEFAULT '',"
            + " `contact_json` TEXT NULL,"
            + " `enabled_packs_json` TEXT NULL,"
            + " `theme_json` TEXT NULL,"
            + " `updated_at` INT NOT NULL DEFAULT 0"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);

        var existing = await LoadColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        foreach (var (column, definition) in SiteSettingsColumns)
        {
            if (existing.Contains(column))
            {
                continue;
            }

            await ErpDb.TryExecuteAsync(
                connection,
                "ALTER TABLE `epc_portal_site_settings` ADD COLUMN `" + column + "` " + definition,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<HashSet<string>> LoadColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT `COLUMN_NAME` FROM `information_schema`.`COLUMNS` "
            + "WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'epc_portal_site_settings'";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }
}
