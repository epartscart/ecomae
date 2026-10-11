namespace EcomAE.Platform.Storefront;

/// <summary>
/// SKU spec/photo library from PHP <c>content/shop/catalogue/epc_sku_media.php</c>.
/// Public lookup stays on <c>ajax_epc_sku_media_public.php</c>.
/// Photo file bytes stay Classic.
/// </summary>
public static class StorefrontSkuMediaPages
{
    public const string SkuMediaPath = "content/shop/catalogue/epc_sku_media.php";
    public const string SkuMediaStorefrontPath = "content/shop/catalogue/epc_sku_media_storefront.php";
    public const string SkuMediaInstallPath = "content/shop/catalogue/epc_sku_media_cp_install.php";
    public const string SkuMediaPublicAjaxPath = "content/shop/catalogue/ajax_epc_sku_media_public.php";
    public const string SkuMediaAdminAjaxPath = "content/shop/catalogue/ajax_epc_sku_media.php";

    public const string epc_sku_media_add_photo = nameof(epc_sku_media_add_photo);
    public const string epc_sku_media_add_spec_group = nameof(epc_sku_media_add_spec_group);
    public const string epc_sku_media_add_spec_row = nameof(epc_sku_media_add_spec_row);
    public const string epc_sku_media_attach_local_photo = nameof(epc_sku_media_attach_local_photo);
    public const string epc_sku_media_default_spec_types = nameof(epc_sku_media_default_spec_types);
    public const string epc_sku_media_delete_photo = nameof(epc_sku_media_delete_photo);
    public const string epc_sku_media_delete_profile = nameof(epc_sku_media_delete_profile);
    public const string epc_sku_media_delete_spec_group = nameof(epc_sku_media_delete_spec_group);
    public const string epc_sku_media_delete_spec_row = nameof(epc_sku_media_delete_spec_row);
    public const string epc_sku_media_ensure_from_identity = nameof(epc_sku_media_ensure_from_identity);
    public const string epc_sku_media_ensure_schema = nameof(epc_sku_media_ensure_schema);
    public const string epc_sku_media_find_profile = nameof(epc_sku_media_find_profile);
    public const string epc_sku_media_format_value = nameof(epc_sku_media_format_value);
    public const string epc_sku_media_full_payload = nameof(epc_sku_media_full_payload);
    public const string epc_sku_media_images_dir = nameof(epc_sku_media_images_dir);
    public const string epc_sku_media_images_fs = nameof(epc_sku_media_images_fs);
    public const string epc_sku_media_list_profiles = nameof(epc_sku_media_list_profiles);
    public const string epc_sku_media_mirror_to_catalogue = nameof(epc_sku_media_mirror_to_catalogue);
    public const string epc_sku_media_normalize_article = nameof(epc_sku_media_normalize_article);
    public const string epc_sku_media_normalize_brand = nameof(epc_sku_media_normalize_brand);
    public const string epc_sku_media_photo_types = nameof(epc_sku_media_photo_types);
    public const string epc_sku_media_photo_url = nameof(epc_sku_media_photo_url);
    public const string epc_sku_media_photos = nameof(epc_sku_media_photos);
    public const string epc_sku_media_price_storage_map = nameof(epc_sku_media_price_storage_map);
    public const string epc_sku_media_public_lookup = nameof(epc_sku_media_public_lookup);
    public const string epc_sku_media_resolve_for_product = nameof(epc_sku_media_resolve_for_product);
    public const string epc_sku_media_search_library = nameof(epc_sku_media_search_library);
    public const string epc_sku_media_spec_bundle = nameof(epc_sku_media_spec_bundle);
    public const string epc_sku_media_storefront_part_url = nameof(epc_sku_media_storefront_part_url);
    public const string epc_sku_media_update_photo = nameof(epc_sku_media_update_photo);
    public const string epc_sku_media_update_spec_row = nameof(epc_sku_media_update_spec_row);
    public const string epc_sku_media_upsert_profile = nameof(epc_sku_media_upsert_profile);
    public const string epc_sku_media_value_types = nameof(epc_sku_media_value_types);
    public const string epc_sku_media_emit_storefront_css = nameof(epc_sku_media_emit_storefront_css);
    public const string epc_sku_media_render_spec_groups_html = nameof(epc_sku_media_render_spec_groups_html);
    public const string epc_sku_media_render_storefront = nameof(epc_sku_media_render_storefront);
    public const string epc_sku_media_storefront_load = nameof(epc_sku_media_storefront_load);
    public const string epc_sku_media_cp_install = nameof(epc_sku_media_cp_install);
    public const string epc_sku_media_cp_lang = nameof(epc_sku_media_cp_lang);
}
