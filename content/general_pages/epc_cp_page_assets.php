<?php
/**
 * CP page assets — load CSS/JS from desktop.php head/footer (outside .row content pane).
 * Inline <style>/<script> inside CP .row renders as plain text due to template base href.
 */
defined('_ASTEXE_') or die('No access');

function epc_cp_page_asset_version(): string
{
	return function_exists('epc_cp_shell_css_version') ? epc_cp_shell_css_version() : '20260607ssftoggle1';
}

function epc_cp_page_asset_url_map(): array
{
	$ver = epc_cp_page_asset_version();
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';

	return array(
		'control/cp-guideline' => array(
			'css' => array('/content/general_pages/epc_cp_guideline_css.php?v=' . rawurlencode($ver)),
		),
		'control/portal/industry_settings' => array(
			'css' => array('/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver)),
			'js' => array(
				'/' . $backend . '/content/control/portal/industry_settings_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/industry_settings.js?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_tax_toolkit_manage' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_tax_toolkit_manage_css.php?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_visual_page_editor' => array(
			'css' => array('/content/general_pages/epc_visual_page_editor_css.php?v=' . rawurlencode($ver)),
		),
		'control/portal/epc_pos_tenant_manage' => array(
			'css' => array(
				'/content/shop/finance/epc_erp_ui.css?v=' . rawurlencode($ver),
				'/content/shop/pos/epc_pos_css.php?v=' . rawurlencode($ver),
			),
			'js' => array('/content/shop/pos/epc_pos_tenant_manage_js.php?v=' . rawurlencode($ver)),
		),
		'control/portal/epc_platform_governance' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
			'js' => array(
				'/' . $backend . '/content/control/portal/epc_platform_governance_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_platform_governance.js?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_platform_health_checkup' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
			'js' => array(
				'/' . $backend . '/content/control/portal/epc_platform_health_checkup_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_platform_health_checkup.js?v=' . rawurlencode($ver),
			),
		),
		'shop/statistics/web_tracker' => array(
			'css' => array('/content/general_pages/epc_web_tracker_cp_css.php?v=' . rawurlencode($ver . 'wt6')),
			'js' => array(
				'/' . $backend . '/content/control/portal/epc_web_tracker_config.php?v=' . rawurlencode($ver . 'wt6'),
				'/' . $backend . '/content/control/portal/epc_web_tracker_cp.js?v=' . rawurlencode($ver . 'wt6'),
			),
		),
		'control/portal/epc_web_tracker' => array(
			'css' => array('/content/general_pages/epc_web_tracker_cp_css.php?v=' . rawurlencode($ver . 'wt6')),
			'js' => array(
				'/' . $backend . '/content/control/portal/epc_web_tracker_config.php?v=' . rawurlencode($ver . 'wt6'),
				'/' . $backend . '/content/control/portal/epc_web_tracker_cp.js?v=' . rawurlencode($ver . 'wt6'),
			),
		),
		'control/portal/epc_power_bi' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
			'js' => array(),
		),
		'control/portal/epc_power_bi_guide' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
			'js' => array(),
		),
		'control/portal/epc_api_documentation_guide' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_platform_failover_guide' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_auto_price_engine' => array(
			'css' => array('/content/general_pages/epc_auto_price_engine_css.php?v=' . rawurlencode($ver)),
			'js' => array(
				'/content/general_pages/epc_auto_price_shell_js.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_auto_price_discover.js?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_auto_price_imports.js?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_auto_price_product_lines.js?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_auto_price_sources.js?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_auto_price_compare.js?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_auto_price_guide' => array(
			'css' => array('/content/general_pages/epc_auto_price_engine_css.php?v=' . rawurlencode($ver)),
		),
		'control/portal/epc_erp_only_onboard_guide' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_custom_shipping_guide' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_api_clients_manage' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_integrations_hub' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
			'js' => array(
				'/' . $backend . '/content/control/portal/epc_integrations_hub_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_integrations_hub.js?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_social_media_hub' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_social_media_hub_css.php?v=' . rawurlencode($ver . 'socPub2'),
			),
			'js' => array(
				'/content/general_pages/epc_social_media_hub_config.php?v=' . rawurlencode($ver . 'socPub2'),
				'/content/general_pages/epc_social_media_hub_js.php?v=' . rawurlencode($ver . 'socPub2'),
			),
		),
		'control/portal/epc_marketing_broadcast' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_marketing_broadcast_css.php?v=' . rawurlencode($ver),
			),
			'js' => array(
				'/content/general_pages/epc_marketing_broadcast_config.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_marketing_broadcast_js.php?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_mobile_apps' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
			'js' => array(
				'/' . $backend . '/content/control/portal/epc_mobile_apps_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_integrations_hub.js?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_tenant_features' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
			'js' => array(
				'/' . $backend . '/content/control/portal/epc_tenant_features_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_integrations_hub.js?v=' . rawurlencode($ver),
			),
		),
		'control/portal/epc_tenant_email_settings' => array(
			'css' => array(
				'/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver),
				'/content/general_pages/epc_portal_module_pages_css.php?v=' . rawurlencode($ver),
			),
			'js' => array(
				'/' . $backend . '/content/control/portal/epc_tenant_email_settings_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/control/portal/epc_integrations_hub.js?v=' . rawurlencode($ver),
			),
		),
		'shop/tenant_hub/tenant_hub' => array(
			'css' => array('/content/general_pages/epc_portal_settings_css.php?v=' . rawurlencode($ver)),
		),
		'shop/pos/terminal' => array(
			'css' => array(
				'/content/shop/finance/epc_erp_ui.css?v=' . rawurlencode($ver),
				'/content/shop/pos/epc_pos_css.php?v=' . rawurlencode($ver),
			),
			'js' => array('/content/shop/pos/epc_pos_terminal_js.php?v=' . rawurlencode($ver)),
		),
		'shop/orders/items' => array(
			'css' => array(
				'/lib/datetimepicker/jquery.datetimepicker.css',
				'/lib/multiple_select/multiple-select.css',
				'/' . $backend . '/content/users/statistics/assets/modal.css',
				'/content/general_pages/epc_orders_cp_css.php?v=' . rawurlencode($ver . 'omsForms2'),
			),
			'js' => array(
				'/lib/datetimepicker/jquery.datetimepicker.js',
				'/lib/multiple_select/jquery.multiple.select.js',
				'/' . $backend . '/content/users/statistics/assets/main.js',
				'/' . $backend . '/content/shop/order_process/orders_items_config.php?v=' . rawurlencode($ver . 'omsForms2'),
				'/' . $backend . '/content/shop/order_process/orders_items_cp.js?v=' . rawurlencode($ver . 'omsForms2'),
			),
		),
		'shop/orders/items/edit' => array(
			'js' => array(
				'/' . $backend . '/content/shop/order_process/orders_items_edit_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/shop/order_process/orders_items_edit_cp.js?v=' . rawurlencode($ver),
			),
		),
		'shop/orders/oms-guide' => array(
			'css' => array(
				'/content/general_pages/epc_orders_cp_css.php?v=' . rawurlencode($ver . 'omsGuide4'),
			),
		),
		'shop/orders/orders' => array(
			'css' => array(
				'/lib/datetimepicker/jquery.datetimepicker.css',
				'/lib/multiple_select/multiple-select.css',
				'/' . $backend . '/content/users/statistics/assets/modal.css',
				'/content/general_pages/epc_orders_cp_css.php?v=' . rawurlencode($ver . 'omsAlt1'),
			),
			'js' => array(
				'/lib/datetimepicker/jquery.datetimepicker.js',
				'/lib/multiple_select/jquery.multiple.select.js',
				'/' . $backend . '/content/users/statistics/assets/main.js',
				'/' . $backend . '/content/shop/order_process/orders_config.php?v=' . rawurlencode($ver . 'omsAlt1')
					. (isset($_GET['order_id']) ? '&order_id=' . (int) $_GET['order_id'] : '')
					. (isset($_GET['status_id']) ? '&status_id=' . (int) $_GET['status_id'] : ''),
				'/' . $backend . '/content/shop/order_process/orders_cp.js?v=' . rawurlencode($ver . 'omsAlt1'),
				'/' . $backend . '/content/shop/order_process/epc_orders_fulfillment.js?v=' . rawurlencode($ver . 'omsAlt1'),
				'/' . $backend . '/content/shop/order_process/epc_orders_supplier_fulfillment.js?v=' . rawurlencode($ver . 'omsAlt1'),
			),
		),
		'shop/finance/account_operations' => array(
			'css' => array(
				'/lib/datetimepicker/jquery.datetimepicker.css',
				'/' . $backend . '/content/users/statistics/assets/modal.css',
				'/' . $backend . '/content/shop/finance/epc_account_operations.css?v=' . rawurlencode($ver . 'aoFix3'),
			),
			'js' => array(
				'/lib/datetimepicker/jquery.datetimepicker.js',
				'/' . $backend . '/content/users/statistics/assets/main.js',
				'/' . $backend . '/content/shop/finance/account_operations_cp.js?v=' . rawurlencode($ver . 'aoFix3'),
			),
		),
		'shop/orders/statuses' => array(
			'css' => array(
				'/content/general_pages/epc_statuses_cp_css.php?v=' . rawurlencode($ver),
			),
		),
		'shop/orders/order' => array(
			'css' => array(
				'/content/general_pages/epc_orders_cp_css.php?v=' . rawurlencode($ver . 'omsForms2'),
			),
			'js' => array(
				'/' . $backend . '/content/shop/order_process/order_card_config.php?v=' . rawurlencode($ver . 'omsForms2'),
				'/' . $backend . '/content/shop/order_process/order_card_cp.js?v=' . rawurlencode($ver . 'omsForms2'),
			),
		),
		'shop/channels/channels' => array(
			'css' => array(
				'/content/general_pages/epc_channels_hub.css?v=' . rawurlencode($ver . 'chHub1'),
			),
			'js' => array(
				'/' . $backend . '/content/shop/channels/channels_hub.js?v=' . rawurlencode($ver . 'chHub1'),
			),
		),
		'shop/logistics/storages' => array(
			'js' => array(
				'/' . $backend . '/content/shop/epc_cp_table_filter.js?v=' . rawurlencode($ver . 'cpFull2'),
			),
		),
		'shop/logistics/storages/storage' => array(
			'css' => array(
				'/lib/multiple_select/multiple-select.css',
			),
			'js' => array(
				'/lib/multiple_select/jquery.multiple.select.js',
				'/' . $backend . '/content/shop/logistics/storage_users_ms.js?v=' . rawurlencode($ver . 'storMs1'),
			),
		),
		'shop/quote-requests' => array(
			'js' => array(
				'/' . $backend . '/content/shop/quote_requests/quote_alt_offer.js?v=' . rawurlencode($ver . 'qAlt2'),
			),
		),
		'shop/parts_agent_chats' => array(
			'css' => array('/content/general_pages/epc_agent_cp_css.php?v=' . rawurlencode($ver . 'cpFull2')),
			'js' => array(
				'/content/shop/parts_agent/parts_agent_chats_config.php?v=' . rawurlencode($ver . 'cpFull2'),
				'/content/general_pages/epc_agent_cp_js.php?v=' . rawurlencode($ver . 'cpFull2'),
			),
		),
		'shop/accessories' => array(
			'css' => array('/content/general_pages/epc_accessories_cp_css.php?v=' . rawurlencode($ver . 'accPhotos1')),
		),
		'shop/crosses' => array(
			'css' => array('/content/general_pages/epc_crosses_cp_css.php?v=' . rawurlencode($ver . 'cx1')),
		),
		'shop/prices' => array(
			'css' => array('/content/general_pages/epc_prices_cp_css.php?v=' . rawurlencode($ver)),
			'js' => array(
				'/' . $backend . '/content/shop/epc_cp_table_filter.js?v=' . rawurlencode($ver . 'cpFull2'),
				'/' . $backend . '/content/shop/prices_upload/epc_prices_upload_history_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/shop/prices_upload/epc_prices_upload_history.js?v=' . rawurlencode($ver),
				'/' . $backend . '/content/shop/prices_upload/epc_storefront_storage_toggle_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/shop/prices_upload/epc_storefront_storage_toggle.js?v=' . rawurlencode($ver),
			),
		),
		// Child price pages reuse the same history modal / download helpers
		'shop/prices/price' => array(
			'css' => array('/content/general_pages/epc_prices_cp_css.php?v=' . rawurlencode($ver)),
			'js' => array(
				'/' . $backend . '/content/shop/prices_upload/epc_prices_upload_history_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/shop/prices_upload/epc_prices_upload_history.js?v=' . rawurlencode($ver),
			),
		),
		'shop/prices/guide' => array(
			'css' => array('/content/general_pages/epc_prices_cp_css.php?v=' . rawurlencode($ver)),
			'js' => array(
				'/' . $backend . '/content/shop/prices_upload/epc_prices_upload_history_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/shop/prices_upload/epc_prices_upload_history.js?v=' . rawurlencode($ver),
			),
		),
		'shop/prices/multivendor' => array(
			'css' => array('/content/general_pages/epc_prices_cp_css.php?v=' . rawurlencode($ver . 'mvMin1')),
			'js' => array(
				'/' . $backend . '/content/shop/prices_upload/epc_multivendor_cp_config.php?v=' . rawurlencode($ver . 'mvMin1'),
				'/' . $backend . '/content/shop/prices_upload/epc_multivendor_cp.js?v=' . rawurlencode($ver . 'mvMin1'),
			),
		),
		'shop/catalogue/sku_media' => array(
			'css' => array('/content/shop/catalogue/epc_sku_media.css?v=' . rawurlencode($ver . 'skuMedia4')),
			'js' => array(
				'/' . $backend . '/content/shop/catalogue/epc_sku_media_cp_config.php?v=' . rawurlencode($ver . 'skuMedia4'),
				'/content/shop/catalogue/epc_sku_media.js?v=' . rawurlencode($ver . 'skuMedia4'),
			),
		),
		'shop/prices/upload' => array(
			'js' => array(
				'/' . $backend . '/content/shop/prices_upload/epc_prices_upload_history_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/shop/prices_upload/epc_prices_upload_history.js?v=' . rawurlencode($ver),
			),
		),
		'shop/prices/prices_edit' => array(
			'css' => array('/content/general_pages/epc_prices_edit_css.php?v=' . rawurlencode($ver)),
		),
		'shop/document_control/document_control' => array(
			'css' => array(
				'/content/shop/finance/epc_erp_ui.css?v=' . rawurlencode($ver),
				'/content/general_pages/epc_document_control_cp_css.php?v=' . rawurlencode($ver),
			),
			'js' => array(
				'/' . $backend . '/content/shop/document_control/epc_document_control_config.php?v=' . rawurlencode($ver),
				'/' . $backend . '/content/shop/document_control/epc_document_control.js?v=' . rawurlencode($ver),
			),
		),
	);
}

function epc_erp_shell_nav_js_src(): string
{
	if (!function_exists('epc_erp_shell_nav_js_href')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cp_shell.php';
	}
	return epc_erp_shell_nav_js_href();
}

function epc_cp_page_assets_for_url(string $contentUrl): array
{
	$contentUrl = trim($contentUrl, '/');
	$map = epc_cp_page_asset_url_map();
	$assets = array('css' => array(), 'js' => array());
	if (preg_match('#^shop/finance/erp(?:/|$)#', $contentUrl)) {
		$assets['js'][epc_erp_shell_nav_js_src()] = true;
	}
	if (isset($map[$contentUrl])) {
		foreach ($map[$contentUrl]['css'] ?? array() as $href) {
			$assets['css'][$href] = true;
		}
		foreach ($map[$contentUrl]['js'] ?? array() as $src) {
			$assets['js'][$src] = true;
		}
	}
	if (!empty($GLOBALS['epc_cp_page_assets']) && is_array($GLOBALS['epc_cp_page_assets'])) {
		foreach ($GLOBALS['epc_cp_page_assets']['css'] ?? array() as $href => $_) {
			$assets['css'][$href] = true;
		}
		foreach ($GLOBALS['epc_cp_page_assets']['js'] ?? array() as $src => $_) {
			$assets['js'][$src] = true;
		}
	}
	return $assets;
}

function epc_cp_page_head_assets(string $contentUrl): void
{
	$assets = epc_cp_page_assets_for_url($contentUrl);
	foreach (array_keys($assets['css']) as $href) {
		echo '<link rel="stylesheet" href="' . htmlspecialchars($href, ENT_QUOTES, 'UTF-8') . '" />' . "\n";
	}
}

function epc_cp_apai_discover_tab_key(): string
{
	$tab = (string) ($_GET['tab'] ?? 'discover');
	$aliases = array(
		'discovery' => 'discover',
		'taxonomy' => 'product_lines',
		'disc_sources' => 'uae_sources',
		'market_sources' => 'uae_sources',
		'settings' => 'rules',
		'dashboard' => 'discover',
		'my_imports' => 'imports',
	);
	return isset($aliases[$tab]) ? (string) $aliases[$tab] : $tab;
}

function epc_cp_apai_inline_discover_config_script(): void
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	$siteKey = isset($_GET['site_key']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_GET['site_key'])) : '';
	$tab = epc_cp_apai_discover_tab_key();
	$ajaxUrl = function_exists('epc_apai_ajax_url') ? epc_apai_ajax_url($backend) : ('/' . $backend . '/control/portal/ajax_auto_price');
	$discCfg = json_encode(array(
		'ajaxUrl' => $ajaxUrl,
		'siteKey' => $siteKey,
		'tab' => $tab,
		'backend' => $backend,
		'active' => ($tab === 'discover'),
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	$sourcesCfg = json_encode(array(
		'ajaxUrl' => $ajaxUrl,
		'siteKey' => $siteKey,
		'tab' => $tab,
		'backend' => $backend,
		'active' => ($tab === 'uae_sources'),
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	$plCfg = json_encode(array(
		'ajaxUrl' => $ajaxUrl,
		'siteKey' => $siteKey,
		'tab' => $tab,
		'backend' => $backend,
		'active' => ($tab === 'product_lines'),
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	$importsFilter = (string) ($_GET['imports_filter'] ?? 'new');
	if (!in_array($importsFilter, array('new', 'price_changes', 'duplicates'), true)) {
		$importsFilter = 'new';
	}
	$importsCfg = json_encode(array(
		'ajaxUrl' => $ajaxUrl,
		'siteKey' => $siteKey,
		'tab' => $tab,
		'filter' => $importsFilter,
		'active' => ($tab === 'imports'),
		'backend' => $backend,
		'pageBase' => '/' . $backend . '/control/portal/epc_auto_price_engine',
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	echo '<script>window.EPC_APAI_DISCOVER=' . $discCfg . ';window.EPC_APAI_SOURCES=' . $sourcesCfg . ';window.EPC_APAI_PRODUCT_LINES=' . $plCfg . ';window.EPC_APAI_IMPORTS=' . $importsCfg . ';</script>' . "\n";
}

function epc_cp_apai_shell_config_script(): void
{
	global $DP_Config;
	$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($backend === '') {
		$backend = 'cp';
	}
	$siteKey = isset($_GET['site_key']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_GET['site_key'])) : '';
	if ($siteKey === '' && function_exists('epc_portal_host')) {
		$host = strtolower((string) epc_portal_host());
		if (strpos($host, 'epartscart') !== false) {
			$siteKey = 'epartscart';
		} elseif (strpos($host, 'electronicae') !== false) {
			$siteKey = 'electronicae';
		}
	}
	$tab = epc_cp_apai_discover_tab_key();
	$ajaxUrl = function_exists('epc_apai_ajax_url') ? epc_apai_ajax_url($backend) : ('/' . $backend . '/control/portal/ajax_auto_price');
	$pageBase = '/' . $backend . '/control/portal/epc_auto_price_engine';
	$shellCfg = array(
		'active' => true,
		'ajaxUrl' => $ajaxUrl,
		'pageBase' => $pageBase,
		'backend' => $backend,
		'siteKey' => $siteKey,
		'tab' => $tab,
	);
	if ($tab === 'discover' && trim((string) ($_GET['view'] ?? '')) === '' && max(0, (int) ($_GET['taxonomy_id'] ?? 0)) === 0) {
		$shellCfg['discoverInlined'] = true;
	}
	echo '<script>window.EPC_APAI_SHELL=' . json_encode($shellCfg, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';</script>' . "\n";
}

function epc_cp_page_footer_scripts(string $contentUrl): void
{
	$contentUrl = trim($contentUrl, '/');
	$assets = epc_cp_page_assets_for_url($contentUrl);

	if ($contentUrl === 'control/portal/epc_auto_price_engine') {
		epc_cp_apai_shell_config_script();
		epc_cp_apai_inline_discover_config_script();
	}

	foreach (array_keys($assets['js']) as $src) {
		echo '<script src="' . htmlspecialchars($src, ENT_QUOTES, 'UTF-8') . '"></script>' . "\n";
	}

	if ($contentUrl === 'control/portal/epc_visual_page_editor') {
		global $DP_Config;
		$backend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
		$ver = epc_cp_page_asset_version();
		$siteKey = isset($_GET['site_key']) ? preg_replace('/[^a-z0-9_]/', '', strtolower((string) $_GET['site_key'])) : '';
		$qs = 'v=' . rawurlencode($ver);
		if ($siteKey !== '') {
			$qs .= '&site_key=' . rawurlencode($siteKey);
		}
		$configSrc = '/' . $backend . '/content/control/portal/epc_visual_page_editor_config.php?' . $qs;
		$jsSrc = '/' . $backend . '/content/control/portal/epc_visual_page_editor.js?v=' . rawurlencode($ver);
		if (empty($assets['js'][$configSrc])) {
			echo '<script src="' . htmlspecialchars($configSrc, ENT_QUOTES, 'UTF-8') . '"></script>' . "\n";
		}
		if (empty($assets['js'][$jsSrc])) {
			echo '<script src="' . htmlspecialchars($jsSrc, ENT_QUOTES, 'UTF-8') . '"></script>' . "\n";
		}
	}
}
