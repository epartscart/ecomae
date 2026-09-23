using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpPmProfile(int GroupId, string Name, string Code, decimal MarginPercent, decimal? VatPercent);

public sealed record CpPmStorage(int Id, string Name, bool Hidden);

public sealed record CpPmStorageRule(long Id, int StorageId, string StorageName, string Manufacturer, string Article, decimal MarginPercent, bool Visible);

public sealed record CpPmProfileRule(long Id, int GroupId, string GroupName, string Manufacturer, string Article, decimal MarginPercent, bool Visible);

public sealed record CpPmCustomer(long UserId, string Email, string Name, string Surname)
{
    public string Label => ("ID " + UserId.ToString(CultureInfo.InvariantCulture) + " — " + (Email + " " + Name + " " + Surname).Trim()).Trim();
}

public sealed record CpPmDemoScenario(
    string Title,
    string ProfileLabel,
    string Brand,
    string Article,
    bool Visible,
    decimal? FinalPrice,
    decimal? TotalMarginPercent,
    string BreakdownText);

public sealed record CpPmPreviewRequest(int GroupId, string Brand, string Article, decimal BasePrice, int StorageId);

public sealed record CpPmPreview(bool Visible, decimal FinalPrice, decimal TotalMarginPercent, string BreakdownText, string HiddenReason);

public sealed record CpPriceManagementPage(
    bool Available,
    string? Unavailable,
    IReadOnlyList<CpPmProfile> Profiles,
    IReadOnlyList<CpPmStorage> Storages,
    IReadOnlyList<CpPmStorageRule> StorageOverall,
    IReadOnlyList<CpPmStorageRule> StorageBrand,
    IReadOnlyList<CpPmStorageRule> StorageArticle,
    IReadOnlyList<CpPmProfileRule> BrandRules,
    IReadOnlyList<CpPmProfileRule> ArticleRules,
    IReadOnlyList<CpPmCustomer> Customers,
    IReadOnlyList<string> Brands,
    string VatPercent,
    string GuestMarginPercent,
    IReadOnlyList<CpPmDemoScenario> Demo,
    CpPmPreview? Preview)
{
    public int StorageRulesTotal => StorageOverall.Count + StorageBrand.Count + StorageArticle.Count;

    public static CpPriceManagementPage Empty(string? unavailable) => new(
        false, unavailable, [], [], [], [], [], [], [], [], [], "5.00", "0.00", [], null);
}

public interface ICpPriceManagementService
{
    Task<CpPriceManagementPage> LoadAsync(CpPmPreviewRequest? preview, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ApplyAsync(string? action, IReadOnlyDictionary<string, string> fields, IReadOnlyList<string> brands, CancellationToken cancellationToken = default);
}

/// <summary>
/// PHP <c>cp/content/shop/pricing/price_management.php</c> twin (profiles, guest/VAT settings,
/// brand/article rules, assignment, bulk visibility, live calculator, demo scenarios).
/// Storage-level actions are delegated to <see cref="ICpPriceStorageRuleWriteService"/> (epc_pm_storage_panel.php).
/// </summary>
public sealed class CpPriceManagementService : ICpPriceManagementService
{
    public const int CustomersLimit = 300;
    public const int BrandsLimit = 3000;
    public const string DefaultVat = "5.00";
    public const string DefaultGuestMargin = "0.00";

    public static readonly string[] ProfileActions =
    [
        "save_vat", "save_profile_margin", "save_guest_margin", "save_profile_vat", "create_profile",
        "save_bulk_visibility", "save_rule", "save_article_rule", "delete_article_rule", "delete_rule", "assign_profile"
    ];

    public static readonly string[] StorageActions =
    [
        "save_storage_rule", "delete_storage_rule",
        "save_storage_brand_rule", "delete_storage_brand_rule",
        "save_storage_article_rule", "delete_storage_article_rule"
    ];

