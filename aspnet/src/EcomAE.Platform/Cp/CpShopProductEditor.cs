namespace EcomAE.Platform.Cp;

/// <summary>
/// Control Panel catalogue product editor from <c>cp/content/shop/catalogue/product.php</c>
/// and the sibling list/tree/review/SKU includes. Multipart image bytes stay Classic.
/// </summary>
public static class CpShopProductEditor
{
    public const string ProductPath = "cp/content/shop/catalogue/product.php";
    public const string ProductsPath = "cp/content/shop/catalogue/products.php";
    public const string AllListProductsPath = "cp/content/shop/catalogue/all_list_products.inc.php";
    public const string CatalogueEditorPath = "cp/content/shop/catalogue/catalogue_editor.php";
    public const string LineListPath = "cp/content/shop/catalogue/line_list.php";
    public const string LineListsManagerPath = "cp/content/shop/catalogue/line_lists_manager.php";
    public const string TreeListPath = "cp/content/shop/catalogue/tree_list.php";
    public const string TreeListsManagerPath = "cp/content/shop/catalogue/tree_lists_manager.php";
    public const string TreeListBrunchEditorPath = "cp/content/shop/catalogue/tree_list_brunch_editor.php";
    public const string TreeListShowAsyncPath = "cp/content/shop/catalogue/tree_list_show_async.php";
    public const string RelatedProductsPath = "cp/content/shop/catalogue/related_products.php";
    public const string ReviewsPath = "cp/content/shop/catalogue/reviews.php";
    public const string SpecialSearchPath = "cp/content/shop/catalogue/special_search.php";
    public const string SpecialSearchesPath = "cp/content/shop/catalogue/special_searches.php";
    public const string MainPageProductsPath = "cp/content/shop/catalogue/main_page_products.php";
    public const string SkuMediaManagerPath = "cp/content/shop/catalogue/epc_sku_media_manager.php";
    public const string SkuMediaConfigPath = "cp/content/shop/catalogue/epc_sku_media_cp_config.php";
    public const string DeleteProductsSubPath = "cp/content/shop/catalogue/delete_products_sub.php";
    public const string CopyPasteCategoriesJsPath = "cp/content/shop/catalogue/copy_paste_categories.js.php";
    public const string CategoriesTemplatesJsPath = "cp/content/shop/catalogue/categories_templates/categories_templates.js.php";
    public const string AppHref = "/cp/product-catalogue-app";
    public const string ProductHref = "/cp/product-catalogue-app#product-save";

    public const string sort_line_list_asc_number = nameof(sort_line_list_asc_number);
    public const string sort_line_list_asc_text = nameof(sort_line_list_asc_text);
    public const string sort_line_list_desc_number = nameof(sort_line_list_desc_number);
    public const string sort_line_list_desc_text = nameof(sort_line_list_desc_text);
    public const string add_new_item_tree_stickers = nameof(add_new_item_tree_stickers);
    public const string applyRelatedProducts = nameof(applyRelatedProducts);
    public const string checkAll = nameof(checkAll);
    public const string deleteImage = nameof(deleteImage);
    public const string deleteRelatedProducts = nameof(deleteRelatedProducts);
    public const string delete_selected_item_tree_stickers = nameof(delete_selected_item_tree_stickers);
    public const string editRelatedProducts = nameof(editRelatedProducts);
    public const string getCheckedProducts = nameof(getCheckedProducts);
    public const string getChildItems = nameof(getChildItems);
    public const string getNewCurrentImageObject = nameof(getNewCurrentImageObject);
    public const string getUpperBrunch = nameof(getUpperBrunch);
    public const string initFieldsByCaption = nameof(initFieldsByCaption);
    public const string init_by_template = nameof(init_by_template);
    public const string onFileChanged = nameof(onFileChanged);
    public const string onSelected = nameof(onSelected);
    public const string onSelected_tree_stickers = nameof(onSelected_tree_stickers);
    public const string onTreeListItemCheck = nameof(onTreeListItemCheck);
    public const string openImageDialog = nameof(openImageDialog);
    public const string openStickerWindow = nameof(openStickerWindow);
    public const string ref_getProductsHTML = nameof(ref_getProductsHTML);
    public const string save_action = nameof(save_action);
    public const string showImages = nameof(showImages);
    public const string sticker_changed_set = nameof(sticker_changed_set);
    public const string sticker_save = nameof(sticker_save);
    public const string unselect_tree = nameof(unselect_tree);
    public const string unselect_tree_related = nameof(unselect_tree_related);
    public const string open_templates_window = nameof(open_templates_window);
    public const string onSelected_templates_tree = nameof(onSelected_templates_tree);
    public const string create_template = nameof(create_template);
    public const string delete_category_template = nameof(delete_category_template);
    public const string template_to_buffer = nameof(template_to_buffer);
}
