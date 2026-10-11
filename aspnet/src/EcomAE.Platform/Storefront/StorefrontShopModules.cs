namespace EcomAE.Platform.Storefront;

/// <summary>
/// Storefront PHP modules: catalogue mega-menu, required menu, login-code app,
/// breadcrumbs, geo, and the ucats category modules. Ucats product HTTP stays
/// on the existing No-access gate unless <c>_ASTEXE_</c> is defined.
/// </summary>
public static class StorefrontShopModules
{
    public const string DpMenuPath = "modules/shop/catalogue/dp_menu.php";
    public const string MenuRequiredPath = "modules/menu/menu_required.php";
    public const string LoginCodeAppPath = "modules/login/code/app.php";
    public const string BreadCrumbsPath = "modules/bread_crumbs/module.php";
    public const string GeoPointPath = "modules/shop/geo/point_geo_node.php";
    public const string UcatsAvtoximiyaPath = "modules/shop/ucats/avtoximiya/module.php";
    public const string UcatsAccessoriesPath = "modules/shop/ucats/accessories/module.php";
    public const string UcatsShinyPath = "modules/shop/ucats/shiny/module.php";
    public const string UcatsBoltyPath = "modules/shop/ucats/bolty_gayki_prostavki/module.php";
    public const string UcatsDiskyPath = "modules/shop/ucats/disky/module.php";
    public const string UcatsKolpakiPath = "modules/shop/ucats/kolpaki/module.php";
    public const string UcatsAkbPath = "modules/shop/ucats/akb/module.php";
    public const string BreadCrumbsHelperPath = "modules/bread_crumbs/helper.php";
    public const string LangModulePath = "modules/lang/module.php";
    public const string LoginModulePath = "modules/login/module.php";
    public const string LoginSocialAppPath = "modules/login/epc_social/app.php";
    public const string MenuPath = "modules/menu/menu.php";
    public const string NewsModulePath = "modules/news/module.php";
    public const string BalanceModulePath = "modules/shop/balance/module.php";
    public const string CartModulePath = "modules/shop/cart/cart.php";
    public const string PrintCatalogueNodePath = "modules/shop/catalogue/printCatalogueNode.php";
    public const string SideMenuPath = "modules/shop/catalogue/side_menu.php";
    public const string TopMenuCatalogPath = "modules/shop/catalogue/top_menu_catalog.php";
    public const string SearchStringPath = "modules/shop/search_string/search_string.php";
    public const string SliderPath = "modules/slider/slider.php";

    public const string getHtmlLink = nameof(getHtmlLink);
    public const string getHtml_tabs_content = nameof(getHtml_tabs_content);
    public const string getHtml_tabs_header = nameof(getHtml_tabs_header);
    public const string get_cnt_links = nameof(get_cnt_links);
    public const string showCatalogMenu = nameof(showCatalogMenu);
    public const string epc_menu_filter_storefront_hidden = nameof(epc_menu_filter_storefront_hidden);
    public const string epc_menu_item_path_key = nameof(epc_menu_item_path_key);
    public const string getHtmlOfMenu = nameof(getHtmlOfMenu);
    public const string get_alternative_bread_crumbs = nameof(get_alternative_bread_crumbs);
    public const string printCatalogueNode = nameof(printCatalogueNode);
    public const string getHtmlOfTopMenuCatalogue = nameof(getHtmlOfTopMenuCatalogue);
    public const string submit_form = nameof(submit_form);
}
