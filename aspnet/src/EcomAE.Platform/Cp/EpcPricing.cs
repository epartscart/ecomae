using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// PHP <c>content/shop/pricing/epc_pricing.php</c> sell-from-purchase stack:
/// supplier → supplier brand → supplier article → profile → brand → article → guest,
/// then the guest/retail/wholesale safety net.
/// Missing pricing tables are treated as unmatched (PHP catch).
/// </summary>
public static class EpcPricing
{
    public const decimal DefaultGuestRetailMarginPercent = 40m;

    public sealed record SellFromPurchase(bool Visible, decimal Price, decimal Purchase, decimal MarkupDecimal);

    public static (decimal Price, decimal MarkupDecimal) ApplyMarginStep(
        decimal price,
        decimal markupDecimal,
        decimal marginPercent)
    {
        if (marginPercent == 0m)
        {
            return (price, markupDecimal);
        }

        return (price + (price * (marginPercent / 100m)), markupDecimal + (marginPercent / 100m));
    }

    public static string NormalizeBrand(string? brand)
        => (brand ?? string.Empty).Trim().ToUpperInvariant();

    public static string NormalizeArticle(string? article)
    {
        if (string.IsNullOrEmpty(article))
        {
            return "";
        }

        var buffer = new char[article.Length];
        var n = 0;
        foreach (var c in article)
        {
            if (char.IsLetterOrDigit(c))
            {
                buffer[n++] = char.ToUpperInvariant(c);
            }
        }

        return n == 0 ? "" : new string(buffer, 0, n);
    }

