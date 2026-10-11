namespace EcomAE.Platform.Cp;

/// <summary>
/// Control Panel price-upload page bodies. Import writes reuse <c>ICpPriceImportService</c>
/// (<c>upload_file.php</c>). Excel/CSV conversion ajax stay on their mapped endpoints.
/// </summary>
public static class CpPricesUploadPages
{
    public const string UploadFilePath = "cp/content/shop/prices_upload/upload_file.php";
    public const string PriceReviewPath = "cp/content/shop/prices_upload/price_review/price_review.php";
    public const string PriceDownloadManagerPath = "cp/content/shop/prices_upload/price_download_manager.php";
    public const string PricesManagerPath = "cp/content/shop/prices_upload/prices_manager.php";
    public const string PricePath = "cp/content/shop/prices_upload/price.php";
    public const string PricesUploadGuidePath = "cp/content/shop/prices_upload/prices_upload_guide.php";
    public const string PricesGuidePagePath = "cp/content/shop/prices_upload/prices_guide_page.php";
    public const string GuidePath = "cp/content/shop/prices_upload/guide.php";
    public const string MultivendorUploadPagePath = "cp/content/shop/prices_upload/multivendor_upload_page.php";
    public const string MultivendorDataUploadPath = "cp/content/shop/prices_upload/multivendor_data_upload.php";
    public const string CommerceDataUploadPath = "cp/content/shop/prices_upload/commerce_data_upload.php";
    public const string CommerceDataPagePath = "cp/content/shop/prices_upload/commerce_data_page.php";
    public const string HistoryConfigPath = "cp/content/shop/prices_upload/epc_prices_upload_history_config.php";
    public const string StorefrontStoragePanelPath = "cp/content/shop/prices_upload/epc_storefront_storage_panel.php";
    public const string AppHref = "/cp/prices-upload-app";
    public const string UploadHref = "/cp/shop/prices/upload";
    public const string ReviewHref = "/cp/shop/prices/review";
    public const string HistoryHref = "/cp/prices-upload-app#epc-upload-history";
    public const string MultivendorHref = "/cp/shop/prices/multivendor";
    public const string DownloadManagerHref = "/cp/shop/prices";

    public const string clear_dir = nameof(clear_dir);
    public const string downloadFile = nameof(downloadFile);
    public const string epc_commerce_render_source_row = nameof(epc_commerce_render_source_row);
    public const string epcGuideRunHealth = nameof(epcGuideRunHealth);
    public const string epcPdlPost = nameof(epcPdlPost);
    public const string create_prices = nameof(create_prices);
    public const string epcPdlLinkStorages = nameof(epcPdlLinkStorages);
    public const string epcPdlCheckMap = nameof(epcPdlCheckMap);
    public const string on_check_uncheck_all = nameof(on_check_uncheck_all);
    public const string on_one_check_changed = nameof(on_one_check_changed);
    public const string getCheckedElements = nameof(getCheckedElements);
    public const string complete_session = nameof(complete_session);
    public const string csv_prepare = nameof(csv_prepare);
    public const string excel_convert = nameof(excel_convert);
    public const string import_csv_to_db = nameof(import_csv_to_db);
    public const string submitForm = nameof(submitForm);
    public const string table_enable_keys = nameof(table_enable_keys);
    public const string cancel_process = nameof(cancel_process);
    public const string download_price = nameof(download_price);
    public const string review_price = nameof(review_price);
    public const string send_request_for_one_part = nameof(send_request_for_one_part);
}