    private static readonly Regex CodeClean = new(@"[^a-z0-9_\-]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex KeyClean = new(@"[^A-Z0-9_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpPriceStorageRuleWriteService _storageRules;

    public CpPriceManagementService(IErpWriteConnectionFactory connections, ICpPriceStorageRuleWriteService storageRules)
    {
        _connections = connections;
        _storageRules = storageRules;
    }

    public static string ProfileCode(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        v = CodeClean.Replace(v, "_");
        return v.Trim('_', '-');
    }

    public static string FormatBreakdown(IReadOnlyList<EpcPricing.PriceStep> steps)
    {
        if (steps.Count == 0)
        {
            return "No extra margin";
        }

        return string.Join("; ", steps.Select(s => s.Label + " +" + F2(s.Percent) + "% → " + F2(s.PriceAfter)));
    }

    public static string F2(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    public async Task<CpPriceManagementPage> LoadAsync(CpPmPreviewRequest? preview, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPriceManagementPage.Empty("No database");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var translate = Translator(connection, cancellationToken);

            var profiles = await ProfilesAsync(connection, translate, cancellationToken).ConfigureAwait(false);
            var storages = await StoragesAsync(connection, cancellationToken).ConfigureAwait(false);
            var overall = await StorageRulesAsync(connection, "epc_price_storage_rules", false, false, cancellationToken).ConfigureAwait(false);
            var sBrand = await StorageRulesAsync(connection, "epc_price_storage_brand_rules", true, false, cancellationToken).ConfigureAwait(false);
            var sArticle = await StorageRulesAsync(connection, "epc_price_storage_article_rules", true, true, cancellationToken).ConfigureAwait(false);
            var brandRules = await ProfileRulesAsync(connection, "epc_price_profile_brand_rules", false, translate, cancellationToken).ConfigureAwait(false);
            var articleRules = await ProfileRulesAsync(connection, "epc_price_profile_article_rules", true, translate, cancellationToken).ConfigureAwait(false);
            var customers = await CustomersAsync(connection, cancellationToken).ConfigureAwait(false);
            var brands = await BrandsAsync(connection, cancellationToken).ConfigureAwait(false);
            var vat = await SettingAsync(connection, "vat_percent", DefaultVat, cancellationToken).ConfigureAwait(false);
            var guest = await SettingAsync(connection, "guest_margin_percent", DefaultGuestMargin, cancellationToken).ConfigureAwait(false);
            var demo = await DemoAsync(connection, profiles, cancellationToken).ConfigureAwait(false);

            CpPmPreview? previewResult = null;
            if (preview is { GroupId: > 0, BasePrice: > 0 })
            {
                var r = await EpcPricing.ApplyPriceRulesWithBreakdownAsync(
                    connection, preview.GroupId, preview.Brand, preview.BasePrice, 0m, preview.Article, preview.StorageId, cancellationToken).ConfigureAwait(false);
                previewResult = new CpPmPreview(r.Visible, Math.Round(r.FinalPrice, 2, MidpointRounding.AwayFromZero), r.TotalMarginPercent, FormatBreakdown(r.Steps), r.Visible ? "" : (r.HiddenReason.Length > 0 ? r.HiddenReason : "Hidden by visibility rule"));
            }

            return new CpPriceManagementPage(true, null, profiles, storages, overall, sBrand, sArticle, brandRules, articleRules, customers, brands, vat, guest, demo, previewResult);
        }
        catch (DbException ex)
        {
            return CpPriceManagementPage.Empty(ex.Message);
        }
    }

    public async Task<ErpSimpleWriteResult> ApplyAsync(string? action, IReadOnlyDictionary<string, string> fields, IReadOnlyList<string> brands, CancellationToken cancellationToken = default)
    {
        var act = (action ?? string.Empty).Trim().ToLowerInvariant();
        string F(string key) => fields.TryGetValue(key, out var v) ? v : string.Empty;
        long L(string key) => long.TryParse(F(key).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

        if (StorageActions.Contains(act))
        {
            return await _storageRules.ApplyAsync(act, L("rule_id"), L("storage_id"), F("manufacturer"), F("article"), F("margin_percent"), (int)L("visible"), cancellationToken).ConfigureAwait(false);
        }

        if (!ProfileActions.Contains(act))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown price-management action.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            return act switch
            {
                "save_vat" => await SaveSettingAsync(connection, "vat_percent", F("vat_percent"), 0m, 100m, "VAT must be from 0 to 100", "VAT setting saved", cancellationToken).ConfigureAwait(false),
                "save_guest_margin" => await SaveSettingAsync(connection, "guest_margin_percent", F("guest_margin_percent"), 0m, 1000m, "Guest margin must be from 0 to 1000", "Guest / non-login margin saved", cancellationToken).ConfigureAwait(false),
                "save_profile_margin" => await SaveProfileMarginAsync(connection, (int)L("group_id"), F("profile_margin_percent"), cancellationToken).ConfigureAwait(false),
                "save_profile_vat" => await SaveProfileVatAsync(connection, (int)L("group_id"), F("profile_vat_percent"), cancellationToken).ConfigureAwait(false),
                "create_profile" => await CreateProfileAsync(connection, F("profile_name"), F("profile_code"), cancellationToken).ConfigureAwait(false),
                "save_bulk_visibility" => await SaveBulkVisibilityAsync(connection, (int)L("group_id"), (int)L("visible"), brands, cancellationToken).ConfigureAwait(false),
                "save_rule" => await SaveRuleAsync(connection, (int)L("group_id"), F("manufacturer"), F("margin_percent"), (int)L("visible"), cancellationToken).ConfigureAwait(false),
                "save_article_rule" => await SaveArticleRuleAsync(connection, (int)L("group_id"), F("manufacturer"), F("article"), F("margin_percent"), (int)L("visible"), cancellationToken).ConfigureAwait(false),
                "delete_rule" => await DeleteRuleAsync(connection, "epc_price_profile_brand_rules", L("rule_id"), "Brand rule deleted", cancellationToken).ConfigureAwait(false),
                "delete_article_rule" => await DeleteRuleAsync(connection, "epc_price_profile_article_rules", L("rule_id"), "Article rule deleted", cancellationToken).ConfigureAwait(false),
                "assign_profile" => await AssignProfileAsync(connection, L("user_id"), (int)L("group_id"), cancellationToken).ConfigureAwait(false),
                _ => ErpSimpleWriteResult.Fail("invalid", "Unknown price-management action.")
            };
        }
        catch (DbException ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    // ---- writes (PHP POST branch) ----

    private static bool TryDecimal(string raw, out decimal value)
        => decimal.TryParse((raw ?? string.Empty).Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    private static async Task<ErpSimpleWriteResult> SaveSettingAsync(DbConnection c, string key, string raw, decimal min, decimal max, string rangeError, string ok, CancellationToken ct)
    {
        TryDecimal(raw, out var value);
        if (value < min || value > max)
        {
            return ErpSimpleWriteResult.Fail("invalid", rangeError);
        }

        await ErpDb.ExecuteAsync(c, null, ErpDb.Positional(
            "INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
            ct, key, F2(value)).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(ok, 0);
    }

    private static async Task<ErpSimpleWriteResult> SaveProfileMarginAsync(DbConnection c, int groupId, string raw, CancellationToken ct)
    {
        if (groupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select profile");
        }

        TryDecimal(raw, out var margin);
        if (margin < 0m || margin > 1000m)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Profile margin must be from 0 to 1000");
        }

        await ErpDb.ExecuteAsync(c, null, ErpDb.Positional("UPDATE `epc_price_profiles` SET `margin_percent` = ? WHERE `group_id` = ?"), ct, F2(margin), groupId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Profile overall margin saved", groupId);
    }

    private static async Task<ErpSimpleWriteResult> SaveProfileVatAsync(DbConnection c, int groupId, string raw, CancellationToken ct)
    {
        if (groupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select profile");
        }

        raw = (raw ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            await ErpDb.ExecuteAsync(c, null, ErpDb.Positional("UPDATE `epc_price_profiles` SET `vat_percent` = NULL WHERE `group_id` = ?"), ct, groupId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Profile VAT saved", groupId);
        }

        TryDecimal(raw, out var vat);
        if (vat < 0m || vat > 100m)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Profile VAT must be from 0 to 100");
        }

        await ErpDb.ExecuteAsync(c, null, ErpDb.Positional("UPDATE `epc_price_profiles` SET `vat_percent` = ? WHERE `group_id` = ?"), ct, F2(vat), groupId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Profile VAT saved", groupId);
    }

    private static async Task<ErpSimpleWriteResult> CreateProfileAsync(DbConnection c, string nameRaw, string codeRaw, CancellationToken ct)
    {
        var name = (nameRaw ?? string.Empty).Trim();
        var code = ProfileCode(string.IsNullOrEmpty(codeRaw) ? name : codeRaw);
        if (name.Length == 0 || code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Enter profile name");
        }

        var existing = await ErpDb.LongAsync(c, null, ErpDb.Positional("SELECT IFNULL((SELECT `id` FROM `epc_price_profiles` WHERE `code` = ? LIMIT 1), 0)"), ct, code).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Fail("duplicate", "This profile code already exists");
        }

        var strKey = "EPC_PROFILE_" + KeyClean.Replace(code.ToUpperInvariant(), "_");
        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)"), ct, strKey, name).ConfigureAwait(false);
        foreach (var lang in new[] { "en", "ru" })
        {
            await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
                "INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)"), ct, strKey, lang, name).ConfigureAwait(false);
        }

        var newGroupId = await ErpDb.LongAsync(c, tx, "SELECT IFNULL(MAX(`id`), 0) + 1 FROM `groups`", ct).ConfigureAwait(false);
        var order = await ErpDb.LongAsync(c, tx, "SELECT IFNULL(MAX(`order`), 0) + 1 FROM `groups` WHERE `parent` = 1", ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `groups` (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`, `for_percentage`, `description`, `order`) "
            + "VALUES (?, ?, 0, 2, 1, 1, 0, 0, 0, 0, ?, ?)"), ct, newGroupId, strKey, strKey, order).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "INSERT INTO `epc_price_profiles` (`code`, `group_id`, `created_at`) VALUES (?, ?, ?)"), ct, code, newGroupId, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx,
            "UPDATE `groups` SET `count` = (SELECT COUNT(*) FROM (SELECT `id` FROM `groups` WHERE `parent` = 1) AS x) WHERE `id` = 1", ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Customer price profile created", newGroupId);
    }

    private static async Task<ErpSimpleWriteResult> SaveBulkVisibilityAsync(DbConnection c, int groupId, int visible, IReadOnlyList<string> brands, CancellationToken ct)
    {
        if (groupId <= 0 || brands.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select profile and at least one brand");
        }

        var updated = 0;
        foreach (var raw in brands)
        {
            var brand = EpcPricing.NormalizeBrand(raw);
            if (brand.Length == 0)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(c, null, ErpDb.Positional(
                "INSERT INTO `epc_price_profile_brand_rules` (`group_id`, `manufacturer`, `margin_percent`, `visible`) VALUES (?, ?, 0, ?) ON DUPLICATE KEY UPDATE `visible` = VALUES(`visible`)"),
                ct, groupId, brand, visible != 0 ? 1 : 0).ConfigureAwait(false);
            updated++;
        }

        return new ErpSimpleWriteResult(true, "ok", "Brand visibility updated for " + updated.ToString(CultureInfo.InvariantCulture) + " brand(s)", groupId, updated);
    }

    private static async Task<ErpSimpleWriteResult> SaveRuleAsync(DbConnection c, int groupId, string manufacturerRaw, string marginRaw, int visible, CancellationToken ct)
    {
        var manufacturer = EpcPricing.NormalizeBrand(manufacturerRaw);
        if (groupId <= 0 || manufacturer.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select profile and enter brand");
        }

        TryDecimal(marginRaw, out var margin);
        await ErpDb.ExecuteAsync(c, null, ErpDb.Positional(
            "INSERT INTO `epc_price_profile_brand_rules` (`group_id`, `manufacturer`, `margin_percent`, `visible`) VALUES (?, ?, ?, ?) "
            + "ON DUPLICATE KEY UPDATE `margin_percent` = VALUES(`margin_percent`), `visible` = VALUES(`visible`)"),
            ct, groupId, manufacturer, margin, visible != 0 ? 1 : 0).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Brand rule saved", groupId);
    }

    private static async Task<ErpSimpleWriteResult> SaveArticleRuleAsync(DbConnection c, int groupId, string manufacturerRaw, string articleRaw, string marginRaw, int visible, CancellationToken ct)
    {
        var manufacturer = EpcPricing.NormalizeBrand(manufacturerRaw);
        var article = EpcPricing.NormalizeArticle(articleRaw);
        if (groupId <= 0 || manufacturer.Length == 0 || article.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select profile, brand, and article");
        }

        TryDecimal(marginRaw, out var margin);
        await ErpDb.ExecuteAsync(c, null, ErpDb.Positional(
            "INSERT INTO `epc_price_profile_article_rules` (`group_id`, `manufacturer`, `article`, `margin_percent`, `visible`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?) "
            + "ON DUPLICATE KEY UPDATE `margin_percent` = VALUES(`margin_percent`), `visible` = VALUES(`visible`), `updated_at` = VALUES(`updated_at`)"),
            ct, groupId, manufacturer, article, margin, visible != 0 ? 1 : 0, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Article rule saved", groupId);
    }

    private static async Task<ErpSimpleWriteResult> DeleteRuleAsync(DbConnection c, string table, long ruleId, string ok, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(c, null, ErpDb.Positional("DELETE FROM `" + table + "` WHERE `id` = ?"), ct, ruleId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(ok, ruleId);
    }

    private static async Task<ErpSimpleWriteResult> AssignProfileAsync(DbConnection c, long userId, int groupId, CancellationToken ct)
    {
        if (userId <= 0 || groupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select customer and profile");
        }

        await using var tx = await c.BeginTransactionAsync(ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional(
            "DELETE FROM `users_groups_bind` WHERE `user_id` = ? AND `group_id` IN (SELECT `group_id` FROM `epc_price_profiles`)"), ct, userId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(c, tx, ErpDb.Positional("INSERT INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)"), ct, userId, groupId).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Customer profile assigned", userId);
    }

    // ---- schema (epc_pm_ensure_profile_schema + epc_pricing_ensure_storage_schema) ----

    private static async Task EnsureSchemaAsync(DbConnection c, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(c, """
            CREATE TABLE IF NOT EXISTS `epc_price_settings` (
              `setting_key` varchar(64) NOT NULL,
              `setting_value` varchar(255) NOT NULL DEFAULT '',
              PRIMARY KEY (`setting_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c, """
            CREATE TABLE IF NOT EXISTS `epc_price_profiles` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `code` varchar(64) NOT NULL,
              `group_id` int(11) NOT NULL,
              `margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
              `vat_percent` decimal(10,2) NULL DEFAULT NULL,
              `created_at` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              UNIQUE KEY `x_code` (`code`),
              UNIQUE KEY `x_group` (`group_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c, "ALTER TABLE `epc_price_profiles` ADD `vat_percent` DECIMAL(10,2) NULL DEFAULT NULL AFTER `group_id`", ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c, "ALTER TABLE `epc_price_profiles` ADD `margin_percent` DECIMAL(10,2) NOT NULL DEFAULT 0.00 AFTER `group_id`", ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c, """
            CREATE TABLE IF NOT EXISTS `epc_price_profile_brand_rules` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `group_id` int(11) NOT NULL,
              `manufacturer` varchar(255) NOT NULL,
              `margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
              `visible` tinyint(1) NOT NULL DEFAULT 1,
              PRIMARY KEY (`id`),
              UNIQUE KEY `x_group_brand` (`group_id`, `manufacturer`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c, """
            CREATE TABLE IF NOT EXISTS `epc_price_profile_article_rules` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `group_id` int(11) NOT NULL,
              `manufacturer` varchar(255) NOT NULL,
              `article` varchar(64) NOT NULL,
              `margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
              `visible` tinyint(1) NOT NULL DEFAULT 1,
              `updated_at` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              UNIQUE KEY `x_group_brand_article` (`group_id`, `manufacturer`, `article`),
              KEY `x_article` (`article`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c, """
            CREATE TABLE IF NOT EXISTS `epc_price_storage_rules` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `storage_id` int(11) NOT NULL,
              `margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
              `visible` tinyint(1) NOT NULL DEFAULT 1,
              `updated_at` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              UNIQUE KEY `x_storage` (`storage_id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c, """
            CREATE TABLE IF NOT EXISTS `epc_price_storage_brand_rules` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `storage_id` int(11) NOT NULL,
              `manufacturer` varchar(255) NOT NULL,
              `margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
              `visible` tinyint(1) NOT NULL DEFAULT 1,
              `updated_at` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              UNIQUE KEY `x_storage_brand` (`storage_id`, `manufacturer`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c, """
            CREATE TABLE IF NOT EXISTS `epc_price_storage_article_rules` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `storage_id` int(11) NOT NULL,
              `manufacturer` varchar(255) NOT NULL,
              `article` varchar(64) NOT NULL,
              `margin_percent` decimal(10,2) NOT NULL DEFAULT 0.00,
              `visible` tinyint(1) NOT NULL DEFAULT 1,
              `updated_at` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              UNIQUE KEY `x_storage_brand_article` (`storage_id`, `manufacturer`, `article`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, ct).ConfigureAwait(false);
    }

    // ---- reads ----

    private static Func<string?, Task<string>> Translator(DbConnection c, CancellationToken ct)
    {
        var cache = new Dictionary<string, string>(StringComparer.Ordinal);
        return async key =>
        {
            key = (key ?? string.Empty).Trim();
            if (key.Length == 0)
            {
                return key;
            }

            if (!cache.TryGetValue(key, out var text))
            {
                text = await ErpDb.StringAsync(c, null,
                    ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? ORDER BY `lang_code` = 'en' DESC LIMIT 1"),
                    ct, key).ConfigureAwait(false);
                text = string.IsNullOrWhiteSpace(text) || text == "==Empty string==" ? key : text;
                cache[key] = text;
            }

            return text;
        };
    }

    private static async Task<List<CpPmProfile>> ProfilesAsync(DbConnection c, Func<string?, Task<string>> translate, CancellationToken ct)
    {
        var rows = new List<(int GroupId, string Value, string Code, decimal Margin, decimal? Vat)>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT g.`id`, IFNULL(g.`value`,''), p.`code`, IFNULL(p.`margin_percent`,0), p.`vat_percent` "
                + "FROM `epc_price_profiles` p INNER JOIN `groups` g ON g.`id` = p.`group_id` ORDER BY p.`id` ASC";
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                rows.Add((Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture), r.GetString(1), r.GetString(2),
                    Convert.ToDecimal(r.GetValue(3), CultureInfo.InvariantCulture),
                    r.IsDBNull(4) ? null : Convert.ToDecimal(r.GetValue(4), CultureInfo.InvariantCulture)));
            }
        }

        var list = new List<CpPmProfile>(rows.Count);
        foreach (var row in rows)
        {
            list.Add(new CpPmProfile(row.GroupId, await translate(row.Value).ConfigureAwait(false), row.Code, row.Margin, row.Vat));
        }

        return list;
    }

    private static async Task<List<CpPmStorage>> StoragesAsync(DbConnection c, CancellationToken ct)
    {
        var list = new List<CpPmStorage>();
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`short_name`,''), IFNULL(`hidden`,0) FROM `shop_storages` "
                + "ORDER BY COALESCE(NULLIF(TRIM(`short_name`), ''), `name`) ASC";
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                var id = Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture);
                var shortName = r.GetString(2).Trim();
                var label = (shortName.Length > 0 ? shortName : r.GetString(1)).Trim();
                list.Add(new CpPmStorage(id, label.Length > 0 ? label : "Warehouse #" + id.ToString(CultureInfo.InvariantCulture), Convert.ToInt32(r.GetValue(3), CultureInfo.InvariantCulture) != 0));
            }
        }
        catch (DbException)
        {
        }

        return list;
    }

    private static async Task<List<CpPmStorageRule>> StorageRulesAsync(DbConnection c, string table, bool withBrand, bool withArticle, CancellationToken ct)
    {
        var list = new List<CpPmStorageRule>();
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT r.`id`, r.`storage_id`, IFNULL(COALESCE(NULLIF(TRIM(s.`short_name`), ''), s.`name`),''), "
                + (withBrand ? "IFNULL(r.`manufacturer`,'')" : "''") + ", " + (withArticle ? "IFNULL(r.`article`,'')" : "''")
                + ", IFNULL(r.`margin_percent`,0), IFNULL(r.`visible`,1) FROM `" + table + "` r LEFT JOIN `shop_storages` s ON s.`id` = r.`storage_id` "
                + "ORDER BY 3 ASC" + (withBrand ? ", 4 ASC" : "") + (withArticle ? ", 5 ASC" : "");
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                var storageId = Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture);
                var name = r.GetString(2);
                list.Add(new CpPmStorageRule(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), storageId,
                    name.Length > 0 ? name : "#" + storageId.ToString(CultureInfo.InvariantCulture), r.GetString(3), r.GetString(4),
                    Convert.ToDecimal(r.GetValue(5), CultureInfo.InvariantCulture), Convert.ToInt32(r.GetValue(6), CultureInfo.InvariantCulture) != 0));
            }
        }
        catch (DbException)
        {
        }

        return list;
    }

    private static async Task<List<CpPmProfileRule>> ProfileRulesAsync(DbConnection c, string table, bool withArticle, Func<string?, Task<string>> translate, CancellationToken ct)
    {
        var rows = new List<CpPmProfileRule>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT r.`id`, r.`group_id`, IFNULL(g.`value`,''), IFNULL(r.`manufacturer`,''), " + (withArticle ? "IFNULL(r.`article`,'')" : "''")
                + ", IFNULL(r.`margin_percent`,0), IFNULL(r.`visible`,1) FROM `" + table + "` r INNER JOIN `groups` g ON g.`id` = r.`group_id` "
                + "ORDER BY g.`order`, r.`manufacturer`" + (withArticle ? ", r.`article`" : "");
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                rows.Add(new CpPmProfileRule(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture),
                    r.GetString(2), r.GetString(3), r.GetString(4), Convert.ToDecimal(r.GetValue(5), CultureInfo.InvariantCulture), Convert.ToInt32(r.GetValue(6), CultureInfo.InvariantCulture) != 0));
            }
        }

        var list = new List<CpPmProfileRule>(rows.Count);
        foreach (var row in rows)
        {
            list.Add(row with { GroupName = await translate(row.GroupName).ConfigureAwait(false) });
        }

        return list;
    }

    private static async Task<List<CpPmCustomer>> CustomersAsync(DbConnection c, CancellationToken ct)
    {
        var list = new List<CpPmCustomer>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT `user_id`, MAX(CASE WHEN `data_key`='email' THEN `data_value` ELSE '' END), "
            + "MAX(CASE WHEN `data_key`='name' THEN `data_value` ELSE '' END), MAX(CASE WHEN `data_key`='surname' THEN `data_value` ELSE '' END) "
            + "FROM `users_profiles` GROUP BY `user_id` ORDER BY `user_id` DESC LIMIT " + CustomersLimit.ToString(CultureInfo.InvariantCulture);
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            list.Add(new CpPmCustomer(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                r.IsDBNull(1) ? "" : r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2), r.IsDBNull(3) ? "" : r.GetString(3)));
        }

        return list;
    }

    private static async Task<List<string>> BrandsAsync(DbConnection c, CancellationToken ct)
    {
        var set = new SortedSet<string>(StringComparer.Ordinal);
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT `brand` FROM ("
                + "SELECT `name` AS `brand` FROM `shop_docpart_manufacturers` WHERE `name` IS NOT NULL AND `name` != '' "
                + "UNION SELECT `manufacturer` AS `brand` FROM `shop_docpart_prices_data` WHERE `manufacturer` IS NOT NULL AND `manufacturer` != '' "
                + "UNION SELECT `manufacturer` AS `brand` FROM `epc_price_profile_brand_rules` WHERE `manufacturer` IS NOT NULL AND `manufacturer` != ''"
                + ") AS `brands` ORDER BY `brand` ASC LIMIT " + BrandsLimit.ToString(CultureInfo.InvariantCulture);
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                var b = EpcPricing.NormalizeBrand(r.IsDBNull(0) ? null : r.GetString(0));
                if (b.Length > 0)
                {
                    set.Add(b);
                }
            }
        }
        catch (DbException)
        {
        }

        return set.ToList();
    }

    private static async Task<string> SettingAsync(DbConnection c, string key, string fallback, CancellationToken ct)
    {
        try
        {
            var v = await ErpDb.StringAsync(c, null, ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"), ct, key).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
        }
        catch (DbException)
        {
            return fallback;
        }
    }

    private static async Task<List<CpPmDemoScenario>> DemoAsync(DbConnection c, IReadOnlyList<CpPmProfile> profiles, CancellationToken ct)
    {
        var guestGroupId = 0;
        try
        {
            guestGroupId = (int)await ErpDb.LongAsync(c, null, "SELECT IFNULL((SELECT `id` FROM `groups` WHERE `for_guests` = 1 ORDER BY `id` ASC LIMIT 1), 0)", ct).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        int ByCode(string code) => profiles.FirstOrDefault(p => p.Code == code)?.GroupId ?? 0;
        var scenarios = new (string Title, int GroupId, string Brand, string Article)[]
        {
            ("Guest visitor (not logged in)", guestGroupId, "TOYOTA", ""),
            ("Retail profile — generic brand (TOYOTA)", ByCode("retail"), "TOYOTA", ""),
            ("Retail profile — MAZDA brand rule", ByCode("retail"), "MAZDA", ""),
            ("Wholesale profile — MAZDA brand rule", ByCode("wholesale"), "MAZDA", ""),
            ("Retail profile — article-level rule (if configured)", ByCode("retail"), "TOYOTA", "1140051020"),
        };

        var list = new List<CpPmDemoScenario>();
        foreach (var s in scenarios)
        {
            if (s.GroupId <= 0)
            {
                continue;
            }

            var r = await EpcPricing.ApplyPriceRulesWithBreakdownAsync(c, s.GroupId, s.Brand, 100m, 0m, s.Article, 0, ct).ConfigureAwait(false);
            var profile = profiles.FirstOrDefault(p => p.GroupId == s.GroupId);
            var label = profile is null ? "Group " + s.GroupId.ToString(CultureInfo.InvariantCulture) : profile.Name + " (" + profile.Code + ")";
            if (s.GroupId == guestGroupId)
            {
                label = "Guest / non-login group";
            }

            list.Add(new CpPmDemoScenario(s.Title, label, s.Brand, s.Article.Length > 0 ? s.Article : "—", r.Visible,
                r.Visible ? Math.Round(r.FinalPrice, 2, MidpointRounding.AwayFromZero) : null,
                r.Visible ? r.TotalMarginPercent : null,
                r.Visible ? FormatBreakdown(r.Steps) : (r.HiddenReason.Length > 0 ? r.HiddenReason : "Hidden")));
        }

        return list;
    }
}
