# Unmentioned PHP functions — named for build

The inventory count **functions_unmentioned** is not a set of anonymous closures. Each function already has a PHP name. It is counted here when `aspnet/src` does not contain that identifier. `csharp_name` is the PascalCase twin to implement.

| Class | Count | What to do |
|---|---:|---|
| Real PHP (non-ERP), gap file ≤200 lines | 162 | Build next — listed below |
| Real PHP (non-ERP), larger gap file | 2587 | Build with the parent kernel |
| Real PHP already on a mentioned file | 859 | Finish leftover helpers on that twin |
| JS functions written inside PHP templates | 1390 | Port as browser JS, not C# methods |
| Vendor (PHPExcel, PclZip, …) | 255 | Do not port |
| ERP finance (Devin) | 2035 | Leave for Devin |
| **Total unmentioned** | **7726** | |

## Ready to build (non-ERP PHP, gap file ≤200 lines)

| PHP name | C# twin | File | Lines |
|---|---|---|---:|
| `epc_copilot_ensure_schema` | `EpcCopilotEnsureSchema` | `content/general_pages/epc_ai_copilot.php` | 150 |
| `epc_copilot_execute` | `EpcCopilotExecute` | `content/general_pages/epc_ai_copilot.php` | 150 |
| `epc_copilot_fleet_stats` | `EpcCopilotFleetStats` | `content/general_pages/epc_ai_copilot.php` | 150 |
| `epc_copilot_generate_sql` | `EpcCopilotGenerateSql` | `content/general_pages/epc_ai_copilot.php` | 150 |
| `epc_copilot_history` | `EpcCopilotHistory` | `content/general_pages/epc_ai_copilot.php` | 150 |
| `epc_copilot_intents` | `EpcCopilotIntents` | `content/general_pages/epc_ai_copilot.php` | 150 |
| `epc_copilot_parse_intent` | `EpcCopilotParseIntent` | `content/general_pages/epc_ai_copilot.php` | 150 |
| `epc_bos_ajax_login_secure` | `EpcBosAjaxLoginSecure` | `content/general_pages/epc_bos_ajax_login.php` | 157 |
| `epc_bos_health_check_all` | `EpcBosHealthCheckAll` | `content/general_pages/epc_bos_health_check.php` | 157 |
| `epc_bos_health_check_tenant` | `EpcBosHealthCheckTenant` | `content/general_pages/epc_bos_health_check.php` | 157 |
| `epc_bos_health_summary` | `EpcBosHealthSummary` | `content/general_pages/epc_bos_health_check.php` | 157 |
| `epc_bos_ajax_action_name` | `EpcBosAjaxActionName` | `content/general_pages/epc_bos_security.php` | 138 |
| `epc_bos_ajax_entry_guard` | `EpcBosAjaxEntryGuard` | `content/general_pages/epc_bos_security.php` | 138 |
| `epc_bos_csrf_meta` | `EpcBosCsrfMeta` | `content/general_pages/epc_bos_security.php` | 138 |
| `epc_bos_provider_only_actions` | `EpcBosProviderOnlyActions` | `content/general_pages/epc_bos_security.php` | 138 |
| `epc_bos_public_actions` | `EpcBosPublicActions` | `content/general_pages/epc_bos_security.php` | 138 |
| `epc_cp_breadcrumb_caption_for_node` | `EpcCpBreadcrumbCaptionForNode` | `content/general_pages/epc_cp_breadcrumb.php` | 159 |
| `epc_cp_breadcrumb_ensure_folder_content` | `EpcCpBreadcrumbEnsureFolderContent` | `content/general_pages/epc_cp_breadcrumb.php` | 159 |
| `epc_cp_breadcrumb_humanize_segment` | `EpcCpBreadcrumbHumanizeSegment` | `content/general_pages/epc_cp_breadcrumb.php` | 159 |
| `epc_cp_breadcrumb_repair_intermediate_folders` | `EpcCpBreadcrumbRepairIntermediateFolders` | `content/general_pages/epc_cp_breadcrumb.php` | 159 |
| `epc_cp_common_parity_host_map` | `EpcCpCommonParityHostMap` | `content/general_pages/epc_cp_common_parity.php` | 151 |
| `epc_cp_common_parity_pack_applies` | `EpcCpCommonParityPackApplies` | `content/general_pages/epc_cp_common_parity.php` | 151 |
| `epc_cp_common_parity_packs` | `EpcCpCommonParityPacks` | `content/general_pages/epc_cp_common_parity.php` | 151 |
| `epc_cp_common_parity_targets` | `EpcCpCommonParityTargets` | `content/general_pages/epc_cp_common_parity.php` | 151 |
| `epc_ecomae_h` | `EpcEcomaeH` | `content/general_pages/epc_ecomae_platform_home.php` | 118 |
| `epc_el_pl_href` | `EpcElPlHref` | `content/general_pages/epc_electronicae_home_product_lines.php` | 70 |
| `epc_ep_pl_href` | `EpcEpPlHref` | `content/general_pages/epc_epartscart_home_product_lines.php` | 81 |
| `epc_epartscart_apai_category_redirect` | `EpcEpartscartApaiCategoryRedirect` | `content/general_pages/epc_epartscart_storefront.php` | 138 |
| `epc_epartscart_catalog_placeholder_url` | `EpcEpartscartCatalogPlaceholderUrl` | `content/general_pages/epc_epartscart_storefront.php` | 138 |
| `epc_epartscart_filter_menu_tree` | `EpcEpartscartFilterMenuTree` | `content/general_pages/epc_epartscart_storefront.php` | 138 |
| `epc_epartscart_is_apai_alias` | `EpcEpartscartIsApaiAlias` | `content/general_pages/epc_epartscart_storefront.php` | 138 |
| `epc_epartscart_is_apai_url` | `EpcEpartscartIsApaiUrl` | `content/general_pages/epc_epartscart_storefront.php` | 138 |
| `epc_epartscart_lang_href` | `EpcEpartscartLangHref` | `content/general_pages/epc_epartscart_storefront.php` | 138 |
| `epc_epartscart_storefront_active` | `EpcEpartscartStorefrontActive` | `content/general_pages/epc_epartscart_storefront.php` | 138 |
| `epc_epartscart_use_neutral_product_image` | `EpcEpartscartUseNeutralProductImage` | `content/general_pages/epc_epartscart_storefront.php` | 138 |
| `epc_storefront_catalog_placeholder_for_hint` | `EpcStorefrontCatalogPlaceholderForHint` | `content/general_pages/epc_epartscart_storefront.php` | 138 |
| `epc_industry_builtin_packs` | `EpcIndustryBuiltinPacks` | `content/general_pages/epc_industry_packs.php` | 168 |
| `epc_industry_ensure_schema` | `EpcIndustryEnsureSchema` | `content/general_pages/epc_industry_packs.php` | 168 |
| `epc_industry_fleet_stats` | `EpcIndustryFleetStats` | `content/general_pages/epc_industry_packs.php` | 168 |
| `epc_industry_seed_packs` | `EpcIndustrySeedPacks` | `content/general_pages/epc_industry_packs.php` | 168 |
| `epc_industry_tenant_packs` | `EpcIndustryTenantPacks` | `content/general_pages/epc_industry_packs.php` | 168 |
| `epc_cp_menu_cache` | `EpcCpMenuCache` | `content/general_pages/epc_perf_cache.php` | 171 |
| `epc_cp_menu_cache_bust` | `EpcCpMenuCacheBust` | `content/general_pages/epc_perf_cache.php` | 171 |
| `epc_perf_cache_bust_prefix` | `EpcPerfCacheBustPrefix` | `content/general_pages/epc_perf_cache.php` | 171 |
| `epc_perf_cache_delete` | `EpcPerfCacheDelete` | `content/general_pages/epc_perf_cache.php` | 171 |
| `epc_perf_cache_dir` | `EpcPerfCacheDir` | `content/general_pages/epc_perf_cache.php` | 171 |
| `epc_perf_cache_get` | `EpcPerfCacheGet` | `content/general_pages/epc_perf_cache.php` | 171 |
| `epc_perf_cache_key_safe` | `EpcPerfCacheKeySafe` | `content/general_pages/epc_perf_cache.php` | 171 |
| `epc_perf_cache_remember` | `EpcPerfCacheRemember` | `content/general_pages/epc_perf_cache.php` | 171 |
| `epc_perf_cache_set` | `EpcPerfCacheSet` | `content/general_pages/epc_perf_cache.php` | 171 |
| `epc_php_reference_apply_deep_uri` | `EpcPhpReferenceApplyDeepUri` | `content/general_pages/epc_php_reference_router.php` | 144 |
| `epc_php_reference_is_super_cp_host` | `EpcPhpReferenceIsSuperCpHost` | `content/general_pages/epc_php_reference_router.php` | 144 |
| `epc_php_reference_surface` | `EpcPhpReferenceSurface` | `content/general_pages/epc_php_reference_router.php` | 144 |
| `epc_php_reference_try_route` | `EpcPhpReferenceTryRoute` | `content/general_pages/epc_php_reference_router.php` | 144 |
| `epc_cpi_header_href` | `EpcCpiHeaderHref` | `content/general_pages/epc_portal_consulting_primeinvest_header.php` | 113 |
| `epc_er_header_href` | `EpcErHeaderHref` | `content/general_pages/epc_portal_electronics_retail_header.php` | 162 |
| `epc_frn_header_href` | `EpcFrnHeaderHref` | `content/general_pages/epc_portal_fashion_retail_namshi_header.php` | 196 |
| `epc_portal_industry_live_audit` | `EpcPortalIndustryLiveAudit` | `content/general_pages/epc_portal_industry_live_bridge.php` | 199 |
| `epc_portal_industry_live_defs` | `EpcPortalIndustryLiveDefs` | `content/general_pages/epc_portal_industry_live_bridge.php` | 199 |
| `epc_portal_industry_live_storefront_url` | `EpcPortalIndustryLiveStorefrontUrl` | `content/general_pages/epc_portal_industry_live_bridge.php` | 199 |
| `epc_portal_merge_live_subs_into_industry` | `EpcPortalMergeLiveSubsIntoIndustry` | `content/general_pages/epc_portal_industry_live_bridge.php` | 199 |
| `epc_jrk_header_href` | `EpcJrkHeaderHref` | `content/general_pages/epc_portal_jewellery_retail_kiyasha_header.php` | 199 |
| `epc_portal_storefront_epartscart_svg_markup` | `EpcPortalStorefrontEpartscartSvgMarkup` | `content/general_pages/epc_portal_storefront_logo.php` | 191 |
| `epc_portal_storefront_hub_enabled` | `EpcPortalStorefrontHubEnabled` | `content/general_pages/epc_portal_storefront_logo.php` | 191 |
| `epc_portal_storefront_hub_logo_enqueue` | `EpcPortalStorefrontHubLogoEnqueue` | `content/general_pages/epc_portal_storefront_logo.php` | 191 |
| `epc_portal_storefront_hub_logo_setting` | `EpcPortalStorefrontHubLogoSetting` | `content/general_pages/epc_portal_storefront_logo.php` | 191 |
| `epc_portal_storefront_logo_markup` | `EpcPortalStorefrontLogoMarkup` | `content/general_pages/epc_portal_storefront_logo.php` | 191 |
| `epc_portal_storefront_logo_show_trade_label` | `EpcPortalStorefrontLogoShowTradeLabel` | `content/general_pages/epc_portal_storefront_logo.php` | 191 |
| `epc_portal_tenant_brand_catalog` | `EpcPortalTenantBrandCatalog` | `content/general_pages/epc_portal_tenant_brand.php` | 174 |
| `epc_portal_tenant_brand_config` | `EpcPortalTenantBrandConfig` | `content/general_pages/epc_portal_tenant_brand.php` | 174 |
| `epc_portal_tenant_brand_css_href` | `EpcPortalTenantBrandCssHref` | `content/general_pages/epc_portal_tenant_brand.php` | 174 |
| `epc_portal_tenant_brand_css_version` | `EpcPortalTenantBrandCssVersion` | `content/general_pages/epc_portal_tenant_brand.php` | 174 |
| `epc_portal_tenant_brand_enabled` | `EpcPortalTenantBrandEnabled` | `content/general_pages/epc_portal_tenant_brand.php` | 174 |
| `epc_portal_tenant_brand_enqueue` | `EpcPortalTenantBrandEnqueue` | `content/general_pages/epc_portal_tenant_brand.php` | 174 |
| `epc_portal_tenant_brand_hero_block` | `EpcPortalTenantBrandHeroBlock` | `content/general_pages/epc_portal_tenant_brand.php` | 174 |
| `epc_portal_tenant_brand_markup` | `EpcPortalTenantBrandMarkup` | `content/general_pages/epc_portal_tenant_brand.php` | 174 |
| `epc_portal_tenant_brand_site_key` | `EpcPortalTenantBrandSiteKey` | `content/general_pages/epc_portal_tenant_brand.php` | 174 |
| `epc_promo_apply` | `EpcPromoApply` | `content/general_pages/epc_promotions_engine.php` | 153 |
| `epc_promo_ensure_schema` | `EpcPromoEnsureSchema` | `content/general_pages/epc_promotions_engine.php` | 153 |
| `epc_promo_fleet_stats` | `EpcPromoFleetStats` | `content/general_pages/epc_promotions_engine.php` | 153 |
| `epc_promo_list` | `EpcPromoList` | `content/general_pages/epc_promotions_engine.php` | 153 |
| `epc_tenant_pdo` | `EpcTenantPdo` | `content/general_pages/epc_tenant_pdo.php` | 153 |
| `epc_tenant_pdo_from_row` | `EpcTenantPdoFromRow` | `content/general_pages/epc_tenant_pdo.php` | 153 |
| `epc_tenant_pdo_pool_stats` | `EpcTenantPdoPoolStats` | `content/general_pages/epc_tenant_pdo.php` | 153 |
| `epc_tenant_pdo_resolve_host` | `EpcTenantPdoResolveHost` | `content/general_pages/epc_tenant_pdo.php` | 153 |
| `epc_tenant_row_uses_dedicated_db` | `EpcTenantRowUsesDedicatedDb` | `content/general_pages/epc_tenant_pdo.php` | 153 |
| `ImplodeIfArray` | `ImplodeIfArray` | `content/originalnye-katalogi/API.v2/PHP/Functions.Common.php` | 124 |
| `SendMail` | `SendMail` | `content/originalnye-katalogi/API.v2/PHP/Functions.Common.php` | 124 |
| `ShowApiAnswer` | `ShowApiAnswer` | `content/originalnye-katalogi/API.v2/PHP/Functions.Common.php` | 124 |
| `generateArticleUrl2` | `GenerateArticleUrl2` | `content/originalnye-katalogi/API.v2/PHP/Functions.Common.php` | 124 |
| `generateBrandUrl` | `GenerateBrandUrl` | `content/originalnye-katalogi/API.v2/PHP/Functions.Common.php` | 124 |
| `generateLink2` | `GenerateLink2` | `content/originalnye-katalogi/API.v2/PHP/Functions.Common.php` | 124 |
| `getApiData` | `GetApiData` | `content/originalnye-katalogi/API.v2/PHP/Functions.Common.php` | 124 |
| `epc_sku_media_cp_install` | `EpcSkuMediaCpInstall` | `content/shop/catalogue/epc_sku_media_cp_install.php` | 118 |
| `epc_sku_media_cp_lang` | `EpcSkuMediaCpLang` | `content/shop/catalogue/epc_sku_media_cp_install.php` | 118 |
| `epc_sku_media_emit_storefront_css` | `EpcSkuMediaEmitStorefrontCss` | `content/shop/catalogue/epc_sku_media_storefront.php` | 165 |
| `epc_sku_media_render_spec_groups_html` | `EpcSkuMediaRenderSpecGroupsHtml` | `content/shop/catalogue/epc_sku_media_storefront.php` | 165 |
| `epc_sku_media_render_storefront` | `EpcSkuMediaRenderStorefront` | `content/shop/catalogue/epc_sku_media_storefront.php` | 165 |
| `epc_sku_media_storefront_load` | `EpcSkuMediaStorefrontLoad` | `content/shop/catalogue/epc_sku_media_storefront.php` | 165 |
| `epc_genuine_cache_path` | `EpcGenuineCachePath` | `content/shop/docpart/docpart_genuine_manufacturers.php` | 191 |
| `epc_genuine_count_umapi_rows` | `EpcGenuineCountUmapiRows` | `content/shop/docpart/docpart_genuine_manufacturers.php` | 191 |
| `epc_genuine_load_manufacturer_names` | `EpcGenuineLoadManufacturerNames` | `content/shop/docpart/docpart_genuine_manufacturers.php` | 191 |
| `epc_genuine_read_cache` | `EpcGenuineReadCache` | `content/shop/docpart/docpart_genuine_manufacturers.php` | 191 |
| `epc_genuine_section_counts` | `EpcGenuineSectionCounts` | `content/shop/docpart/docpart_genuine_manufacturers.php` | 191 |
| `epc_genuine_site_base_url` | `EpcGenuineSiteBaseUrl` | `content/shop/docpart/docpart_genuine_manufacturers.php` | 191 |
| `epc_genuine_sync_umapi_sections` | `EpcGenuineSyncUmapiSections` | `content/shop/docpart/docpart_genuine_manufacturers.php` | 191 |
| `epc_genuine_write_cache` | `EpcGenuineWriteCache` | `content/shop/docpart/docpart_genuine_manufacturers.php` | 191 |
| `epc_build_initial_price_bunch` | `EpcBuildInitialPriceBunch` | `content/shop/docpart/epc_build_initial_price_bunch.php` | 108 |
| `epc_prices_build_office_storage_data_info` | `EpcPricesBuildOfficeStorageDataInfo` | `content/shop/docpart/epc_prices_office_storage_meta.php` | 170 |
| `epc_logistics_configure_urls` | `EpcLogisticsConfigureUrls` | `content/shop/logistics/epc_logistics_helpers.php` | 151 |
| `epc_logistics_dashboard` | `EpcLogisticsDashboard` | `content/shop/logistics/epc_logistics_helpers.php` | 151 |
| `epc_logistics_demo_report` | `EpcLogisticsDemoReport` | `content/shop/logistics/epc_logistics_helpers.php` | 151 |
| `epc_logistics_guide_snapshot` | `EpcLogisticsGuideSnapshot` | `content/shop/logistics/epc_logistics_helpers.php` | 151 |
| `epc_logistics_h` | `EpcLogisticsH` | `content/shop/logistics/epc_logistics_helpers.php` | 151 |
| `epc_logistics_money` | `EpcLogisticsMoney` | `content/shop/logistics/epc_logistics_helpers.php` | 151 |
| `epc_logistics_seed_defaults` | `EpcLogisticsSeedDefaults` | `content/shop/logistics/epc_logistics_helpers.php` | 151 |
| `epc_logistics_seed_sample_data` | `EpcLogisticsSeedSampleData` | `content/shop/logistics/epc_logistics_helpers.php` | 151 |
| `epc_apai_cp_catalogue_filter_close` | `EpcApaiCpCatalogueFilterClose` | `content/shop/price_engine/epc_apai_cp_catalogue_filter.php` | 195 |
| `epc_apai_cp_catalogue_filter_ctx` | `EpcApaiCpCatalogueFilterCtx` | `content/shop/price_engine/epc_apai_cp_catalogue_filter.php` | 195 |
| `epc_apai_cp_catalogue_filter_render` | `EpcApaiCpCatalogueFilterRender` | `content/shop/price_engine/epc_apai_cp_catalogue_filter.php` | 195 |
| `epc_apai_cp_resolve_site_key` | `EpcApaiCpResolveSiteKey` | `content/shop/price_engine/epc_apai_cp_catalogue_filter.php` | 195 |
| `epc_ape_adapter_fetch` | `EpcApeAdapterFetch` | `content/shop/price_engine/epc_auto_price_adapters.php` | 167 |
| `epc_ape_adapter_parse_og_price` | `EpcApeAdapterParseOgPrice` | `content/shop/price_engine/epc_auto_price_adapters.php` | 167 |
| `epc_ape_adapter_registry` | `EpcApeAdapterRegistry` | `content/shop/price_engine/epc_auto_price_adapters.php` | 167 |
| `epc_price_adapter_amazon_ae` | `EpcPriceAdapterAmazonAe` | `content/shop/price_engine/epc_auto_price_adapters.php` | 167 |
| `epc_price_adapter_ebay` | `EpcPriceAdapterEbay` | `content/shop/price_engine/epc_auto_price_adapters.php` | 167 |
| `epc_price_adapter_manual` | `EpcPriceAdapterManual` | `content/shop/price_engine/epc_auto_price_adapters.php` | 167 |
| `epc_price_adapter_noon` | `EpcPriceAdapterNoon` | `content/shop/price_engine/epc_auto_price_adapters.php` | 167 |
| `epc_price_adapter_supplier` | `EpcPriceAdapterSupplier` | `content/shop/price_engine/epc_auto_price_adapters.php` | 167 |
| `epc_price_adapter_warehouse` | `EpcPriceAdapterWarehouse` | `content/shop/price_engine/epc_auto_price_adapters.php` | 167 |
| `epc_apai_clean_description` | `EpcApaiCleanDescription` | `content/shop/price_engine/epc_auto_price_ai_enrich.php` | 167 |
| `epc_apai_clean_title` | `EpcApaiCleanTitle` | `content/shop/price_engine/epc_auto_price_ai_enrich.php` | 167 |
| `epc_apai_enrich_product` | `EpcApaiEnrichProduct` | `content/shop/price_engine/epc_auto_price_ai_enrich.php` | 167 |
| `epc_apai_openai_enrich` | `EpcApaiOpenaiEnrich` | `content/shop/price_engine/epc_auto_price_ai_enrich.php` | 167 |
| `epc_apai_suggest_taxonomy` | `EpcApaiSuggestTaxonomy` | `content/shop/price_engine/epc_auto_price_ai_enrich.php` | 167 |
| `addContentToDump` | `AddContentToDump` | `cp/content/content/get_content_records.php` | 138 |
| `epc_adg_h` | `EpcAdgH` | `cp/content/control/portal/epc_api_documentation_guide.php` | 141 |
| `epc_apai_cp_load_shell_modules` | `EpcApaiCpLoadShellModules` | `cp/content/control/portal/epc_auto_price_cp_shell.php` | 194 |
| `epc_apai_cp_render_shell` | `EpcApaiCpRenderShell` | `cp/content/control/portal/epc_auto_price_cp_shell.php` | 194 |
| `epc_awg_h` | `EpcAwgH` | `cp/content/control/portal/epc_autoworkshop_guide.php` | 141 |
| `epc_csg_portal_h` | `EpcCsgPortalH` | `cp/content/control/portal/epc_custom_shipping_guide.php` | 92 |
| `epc_eog_h` | `EpcEogH` | `cp/content/control/portal/epc_erp_only_onboard_guide.php` | 89 |
| `epc_ffg_h` | `EpcFfgH` | `cp/content/control/portal/epc_platform_failover_guide.php` | 156 |
| `epc_pg_h` | `EpcPgH` | `cp/content/control/portal/epc_platform_governance.php` | 139 |
| `epc_pos_manage_h` | `EpcPosManageH` | `cp/content/control/portal/epc_pos_tenant_manage.php` | 127 |
| `epc_pbig_h` | `EpcPbigH` | `cp/content/control/portal/epc_power_bi_guide.php` | 159 |
| `epc_emod_cp_h` | `EpcEmodCpH` | `cp/content/shop/eparts-mod/eparts_mod_cp.php` | 155 |
| `epc_wa_guide_h` | `EpcWaGuideH` | `cp/content/shop/order_process/whatsapp_guide.php` | 166 |
| `clear_dir` | `ClearDir` | `cp/content/shop/prices_upload/for_pyprices/del_tmp_folder.php` | 102 |
| `epc_cp_bootstrap_light_active` | `EpcCpBootstrapLightActive` | `cp/epc_cp_bootstrap_light.php` | 108 |
| `epc_cp_bootstrap_light_init` | `EpcCpBootstrapLightInit` | `cp/epc_cp_bootstrap_light.php` | 108 |
| `epc_cp_has_admin_cookies` | `EpcCpHasAdminCookies` | `cp/epc_cp_bootstrap_light.php` | 108 |
| `epc_cp_is_login_request` | `EpcCpIsLoginRequest` | `cp/epc_cp_bootstrap_light.php` | 108 |
| `epc_cp_request_route` | `EpcCpRequestRoute` | `cp/epc_cp_bootstrap_light.php` | 108 |
| `epc_deploy_allowed_ips` | `EpcDeployAllowedIps` | `epc_deploy_auth.php` | 128 |
| `epc_deploy_client_ip` | `EpcDeployClientIp` | `epc_deploy_auth.php` | 128 |
| `epc_deploy_forbidden` | `EpcDeployForbidden` | `epc_deploy_auth.php` | 128 |
| `epc_deploy_lockdown_enabled` | `EpcDeployLockdownEnabled` | `epc_deploy_auth.php` | 128 |
| `epc_deploy_require_token` | `EpcDeployRequireToken` | `epc_deploy_auth.php` | 128 |
| `get_alternative_bread_crumbs` | `GetAlternativeBreadCrumbs` | `modules/bread_crumbs/helper.php` | 125 |
| `printCatalogueNode` | `PrintCatalogueNode` | `modules/shop/catalogue/printCatalogueNode.php` | 121 |
| `getHtmlOfTopMenuCatalogue` | `GetHtmlOfTopMenuCatalogue` | `modules/shop/catalogue/top_menu_catalog.php` | 155 |

Full table: `docs/migration/inventory/PHP_BUILDABLE_FUNCTIONS.tsv`.
