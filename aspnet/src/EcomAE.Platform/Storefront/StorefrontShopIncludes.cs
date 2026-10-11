namespace EcomAE.Platform.Storefront;

/// <summary>
/// Remaining customer catalogue includes: the category tree dump, product block,
/// description/spec slices, search-result and search-tab shells, and tree-list dumps.
/// The live listing and product page stay on own-catalog and <c>StorefrontProductPage</c>.
/// </summary>
public static class StorefrontShopIncludes
{
    public const string HelperPath = "content/shop/catalogue/helper.php";
    public const string CatalogueTreePath = "content/shop/catalogue/get_catalogue_tree.php";
    public const string CatalogueForCustomerPath = "content/shop/catalogue/catalogue_for_customer.php";
    public const string CatalogueSeparatePagePath = "content/shop/catalogue/catalogue_separate_page.php";
    public const string MainPagePath = "content/shop/catalogue/main_page.php";
    public const string SearchResultPath = "content/shop/catalogue/search_result.php";
    public const string ProductDescriptionPath = "content/shop/catalogue/product_description.php";
    public const string ProductSpecificationsPath = "content/shop/catalogue/product_specifications.php";
    public const string ProductWarehouseSupplierPath = "content/shop/catalogue/product_page_warehouse_supplier.php";
    public const string SpecialSuggestionsPath = "content/shop/catalogue/special_suggestions.php";
    public const string PrintSpecialSearchesPath = "content/shop/catalogue/printSpecialSearches.php";
    public const string SpecialSearchesHandlerPath = "content/shop/catalogue/special_searches_handler.php";
    public const string CatLangGeneralPath = "content/shop/catalogue/cat_lang_general.php";
    public const string DpProductPath = "content/shop/catalogue/dp_product.php";
    public const string DpCategoryRecordPath = "content/shop/catalogue/dp_category_record.php";
    public const string QueryProductsAllPath = "content/shop/catalogue/query_builder/query_products_all.php";
    public const string QueryProductsShowPath = "content/shop/catalogue/query_builder/query_products_show.php";
    public const string TreeListItemsPath = "content/shop/catalogue/tree_lists/get_tree_list_items.php";
    public const string TreeListItemRecordPath = "content/shop/catalogue/tree_lists/dp_tree_list_item.php";
    public const string TreeListHelperPath = "content/shop/catalogue/tree_lists/helper.php";
    public const string SearchTabsPath = "content/shop/catalogue/search_tabs/search_tabs.php";
    public const string PartsCataloguesTabPath = "content/shop/catalogue/search_tabs/tabs_content/parts_catalogues/tab_content.php";
    public const string AftermarketTabPath = "content/shop/catalogue/search_tabs/tabs_content/aftermarket/tab_content.php";
    public const string ToCatalogueTabPath = "content/shop/catalogue/search_tabs/tabs_content/to_catalogue/tab_content.php";
    public const string VinSearchTabPath = "content/shop/catalogue/search_tabs/tabs_content/vin_search/tab_content.php";
    public const string VinSearchNeoriginalTabPath = "content/shop/catalogue/search_tabs/tabs_content/vin_search_neoriginal/tab_content.php";
    public const string ArticleSearchTabPath = "content/shop/catalogue/search_tabs/tabs_content/article_search/tab_content.php";
    public const string NameSearchTabPath = "content/shop/catalogue/search_tabs/tabs_content/name_search/tab_content.php";
    public const string LaximoCatalogTabPath = "content/shop/catalogue/search_tabs/tabs_content/laximo_catalog/tab_content.php";

    public const string printProductBlock = nameof(printProductBlock);
    public const string addCategoryToDump = nameof(addCategoryToDump);
    public const string addProductsToCategory = nameof(addProductsToCategory);
    public const string addItemToDump = nameof(addItemToDump);
    public const string autoxp_redirect = nameof(autoxp_redirect);
    public const string parts_catalogues_tab_show_cars = nameof(parts_catalogues_tab_show_cars);
    public const string parts_catalogues_tab_show_catalogues = nameof(parts_catalogues_tab_show_catalogues);
}
