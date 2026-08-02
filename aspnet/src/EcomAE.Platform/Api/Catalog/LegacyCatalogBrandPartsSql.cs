namespace EcomAE.Platform.Api.Catalog;

public static class LegacyCatalogBrandPartsSql
{
    public const string SourceTable = "shop_docpart_prices_data";

    public const string CountDistinctArticles = """
        SELECT COUNT(*) FROM (
            SELECT COALESCE(NULLIF(`article_show`, ''), `article`) AS `article_key`
            FROM `shop_docpart_prices_data`
            WHERE TRIM(IFNULL(`manufacturer`, '')) != ''
              AND TRIM(IFNULL(`article`, '')) != ''
              AND IFNULL(`price`, 0) > 0
              AND IFNULL(`exist`, 0) > 0
              AND (UPPER(TRIM(`manufacturer`)) = @brand
                   OR REPLACE(REPLACE(REPLACE(UPPER(TRIM(`manufacturer`)), ' ', ''), '-', ''), '.', '') = @brandCompact)
            GROUP BY UPPER(TRIM(`manufacturer`)), COALESCE(NULLIF(`article_show`, ''), `article`)
        ) AS brand_items
        """;

    public const string SelectPage = """
        SELECT
            UPPER(TRIM(`manufacturer`)) AS `manufacturer`,
            COALESCE(NULLIF(`article_show`, ''), `article`) AS `article_show`,
            MIN(`article`) AS `article`,
            MIN(`name`) AS `name`,
            SUM(IFNULL(`exist`, 0)) AS `exist`,
            MIN(`price`) AS `price`,
            MIN(`time_to_exe`) AS `time_to_exe`,
            MIN(`storage`) AS `storage`
        FROM `shop_docpart_prices_data`
        WHERE TRIM(IFNULL(`manufacturer`, '')) != ''
          AND TRIM(IFNULL(`article`, '')) != ''
          AND IFNULL(`price`, 0) > 0
          AND IFNULL(`exist`, 0) > 0
          AND (UPPER(TRIM(`manufacturer`)) = @brand
               OR REPLACE(REPLACE(REPLACE(UPPER(TRIM(`manufacturer`)), ' ', ''), '-', ''), '.', '') = @brandCompact)
        GROUP BY UPPER(TRIM(`manufacturer`)), COALESCE(NULLIF(`article_show`, ''), `article`)
        ORDER BY `article_show` ASC
        LIMIT @limit OFFSET @offset
        """;
}
