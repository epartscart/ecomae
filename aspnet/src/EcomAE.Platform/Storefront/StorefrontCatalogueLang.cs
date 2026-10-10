namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/catalogue/cat_lang_general.php</c>: the manufacturer and article property-name
/// subqueries used by the catalogue text-search algorithm. Verified against PHP 8.3 by
/// <c>Fixtures/StorefrontFragments/golden.json</c>.
/// </summary>
public static class StorefrontCatalogueLang
{
    public const string Manufacturer =
        "(SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN (\"Производитель\", \"Manufacturer\") )";

    public const string Article =
        "(SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN (\"Артикул\", \"Article\") )";
}
