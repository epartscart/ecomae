using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_design_tokens.php</c> twin of <c>epc_design_tokens_save</c>.
/// CSS emit and schema-ensure stay Classic. This service does not invent a send.
/// </summary>
public interface ICpDesignTokensWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpDesignTokenSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpDesignTokenSaveRequest(
    string? SiteKey,
    string? SettingKey,
    string? SettingValue);

public sealed class CpDesignTokensWriteService : ICpDesignTokensWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyList<string> AllowedKeys =
    [
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
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public CpDesignTokensWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string NormalizeSettingKey(string? raw)
    {
        var key = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return AllowedKeys.Contains(key, StringComparer.Ordinal) ? key : string.Empty;
    }

    public static string Clip(string? raw, int max)
    {
        var text = (raw ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpDesignTokenSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Site key is required");
        }

        var settingKey = NormalizeSettingKey(request.SettingKey);
        if (settingKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Setting key is not allowed");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var value = Clip(request.SettingValue, 256);
        if (settingKey == "white_label_login")
        {
            var flag = value is "1" or "true" or "on" or "yes";
            value = flag ? "1" : "0";
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_settings` (`site_key`, `setting_key`, `setting_value`, `updated_at`) VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE `setting_value`=VALUES(`setting_value`), `updated_at`=VALUES(`updated_at`)"),
                cancellationToken,
                siteKey,
                settingKey,
                value,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Design token saved.", 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Settings table is missing — schema-ensure stays Classic.");
        }
    }
}
