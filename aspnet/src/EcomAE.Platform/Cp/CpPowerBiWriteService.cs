using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_power_bi.php</c> twin of <c>epc_power_bi_ensure_schema</c>,
/// <c>epc_power_bi_configure</c> and <c>epc_power_bi_register_report</c>.
/// Azure embed token mint stays out of scope until the tenant supplies AAD credentials.
/// </summary>
public interface ICpPowerBiWriteService
{
    Task<ErpSimpleWriteResult> SaveConfigAsync(
        CpPowerBiSaveConfigRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddReportAsync(
        CpPowerBiAddReportRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpPowerBiSaveConfigRequest(
    string? SiteKey,
    string? WorkspaceId,
    string? AzureTenantId,
    string? DefaultReportId,
    string? DefaultDatasetId,
    string? EmbedUrl,
    string? EmbedMode,
    string? Notes);

public sealed record CpPowerBiAddReportRequest(
    string? SiteKey,
    string? ReportId,
    string? ReportName,
    string? DatasetId,
    string? Category,
    string? EmbedUrl);

public sealed class CpPowerBiWriteService : ICpPowerBiWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyList<string> EmbedModes = ["none", "url", "azure"];

    private readonly IErpWriteConnectionFactory _connections;

    public CpPowerBiWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string NormalizeEmbedMode(string? raw)
    {
        var mode = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return EmbedModes.Contains(mode, StringComparer.Ordinal) ? mode : "none";
    }

    public static string Clip(string? raw, int max)
    {
        var text = (raw ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    /// <summary>PHP <c>epc_power_bi_ensure_schema()</c> MySQL branch.</summary>
    public static async Task EnsureSchemaAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default)
    {
        await ErpDb.ExecuteAsync(
            connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_power_bi_config` ("
            + "`id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,"
            + "`site_key` VARCHAR(64) NOT NULL DEFAULT '__platform__',"
            + "`workspace_id` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`azure_tenant_id` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`default_report_id` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`default_dataset_id` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`embed_url` VARCHAR(512) NOT NULL DEFAULT '',"
            + "`embed_mode` VARCHAR(16) NOT NULL DEFAULT 'none',"
            + "`notes` VARCHAR(512) NOT NULL DEFAULT '',"
            + "`active` TINYINT(1) NOT NULL DEFAULT 0,"
            + "`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,"
            + "`updated_at` DATETIME NULL ON UPDATE CURRENT_TIMESTAMP,"
            + "UNIQUE KEY `site` (`site_key`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_power_bi_reports` ("
            + "`id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,"
            + "`site_key` VARCHAR(64) NOT NULL,"
            + "`report_id` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`report_name` VARCHAR(128) NOT NULL DEFAULT '',"
            + "`dataset_id` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`category` VARCHAR(32) NOT NULL DEFAULT 'finance',"
            + "`embed_url` VARCHAR(512) NOT NULL DEFAULT '',"
            + "`active` TINYINT(1) NOT NULL DEFAULT 1,"
            + "`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,"
            + "INDEX `idx_site` (`site_key`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> SaveConfigAsync(
        CpPowerBiSaveConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Site key is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var mode = NormalizeEmbedMode(request.EmbedMode);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_power_bi_config` (`site_key`,`workspace_id`,`azure_tenant_id`,`default_report_id`,`default_dataset_id`,`embed_url`,`embed_mode`,`notes`,`active`) VALUES (?,?,?,?,?,?,?,?,1) ON DUPLICATE KEY UPDATE `workspace_id`=VALUES(`workspace_id`), `azure_tenant_id`=VALUES(`azure_tenant_id`), `default_report_id`=VALUES(`default_report_id`), `default_dataset_id`=VALUES(`default_dataset_id`), `embed_url`=VALUES(`embed_url`), `embed_mode`=VALUES(`embed_mode`), `notes`=VALUES(`notes`), `active`=1"),
                cancellationToken,
                siteKey,
                Clip(request.WorkspaceId, 64),
                Clip(request.AzureTenantId, 64),
                Clip(request.DefaultReportId, 64),
                Clip(request.DefaultDatasetId, 64),
                Clip(request.EmbedUrl, 512),
                mode,
                Clip(request.Notes, 512)).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Power BI config saved for " + siteKey + ".", 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Power BI config write failed.");
        }
    }

    public async Task<ErpSimpleWriteResult> AddReportAsync(
        CpPowerBiAddReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Site key is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var name = Clip(request.ReportName, 128);
        if (name.Length == 0)
        {
            name = "Report";
        }

        var category = Clip(request.Category, 32);
        if (category.Length == 0)
        {
            category = "finance";
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_power_bi_reports` (`site_key`,`report_id`,`report_name`,`dataset_id`,`category`,`embed_url`,`active`) VALUES (?,?,?,?,?,?,1)"),
                cancellationToken,
                siteKey,
                Clip(request.ReportId, 64),
                name,
                Clip(request.DatasetId, 64),
                category,
                Clip(request.EmbedUrl, 512)).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Report registered.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Power BI report write failed.");
        }
    }
}
