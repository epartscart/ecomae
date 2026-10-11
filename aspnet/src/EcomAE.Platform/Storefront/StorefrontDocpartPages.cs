namespace EcomAE.Platform.Storefront;

/// <summary>
/// Customer part-search / garage / demand / ingest includes from
/// <c>content/shop/docpart</c>. Live search stays on
/// <c>/storefront/search-app</c>; garage and demand ajax stay on their mapped
/// endpoints. Supplier-price ingest reuses the existing price importer.
/// </summary>
public static class StorefrontDocpartPages
{
    public const string PartSearchPagePath = "content/shop/docpart/part_search_page.php";
    public const string PartSearchPage1Path = "content/shop/docpart/part_search_page_1.php";
    public const string PartSearchPage2Path = "content/shop/docpart/part_search_page_2.php";
    public const string PartSearchManufacturerBrowsePath = "content/shop/docpart/part_search_manufacturer_browse.php";
    public const string PartSearchManufacturersIndexPath = "content/shop/docpart/part_search_manufacturers_index.php";
    public const string SearchStrsForInputsPath = "content/shop/docpart/search_strs_for_inputs.php";
    public const string DocpartManufacturerPath = "content/shop/docpart/DocpartManufacturer.php";
    public const string DocpartProductPath = "content/shop/docpart/DocpartProduct.php";
    public const string DocpartSuppliersApiDebugPath = "content/shop/docpart/DocpartSuppliersAPI_Debug.php";
    public const string ChoseCarPath = "content/shop/docpart/car_catalogues/chose_car.php";
    public const string CheckApiDirectQueryPath = "content/shop/docpart/check_api_direct_query.php";
    public const string ArticleMatchPath = "content/shop/docpart/docpart_article_match.php";
    public const string CrossInterchangePath = "content/shop/docpart/docpart_cross_interchange.php";
    public const string ArticleBrandsPath = "content/shop/docpart/docpart_epc_article_brands.php";
    public const string GenuineManufacturersPath = "content/shop/docpart/docpart_genuine_manufacturers.php";
    public const string ManufacturerSynonymsPath = "content/shop/docpart/docpart_manufacturer_synonyms.php";
    public const string ProductHashPath = "content/shop/docpart/docpart_product_hash.php";
    public const string AccessoriesCatalogPath = "content/shop/docpart/epc_accessories_catalog.php";
    public const string AccessoriesDbPath = "content/shop/docpart/epc_accessories_db.php";
    public const string AgentCatalogKnowledgePath = "content/shop/docpart/epc_agent_catalog_knowledge.php";
    public const string AiPartsExpertPath = "content/shop/docpart/epc_ai_parts_expert.php";
    public const string BuildInitialPriceBunchPath = "content/shop/docpart/epc_build_initial_price_bunch.php";
    public const string CataBridgePath = "content/shop/docpart/epc_cata_bridge.php";
    public const string CommercePriceIngestPath = "content/shop/docpart/epc_commerce_price_ingest.php";
    public const string ComplementaryPartsPath = "content/shop/docpart/epc_complementary_parts.php";
    public const string CrossbaseCachePath = "content/shop/docpart/epc_crossbase_cache.php";
    public const string DemandCountryIsoPath = "content/shop/docpart/epc_demand_country_iso.php";
    public const string DemandIntelligencePath = "content/shop/docpart/epc_demand_intelligence.php";
    public const string FitmentWidgetPath = "content/shop/docpart/epc_fitment_widget.php";
    public const string MultivendorMinPriceAclPath = "content/shop/docpart/epc_multivendor_min_price_acl.php";
    public const string MultivendorPriceIngestPath = "content/shop/docpart/epc_multivendor_price_ingest.php";
    public const string PartsAgentPath = "content/shop/docpart/epc_parts_agent.php";
    public const string PriceExtraFieldsPath = "content/shop/docpart/epc_price_extra_fields.php";
    public const string PriceUploadDiagnosticsPath = "content/shop/docpart/epc_price_upload_diagnostics.php";
    public const string PriceUploadGuideDataPath = "content/shop/docpart/epc_price_upload_guide_data.php";
    public const string PricesOfficeStorageMetaPath = "content/shop/docpart/epc_prices_office_storage_meta.php";
    public const string ProductFamilyPath = "content/shop/docpart/epc_product_family.php";
    public const string StockBrandsHelpersPath = "content/shop/docpart/epc_stock_brands_helpers.php";
    public const string StorefrontAntiCrawlPath = "content/shop/docpart/epc_storefront_anti_crawl.php";
    public const string StorefrontStorageFlagsPath = "content/shop/docpart/epc_storefront_storage_flags.php";
    public const string WarehouseAttrSearchPagePath = "content/shop/docpart/epc_warehouse_attr_search_page.php";
    public const string GarageCarPath = "content/shop/docpart/garage/car.php";
    public const string GaragePath = "content/shop/docpart/garage/garage.php";
}
