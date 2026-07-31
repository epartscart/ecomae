namespace EcomAE.Platform.Api.Catalog;

public static class LegacyPriceLookupSql
{
    public const string SourceTable = "shop_docpart_prices_data";

    public const int DefaultLimit = 25;

    public const string LookupOffers = """
        SELECT `manufacturer`, COALESCE(NULLIF(`article_show`, ''), `article`) AS `article`,
               `name`, `price`, `exist`, `storage`, `time_to_exe`
        FROM `shop_docpart_prices_data`
        WHERE UPPER(TRIM(`manufacturer`)) = @brand
          AND (UPPER(REPLACE(`article`, ' ', '')) = @article OR UPPER(REPLACE(COALESCE(`article_show`, `article`), ' ', '')) = @article)
          AND IFNULL(`price`, 0) > 0
        ORDER BY `price` ASC
        LIMIT 25
        """;
}
