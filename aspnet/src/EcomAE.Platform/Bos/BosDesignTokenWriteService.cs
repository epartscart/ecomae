using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>design_tokens</c> <c>save_token</c> / <c>epc_design_tokens_save</c>.
/// Preview CSS emit and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. This write uses the platform operator PDO.
/// </summary>
public interface IBosDesignTokenWriteService
{
    Task<ErpSimpleWriteResult> SaveTokenAsync(
        string? siteKey,
        string? settingKey,
        string? value,
        CancellationToken cancellationToken = default);
}

public sealed class BosDesignTokenWriteService : IBosDesignTokenWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SettingKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlySet<string> AllowedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "brand_primary",
        "brand_secondary",
        "brand_accent",
        "brand_bg_dark",
        "brand_logo_url",
        "brand_login_bg",
        "brand_font",
        "brand_radius",
        "brand_sidebar_bg",
        "brand_header_bg",
        "white_label_login",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public BosDesignTokenWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', ...)</c> — no <c>strtolower</c>.</summary>
    public static string PhpSettingKey(string? raw)
        => SettingKeySafe.Replace(raw ?? "", "");

    public async Task<ErpSimpleWriteResult> SaveTokenAsync(
        string? siteKey,
        string? settingKey,
        string? value,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        var setting = PhpSettingKey(settingKey);
        if (key.Length == 0 || setting.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key or setting_key");
        }

        if (!AllowedKeys.Contains(setting))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown setting_key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_settings` (`site_key`, `setting_key`, `setting_value`, `updated_at`)
                    VALUES (?, ?, ?, ?)
                    ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `updated_at` = VALUES(`updated_at`)
                    """),
                cancellationToken,
                key,
                setting,
                value ?? "",
                now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Design token saved", id > 0 ? id : 1);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Settings table is missing — schema-ensure stays Classic.");
        }
    }
}