    public static async Task<int> ResolveCustomerGroupIdAsync(
        DbConnection connection,
        long userId,
        CancellationToken cancellationToken,
        int fallbackGroupId = 0)
    {
        if (userId > 0)
        {
            var profileGid = await TryLongAsync(
                connection,
                """
                SELECT `users_groups_bind`.`group_id`
                FROM `users_groups_bind`
                INNER JOIN `epc_price_profiles` ON `epc_price_profiles`.`group_id` = `users_groups_bind`.`group_id`
                WHERE `users_groups_bind`.`user_id` = ?
                ORDER BY `users_groups_bind`.`record_id` DESC
                LIMIT 1
                """,
                cancellationToken,
                userId).ConfigureAwait(false);
            if (profileGid > 0)
            {
                return (int)profileGid;
            }
        }

        if (fallbackGroupId > 0)
        {
            return fallbackGroupId;
        }

        if (userId <= 0)
        {
            var guest = await TryLongAsync(
                connection,
                "SELECT `id` FROM `groups` WHERE `for_guests` = 1 ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (guest > 0)
            {
                return (int)guest;
            }
        }

        return 0;
    }

    public static async Task<decimal> ApplySellFromPurchaseAsync(
        DbConnection connection,
        int groupId,
        string? brand,
        decimal purchase,
        string? article,
        int storageId,
        CancellationToken cancellationToken)
    {
        var result = await ApplyPriceRulesAsync(
            connection, groupId, brand, purchase, 0m, article, storageId, cancellationToken).ConfigureAwait(false);
        var price = result.Price;
        var markup = result.MarkupDecimal;
        if (purchase > 0 && price <= purchase)
        {
            var code = await ProfileCodeAsync(connection, groupId, cancellationToken).ConfigureAwait(false);
            decimal floor;
            if (await IsGuestGroupAsync(connection, groupId, cancellationToken).ConfigureAwait(false))
            {
                floor = await GuestMarginPercentAsync(connection, cancellationToken).ConfigureAwait(false);
            }
            else if (code == "wholesale")
            {
                floor = await ProfileMarginPercentAsync(connection, groupId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                floor = await ProfileMarginPercentAsync(connection, groupId, cancellationToken).ConfigureAwait(false);
                if (floor <= 0m)
                {
                    floor = DefaultGuestRetailMarginPercent;
                }
            }

            if (floor > 0)
            {
                (price, markup) = ApplyMarginStep(purchase, 0m, floor);
            }
        }

        _ = markup;
        return Math.Round(price, 2, MidpointRounding.AwayFromZero);
    }

    public static async Task<SellFromPurchase> ApplyPriceRulesAsync(
        DbConnection connection,
        int groupId,
        string? brand,
        decimal price,
        decimal markupDecimal,
        string? article,
        int storageId,
        CancellationToken cancellationToken)
    {
        var brandNorm = NormalizeBrand(brand);
        var articleNorm = NormalizeArticle(article);
        var storage = await StorageRuleAsync(connection, storageId, cancellationToken).ConfigureAwait(false);
        if (storage.Matched && storage.Visible == 0)
        {
            return new SellFromPurchase(false, price, price, markupDecimal);
        }

        var storageBrand = await StorageBrandRuleAsync(connection, storageId, brandNorm, cancellationToken).ConfigureAwait(false);
        if (storageBrand.Matched && storageBrand.Visible == 0)
        {
            return new SellFromPurchase(false, price, price, markupDecimal);
        }

        var storageArticle = await StorageArticleRuleAsync(connection, storageId, brandNorm, articleNorm, cancellationToken).ConfigureAwait(false);
        if (storageArticle.Matched && storageArticle.Visible == 0)
        {
            return new SellFromPurchase(false, price, price, markupDecimal);
        }

        var brandRule = await BrandRuleAsync(connection, groupId, brandNorm, cancellationToken).ConfigureAwait(false);
        if (brandRule.Visible == 0)
        {
            return new SellFromPurchase(false, price, price, markupDecimal);
        }

        var articleRule = await ArticleRuleAsync(connection, groupId, brandNorm, articleNorm, cancellationToken).ConfigureAwait(false);
        if (articleRule.Matched && articleRule.Visible == 0)
        {
            return new SellFromPurchase(false, price, price, markupDecimal);
        }

        if (storage.Matched && storage.MarginPercent != 0m)
        {
            (price, markupDecimal) = ApplyMarginStep(price, markupDecimal, storage.MarginPercent);
        }

        if (storageBrand.Matched && storageBrand.MarginPercent != 0m)
        {
            (price, markupDecimal) = ApplyMarginStep(price, markupDecimal, storageBrand.MarginPercent);
        }

        if (storageArticle.Matched && storageArticle.MarginPercent != 0m)
        {
            (price, markupDecimal) = ApplyMarginStep(price, markupDecimal, storageArticle.MarginPercent);
        }

        var profileMargin = await ProfileMarginPercentAsync(connection, groupId, cancellationToken).ConfigureAwait(false);
        if (profileMargin != 0m)
        {
            (price, markupDecimal) = ApplyMarginStep(price, markupDecimal, profileMargin);
        }

        if (brandRule.MarginPercent != 0m)
        {
            (price, markupDecimal) = ApplyMarginStep(price, markupDecimal, brandRule.MarginPercent);
        }

        if (articleRule.Matched && articleRule.MarginPercent != 0m)
        {
            (price, markupDecimal) = ApplyMarginStep(price, markupDecimal, articleRule.MarginPercent);
        }

        if (await IsGuestGroupAsync(connection, groupId, cancellationToken).ConfigureAwait(false))
        {
            var guest = await GuestMarginPercentAsync(connection, cancellationToken).ConfigureAwait(false);
            if (guest != 0m)
            {
                (price, markupDecimal) = ApplyMarginStep(price, markupDecimal, guest);
            }
        }

        return new SellFromPurchase(true, price, price, markupDecimal);
    }

    private sealed record Rule(int Visible, decimal MarginPercent, bool Matched);

    private static Rule Unmatched => new(1, 0m, false);

    private static async Task<Rule> StorageRuleAsync(
        DbConnection connection,
        int storageId,
        CancellationToken cancellationToken)
    {
        if (storageId <= 0)
        {
            return Unmatched;
        }

        return await TryRuleAsync(
            connection,
            "SELECT `visible`, `margin_percent` FROM `epc_price_storage_rules` WHERE `storage_id` = ? LIMIT 1",
            cancellationToken,
            storageId).ConfigureAwait(false);
    }

    private static async Task<Rule> StorageBrandRuleAsync(
        DbConnection connection,
        int storageId,
        string brand,
        CancellationToken cancellationToken)
    {
        if (storageId <= 0 || brand.Length == 0)
        {
            return Unmatched;
        }

        return await TryRuleAsync(
            connection,
            "SELECT `visible`, `margin_percent` FROM `epc_price_storage_brand_rules` WHERE `storage_id` = ? AND `manufacturer` = ? LIMIT 1",
            cancellationToken,
            storageId, brand).ConfigureAwait(false);
    }

    private static async Task<Rule> StorageArticleRuleAsync(
        DbConnection connection,
        int storageId,
        string brand,
        string article,
        CancellationToken cancellationToken)
    {
        if (storageId <= 0 || brand.Length == 0 || article.Length == 0)
        {
            return Unmatched;
        }

        return await TryRuleAsync(
            connection,
            "SELECT `visible`, `margin_percent` FROM `epc_price_storage_article_rules` WHERE `storage_id` = ? AND `manufacturer` = ? AND `article` = ? LIMIT 1",
            cancellationToken,
            storageId, brand, article).ConfigureAwait(false);
    }

    private static async Task<Rule> BrandRuleAsync(
        DbConnection connection,
        int groupId,
        string brand,
        CancellationToken cancellationToken)
    {
        if (groupId <= 0 || brand.Length == 0)
        {
            return new Rule(1, 0m, false);
        }

        var rule = await TryRuleAsync(
            connection,
            "SELECT `visible`, `margin_percent` FROM `epc_price_profile_brand_rules` WHERE `group_id` = ? AND `manufacturer` = ? LIMIT 1",
            cancellationToken,
            groupId, brand).ConfigureAwait(false);
        return rule.Matched ? rule : new Rule(1, 0m, false);
    }

    private static async Task<Rule> ArticleRuleAsync(
        DbConnection connection,
        int groupId,
        string brand,
        string article,
        CancellationToken cancellationToken)
    {
        if (article.Length == 0 || groupId <= 0 || brand.Length == 0)
        {
            return Unmatched;
        }

        return await TryRuleAsync(
            connection,
            "SELECT `visible`, `margin_percent` FROM `epc_price_profile_article_rules` WHERE `group_id` = ? AND `manufacturer` = ? AND `article` = ? LIMIT 1",
            cancellationToken,
            groupId, brand, article).ConfigureAwait(false);
    }

    private static async Task<string> ProfileCodeAsync(
        DbConnection connection,
        int groupId,
        CancellationToken cancellationToken)
    {
        if (groupId <= 0)
        {
            return "";
        }

        var code = await TryStringAsync(
            connection,
            "SELECT `code` FROM `epc_price_profiles` WHERE `group_id` = ? LIMIT 1",
            cancellationToken,
            groupId).ConfigureAwait(false);
        return (code ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static async Task<decimal> ProfileMarginPercentAsync(
        DbConnection connection,
        int groupId,
        CancellationToken cancellationToken)
    {
        if (groupId <= 0)
        {
            return 0m;
        }

        var value = await TryDecimalAsync(
            connection,
            "SELECT `margin_percent` FROM `epc_price_profiles` WHERE `group_id` = ? LIMIT 1",
            cancellationToken,
            groupId).ConfigureAwait(false);
        if (value <= 0m && await ProfileCodeAsync(connection, groupId, cancellationToken).ConfigureAwait(false) == "retail")
        {
            return DefaultGuestRetailMarginPercent;
        }

        return value;
    }

    private static async Task<bool> IsGuestGroupAsync(
        DbConnection connection,
        int groupId,
        CancellationToken cancellationToken)
    {
        if (groupId <= 0)
        {
            return false;
        }

        var flag = await TryLongAsync(
            connection,
            "SELECT `for_guests` FROM `groups` WHERE `id` = ? LIMIT 1",
            cancellationToken,
            groupId).ConfigureAwait(false);
        return flag == 1;
    }

    private static async Task<decimal> GuestMarginPercentAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var raw = await TryStringAsync(
            connection,
            "SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1",
            cancellationToken,
            "guest_margin_percent").ConfigureAwait(false);
        if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value <= 0m)
        {
            return DefaultGuestRetailMarginPercent;
        }

        return value;
    }

    private static async Task<Rule> TryRuleAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params object?[] args)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(sql);
            ErpDb.AddParameters(cmd, args);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Unmatched;
            }

            var visible = reader["visible"] is DBNull ? 1 : Convert.ToInt32(reader["visible"], CultureInfo.InvariantCulture);
            var margin = reader["margin_percent"] is DBNull
                ? 0m
                : Convert.ToDecimal(reader["margin_percent"], CultureInfo.InvariantCulture);
            return new Rule(visible, margin, true);
        }
        catch (DbException)
        {
            return Unmatched;
        }
    }

    private static async Task<long> TryLongAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params object?[] args)
    {
        try
        {
            return await ErpDb.LongAsync(connection, null, ErpDb.Positional(sql), cancellationToken, args).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private static async Task<decimal> TryDecimalAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params object?[] args)
    {
        try
        {
            return await ErpDb.DecimalAsync(connection, null, ErpDb.Positional(sql), cancellationToken, args).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0m;
        }
    }

    private static async Task<string?> TryStringAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params object?[] args)
    {
        try
        {
            return await ErpDb.StringAsync(connection, null, ErpDb.Positional(sql), cancellationToken, args).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }
    }
}
