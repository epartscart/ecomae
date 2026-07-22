<?php
/**
 * Super CP / Tenant CP — Auto Price AI (universal multi-tenant).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_engine.php';

/**
 * Load price-engine modules — full set on POST; tab-scoped on GET.
 */
function epc_ape_cp_load_modules(string $tab, bool $fullLoad): void
{
	$doc = $_SERVER['DOCUMENT_ROOT'];
	if ($fullLoad) {
		require_once $doc . '/content/shop/price_engine/epc_auto_price_adapters.php';
		require_once $doc . '/content/shop/price_engine/epc_electronics_taxonomy.php';
		require_once $doc . '/content/shop/price_engine/epc_industry_taxonomy.php';
		require_once $doc . '/content/shop/price_engine/epc_discovery_adapters.php';
		require_once $doc . '/content/shop/price_engine/epc_apai_country_sources.php';
		if (is_file($doc . '/content/shop/price_engine/epc_apai_marketplace_channels.php')) {
			require_once $doc . '/content/shop/price_engine/epc_apai_marketplace_channels.php';
		}
		if (is_file($doc . '/content/shop/price_engine/epc_apai_product_line_rankings.php')) {
			require_once $doc . '/content/shop/price_engine/epc_apai_product_line_rankings.php';
		}
		require_once $doc . '/content/shop/price_engine/epc_auto_price_ai_enrich.php';
		if (is_file($doc . '/content/shop/price_engine/epc_auto_price_categories.php')) {
			require_once $doc . '/content/shop/price_engine/epc_auto_price_categories.php';
		}
		if (is_file($doc . '/content/shop/price_engine/epc_auto_price_storefront.php')) {
			require_once $doc . '/content/shop/price_engine/epc_auto_price_storefront.php';
		}
		if (is_file($doc . '/content/shop/price_engine/epc_auto_price_images.php')) {
			require_once $doc . '/content/shop/price_engine/epc_auto_price_images.php';
		}
		return;
	}
	require_once $doc . '/content/shop/price_engine/epc_industry_taxonomy.php';
	$discTabs = array('discover', 'imports', 'uae_sources', 'product_lines', 'compare', 'rules');
	if (in_array($tab, $discTabs, true)) {
		require_once $doc . '/content/shop/price_engine/epc_discovery_adapters.php';
		require_once $doc . '/content/shop/price_engine/epc_apai_country_sources.php';
		require_once $doc . '/content/shop/price_engine/epc_auto_price_ai_enrich.php';
	}
	if (in_array($tab, array('compare', 'rules', 'sources', 'wizard', 'listings'), true)) {
		require_once $doc . '/content/shop/price_engine/epc_auto_price_adapters.php';
	}
	if (in_array($tab, array('discover', 'imports'), true)) {
		if (is_file($doc . '/content/shop/price_engine/epc_auto_price_storefront.php')) {
			require_once $doc . '/content/shop/price_engine/epc_auto_price_storefront.php';
		}
		if (is_file($doc . '/content/shop/price_engine/epc_auto_price_images.php')) {
			require_once $doc . '/content/shop/price_engine/epc_auto_price_images.php';
		}
	}
	if ($tab === 'product_lines') {
		require_once $doc . '/content/shop/price_engine/epc_electronics_taxonomy.php';
		if (is_file($doc . '/content/shop/price_engine/epc_apai_product_line_rankings.php')) {
			require_once $doc . '/content/shop/price_engine/epc_apai_product_line_rankings.php';
		}
		if (is_file($doc . '/content/shop/price_engine/epc_auto_price_categories.php')) {
			require_once $doc . '/content/shop/price_engine/epc_auto_price_categories.php';
		}
	}
	if (in_array($tab, array('uae_sources', 'compare', 'rules'), true)) {
		if (is_file($doc . '/content/shop/price_engine/epc_apai_marketplace_channels.php')) {
			require_once $doc . '/content/shop/price_engine/epc_apai_marketplace_channels.php';
		}
	}
}

$isSuperCp = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
if (!$isSuperCp) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if (!DP_User::isAdmin()) {
		global $DP_Config;
		echo '<div class="alert alert-warning">Please <a href="/' . epc_ape_h((string) $DP_Config->backend_dir) . '/">log in to CP</a>.</div>';
		return;
	}
} else {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_super_cp_platform.php';
	if (!epc_scp_guard_super_admin()) {
		return;
	}
}

global $db_link, $DP_Config;
$platformPdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$platformPdo instanceof PDO) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}
register_shutdown_function(static function (): void {
	$err = error_get_last();
	if (!$err || !in_array($err['type'], array(E_ERROR, E_PARSE, E_COMPILE_ERROR, E_USER_ERROR), true)) {
		return;
	}
	if (headers_sent()) {
		echo '<div class="alert alert-danger"><strong>Auto Price AI fatal error:</strong> ' . epc_ape_h((string) ($err['message'] ?? 'Unknown')) . '</div>';
	}
});


$GLOBALS['epc_cp_apai_page'] = true;
$__apaiBackendRaw = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
if ($__apaiBackendRaw === '') {
	$__apaiBackendRaw = 'cp';
}
$backend = epc_ape_h($__apaiBackendRaw);
$pageBase = '/' . $__apaiBackendRaw . '/control/portal/epc_auto_price_engine';
$flash = '';
$flashClass = 'info';
$tab = (string) ($_GET['tab'] ?? 'discover');
$tabAliases = array(
	'discovery' => 'discover',
	'taxonomy' => 'product_lines',
	'disc_sources' => 'uae_sources',
	'market_sources' => 'uae_sources',
	'settings' => 'rules',
	'dashboard' => 'discover',
	'my_imports' => 'imports',
);
if (isset($tabAliases[$tab])) {
	$tab = $tabAliases[$tab];
}

$isApaiPost = ($_SERVER['REQUEST_METHOD'] === 'POST');
$apaiPartial = !empty($_GET['apai_partial']);
// Lightweight shell + AJAX tabs by default; ?apai_sync=1 forces full server render (escape hatch).
$apaiSyncTab = !empty($_GET['apai_sync']);
$apaiShellMode = !$isApaiPost && !$apaiPartial && !$apaiSyncTab;
$apaiShellInlineDiscover = $apaiShellMode
	&& $tab === 'discover'
	&& trim((string) ($_GET['view'] ?? '')) === ''
	&& max(0, (int) ($_GET['taxonomy_id'] ?? 0)) === 0;

$__apaiBackend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
require_once $_SERVER['DOCUMENT_ROOT'] . '/' . $__apaiBackend . '/content/control/portal/epc_auto_price_cp_shell.php';

if (!$apaiShellMode || $apaiShellInlineDiscover) {
	epc_ape_ensure_schema($platformPdo);
}

if ($apaiShellMode && !$apaiShellInlineDiscover) {
	epc_apai_cp_load_shell_modules();
} else {
	epc_ape_cp_load_modules($tab, $isApaiPost && !$apaiShellInlineDiscover);
}

$tenantOptions = array();
if ($isSuperCp && function_exists('epc_scp_tenant_options')) {
	$tenantOptions = epc_scp_tenant_options($platformPdo);
}

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
if ($siteKey === '') {
	if ($isSuperCp && !empty($tenantOptions)) {
		$siteKey = (string) ($tenantOptions[0]['site_key'] ?? 'platform');
	} else {
		$host = function_exists('epc_portal_host') ? strtolower(epc_portal_host()) : '';
		if (strpos($host, 'electronicae') !== false) {
			$siteKey = 'electronicae';
		} elseif (strpos($host, 'epartscart') !== false) {
			$siteKey = 'epartscart';
		} elseif (strpos($host, 'stylenlook') !== false) {
			$siteKey = 'stylenlook';
		} elseif (strpos($host, 'thejewellerytrend') !== false) {
			$siteKey = 'thejewellerytrend';
		} elseif (strpos($host, 'taxofinca') !== false) {
			$siteKey = 'taxofinca';
		} elseif (function_exists('epc_portal_site_key_from_hostname') && $host !== '') {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_tenant_intro.php';
			$siteKey = epc_portal_site_key_from_hostname($host);
		} else {
			$siteKey = 'platform';
		}
	}
}

$pdo = $platformPdo;
if ($isSuperCp && $siteKey !== '' && $siteKey !== 'platform') {
	$tenantPdo = epc_ape_tenant_pdo($platformPdo, $siteKey);
	if ($tenantPdo instanceof PDO) {
		if (!$apaiShellMode || $apaiShellInlineDiscover) {
			epc_ape_ensure_schema($tenantPdo);
		}
		$pdo = $tenantPdo;
	}
}

if ($apaiShellMode && !$apaiShellInlineDiscover) {
	try {
		epc_apai_cp_render_shell(array(
			'isSuperCp' => $isSuperCp,
			'tenantOptions' => $tenantOptions,
			'siteKey' => $siteKey,
			'tab' => $tab,
			'pageBase' => $pageBase,
			'backend' => $backend,
			'backendRaw' => $__apaiBackendRaw,
			'flash' => $flash,
			'flashClass' => $flashClass,
			'pdo' => $pdo,
		));
	} catch (Throwable $e) {
		echo '<div class="alert alert-danger"><strong>Auto Price AI shell error:</strong> ' . epc_ape_h($e->getMessage()) . '</div>';
	}
} else {

if ($apaiShellInlineDiscover) {
	$apaiPartial = true;
}

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
	$action = (string) ($_POST['epc_ape_action'] ?? '');
	try {
		if ($action === 'save_tenant_config') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			epc_ape_tenant_config_save($pdo, $sk, (string) ($_POST['profile'] ?? 'warehouse_supplier'), (string) ($_POST['currency'] ?? 'AED'));
			$siteKey = $sk;
			$flash = 'Tenant profile saved.';
			$flashClass = 'success';
		} elseif ($action === 'save_rules') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			epc_ape_rules_save($pdo, $sk, $_POST);
			if (function_exists('epc_apai_save_marketplace_arbitrage_config')) {
				$sellMps = isset($_POST['sell_marketplaces']) && is_array($_POST['sell_marketplaces'])
					? $_POST['sell_marketplaces']
					: array();
				epc_apai_save_marketplace_arbitrage_config($pdo, $sk, array(
					'sell_marketplaces' => $sellMps,
					'primary_marketplace' => (string) ($_POST['primary_marketplace'] ?? 'noon'),
					'min_margin_pct' => (float) ($_POST['arb_min_margin_pct'] ?? $_POST['min_margin_percent'] ?? 15),
					'enabled' => !empty($_POST['marketplace_arbitrage_enabled']),
				));
			}
			$siteKey = $sk;
			$flash = 'Pricing rules saved.';
			$flashClass = 'success';
		} elseif ($action === 'save_source') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			epc_ape_source_save($pdo, $sk, $_POST, max(0, (int) ($_POST['id'] ?? 0)));
			$siteKey = $sk;
			$flash = 'Price source saved.';
			$flashClass = 'success';
			$tab = 'sources';
		} elseif ($action === 'delete_source') {
			epc_ape_source_delete($pdo, max(0, (int) ($_POST['id'] ?? 0)));
			$flash = 'Source removed.';
			$flashClass = 'success';
			$tab = 'sources';
		} elseif ($action === 'add_compare_row') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$srcId = max(0, (int) ($_POST['source_id'] ?? 0));
			epc_ape_source_product_save($pdo, $srcId, array(
				'external_sku' => trim((string) ($_POST['external_sku'] ?? '')),
				'external_url' => trim((string) ($_POST['external_url'] ?? '')),
				'title' => trim((string) ($_POST['title'] ?? '')),
				'last_price' => (float) ($_POST['last_price'] ?? 0),
				'warehouse_cost' => (float) ($_POST['warehouse_cost'] ?? 0),
			));
			$siteKey = $sk;
			$flash = 'Compare row added.';
			$flashClass = 'success';
			$tab = 'compare';
		} elseif ($action === 'run_fetch') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$res = epc_ape_run_compare($pdo, $sk, 'manual');
			$siteKey = $sk;
			$flash = 'Price fetch complete: ' . ($res['summary'] ?? '');
			$flashClass = !empty($res['errors']) ? 'warning' : 'success';
			$tab = 'compare';
		} elseif ($action === 'product_wizard') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$url = trim((string) ($_POST['wizard_url'] ?? ''));
			$title = trim((string) ($_POST['wizard_title'] ?? ''));
			$price = (float) ($_POST['wizard_price'] ?? 0);
			$cost = (float) ($_POST['wizard_cost'] ?? 0);
			$srcId = max(0, (int) ($_POST['wizard_source_id'] ?? 0));

			if ($url !== '' && $title === '') {
				$extract = epc_ape_extract_url_meta($url);
				if (!empty($extract['ok'])) {
					$meta = $extract['meta'] ?? array();
					$title = (string) ($meta['title'] ?? '');
					if ($price <= 0) {
						$price = (float) ($meta['price'] ?? 0);
					}
				}
			}
			if ($title === '') {
				throw new RuntimeException('Title required — paste URL or enter manually');
			}

			$productId = epc_ape_create_catalogue_product($pdo, $title, $price);
			if ($srcId > 0) {
				epc_ape_source_product_save($pdo, $srcId, array(
					'product_id' => $productId,
					'external_url' => $url,
					'title' => $title,
					'last_price' => $price,
					'warehouse_cost' => $cost,
					'external_sku' => 'CAT-' . $productId,
				));
			}
			$siteKey = $sk;
			$flash = 'Product #' . $productId . ' created' . ($srcId ? ' and linked to source' : '') . '.';
			$flashClass = 'success';
			$tab = 'wizard';
		} elseif ($action === 'cross_list') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$productId = max(0, (int) ($_POST['product_id'] ?? 0));
			$channel = (string) ($_POST['channel_type'] ?? 'storefront');
			$price = (float) ($_POST['list_price'] ?? 0);
			$listingId = epc_ape_create_listing($pdo, $sk, $productId, $channel, $price, array('source' => 'manual_cp'));
			$siteKey = $sk;
			$flash = ucfirst($channel) . ' listing draft #' . $listingId . ' created.';
			$flashClass = 'success';
			$tab = 'listings';
		} elseif ($action === 'save_disc_source') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$data = $_POST;
			$data['source_type'] = 'custom_website';
			$data['created_by_tenant'] = 1;
			epc_disc_source_save($pdo, $sk, $data, max(0, (int) ($_POST['id'] ?? 0)));
			$siteKey = $sk;
			$flash = 'Custom discovery source saved.';
			$flashClass = 'success';
			$tab = 'uae_sources';
		} elseif ($action === 'delete_disc_source') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			if (!epc_disc_source_delete($pdo, max(0, (int) ($_POST['id'] ?? 0)), $sk)) {
				throw new RuntimeException('Only custom tenant sources can be removed.');
			}
			$flash = 'Custom discovery source removed.';
			$flashClass = 'success';
			$tab = 'uae_sources';
		} elseif ($action === 'run_discovery') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$taxSlug = preg_replace('/[^a-z0-9\-]/', '', strtolower(trim((string) ($_POST['taxonomy_slug'] ?? ''))));
			$keyword = trim((string) ($_POST['discovery_keyword'] ?? ''));
			$res = epc_disc_run_for_taxonomy($pdo, $sk, $taxSlug, $keyword);
			$siteKey = $sk;
			$flash = $res['message'] ?? 'Discovery run complete';
			if (!empty($res['search_message'])) {
				$flash .= ' — ' . $res['search_message'];
			}
			$flashClass = !empty($res['ok']) ? 'success' : 'warning';
			$tab = (string) ($_POST['return_tab'] ?? 'discover');
		} elseif ($action === 'batch_urls') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$res = epc_disc_batch_urls($pdo, $sk, (string) ($_POST['batch_urls'] ?? ''), max(0, (int) ($_POST['taxonomy_node_id'] ?? 0)));
			$siteKey = $sk;
			$flash = $res['message'] ?? 'URLs processed';
			$flashClass = !empty($res['ok']) ? 'success' : 'warning';
			$tab = 'discover';
		} elseif ($action === 'approve_discovery') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$qid = max(0, (int) ($_POST['queue_id'] ?? 0));
			$catMode = trim((string) ($_POST['category_mode'] ?? 'auto'));
			$catId = max(0, (int) ($_POST['category_id'] ?? 0));
			$approveOpts = array();
			if ($catMode !== '' && $catMode !== 'auto') {
				$approveOpts['category_mode'] = $catMode;
			}
			if ($catId > 0) {
				$approveOpts['category_id'] = $catId;
				if ($catMode === '' || $catMode === 'auto') {
					$approveOpts['category_mode'] = 'override';
				}
			}
			$res = epc_disc_queue_approve_import($pdo, $sk, $qid, $approveOpts);
			$siteKey = $sk;
			$flash = $res['message'] ?? 'Processed';
			if (!empty($res['image_warnings'])) {
				$flash .= ' — ' . epc_ape_h(implode('; ', array_slice((array) $res['image_warnings'], 0, 2)));
			}
			if (!empty($res['storefront_url'])) {
				$flash .= ' — <a href="' . epc_ape_h($res['storefront_url']) . '" target="_blank">View on storefront</a>';
			}
			$flashClass = !empty($res['ok']) ? 'success' : 'danger';
			$tab = 'imports';
		} elseif ($action === 'refresh_discovery_images') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$qid = max(0, (int) ($_POST['queue_id'] ?? 0));
			$res = epc_disc_queue_refresh_images($pdo, $sk, $qid);
			$siteKey = $sk;
			$flash = $res['message'] ?? 'Photos refreshed';
			if (!empty($res['image_warnings'])) {
				$flash .= ' — ' . epc_ape_h(implode('; ', array_slice((array) $res['image_warnings'], 0, 2)));
			}
			$flashClass = !empty($res['ok']) ? 'success' : 'warning';
			$tab = 'imports';
		} elseif ($action === 'reject_discovery') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$qid = max(0, (int) ($_POST['queue_id'] ?? 0));
			epc_disc_queue_reject($pdo, $qid, $sk);
			$siteKey = $sk;
			$flash = 'Product suggestion rejected.';
			$flashClass = 'info';
			$tab = (string) ($_POST['return_tab'] ?? 'discover');
		} elseif ($action === 'warehouse_update_price') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$baKey = trim((string) ($_POST['brand_article_key'] ?? ''));
			$res = epc_disc_warehouse_update_catalogue_price($pdo, $sk, $baKey, true);
			$siteKey = $sk;
			$flash = (string) ($res['message'] ?? 'Done');
			$flashClass = !empty($res['ok']) ? 'success' : 'warning';
			$tab = 'compare';
		} elseif ($action === 'warehouse_flag_repricing') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$baKey = trim((string) ($_POST['brand_article_key'] ?? ''));
			$res = epc_disc_warehouse_flag_repricing($pdo, $sk, $baKey);
			$siteKey = $sk;
			$flash = (string) ($res['message'] ?? 'Done');
			$flashClass = !empty($res['ok']) ? 'success' : 'warning';
			$tab = 'compare';
		} elseif ($action === 'run_warehouse_match') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			epc_disc_cross_source_match($pdo, $sk);
			$res = epc_disc_match_warehouse_to_market($pdo, $sk);
			$siteKey = $sk;
			$flash = (string) ($res['message'] ?? 'Warehouse match complete');
			$flashClass = 'success';
			$tab = (string) ($_POST['return_tab'] ?? 'compare');
		} elseif ($action === 'dismiss_duplicate') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$keepId = max(0, (int) ($_POST['keep_id'] ?? 0));
			$dismissRaw = $_POST['dismiss_ids'] ?? array();
			if (!is_array($dismissRaw)) {
				$dismissRaw = array($dismissRaw);
			}
			$dismissIds = array();
			foreach ($dismissRaw as $did) {
				$did = (int) $did;
				if ($did > 0) {
					$dismissIds[] = $did;
				}
			}
			$res = epc_disc_queue_dismiss_duplicates($pdo, $sk, $keepId, $dismissIds, !empty($_POST['approve_keep']));
			$siteKey = $sk;
			$flash = (string) ($res['message'] ?? 'Duplicate resolved');
			$flashClass = !empty($res['ok']) ? 'success' : 'danger';
			$tab = 'imports';
		} elseif ($action === 'sync_categories') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$res = epc_apai_sync_categories($pdo, $sk);
			$siteKey = $sk;
			$flash = 'Synced ' . (int) ($res['synced'] ?? 0) . ' catalogue categories for ' . epc_ape_h((string) ($res['industry_key'] ?? '')) . '.';
			$flashClass = 'success';
			$tab = 'product_lines';
		} elseif ($action === 'save_search_api') {
			$sk = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_POST['site_key'] ?? $siteKey))));
			$cfgRow = epc_ape_tenant_config_get($pdo, $sk);
			$config = (array) ($cfgRow['config'] ?? array());
			$config['serpapi_key'] = trim((string) ($_POST['serpapi_key'] ?? ''));
			$config['google_cse_key'] = trim((string) ($_POST['google_cse_key'] ?? ''));
			$config['google_cse_cx'] = trim((string) ($_POST['google_cse_cx'] ?? ''));
			$config['openai_key'] = trim((string) ($_POST['openai_key'] ?? ''));
			$config['default_margin_pct'] = (float) ($_POST['default_margin_pct'] ?? 0);
			$config['auto_suggest_enabled'] = !empty($_POST['auto_suggest_enabled']) ? 1 : 0;
			$config['show_market_prices_on_frontend'] = !empty($_POST['show_market_prices_on_frontend']) ? 1 : 0;
			$now = time();
			$pdo->prepare(
				'INSERT INTO `epc_auto_price_tenant_config` (`site_key`, `profile`, `currency`, `active`, `config_json`, `updated_at`)
				 VALUES (?, ?, ?, 1, ?, ?)
				 ON DUPLICATE KEY UPDATE `config_json` = VALUES(`config_json`), `updated_at` = VALUES(`updated_at`)'
			)->execute(array($sk, (string) ($cfgRow['profile'] ?? 'marketplace_arbitrage'), (string) ($cfgRow['currency'] ?? 'AED'), json_encode($config, JSON_UNESCAPED_UNICODE), $now));
			$siteKey = $sk;
			$flash = 'Auto Price AI config saved.';
			$flashClass = 'success';
			$tab = 'uae_sources';
		}
	} catch (Throwable $e) {
		$flash = $e->getMessage();
		$flashClass = 'danger';
	}
}

if (isset($_GET['export']) && $_GET['export'] === 'ebay_csv') {
	header('Content-Type: text/csv; charset=utf-8');
	header('Content-Disposition: attachment; filename="epc-ebay-listings-' . $siteKey . '.csv"');
	echo epc_ape_export_ebay_csv($pdo, $siteKey);
	exit;
}

$pageLoadError = '';
try {

$needsCompare = ($tab === 'compare');
$needsDiscover = ($tab === 'discover');
$needsImports = ($tab === 'imports');
$needsProductLines = ($tab === 'product_lines');
$needsUaeSources = ($tab === 'uae_sources');
$needsRules = ($tab === 'rules');
$needsListings = ($tab === 'listings');
$needsWizard = ($tab === 'wizard');
$needsSources = ($tab === 'sources');
$needsKpiChrome = !$apaiPartial && ($needsDiscover || $needsImports || $needsProductLines);
// Shell-inline Discover sets apaiPartial=true for lighter loads — still need taxonomy options in the filter.
$needsFlatTax = ($needsDiscover || $needsUaeSources || $needsCompare || $needsWizard || $needsSources)
	&& (!$apaiPartial || !empty($apaiShellInlineDiscover));

$tenantCfg = epc_ape_tenant_config_get($pdo, $siteKey);
$rules = ($needsRules || $needsCompare) ? epc_ape_rules_get($pdo, $siteKey) : array('min_margin_percent' => 15);
$pricingStrategy = function_exists('epc_apai_pricing_strategy') ? epc_apai_pricing_strategy($pdo, $siteKey) : 'lowest_cost_highest_target';
$pricingStrategyOptions = function_exists('epc_apai_pricing_strategy_options') ? epc_apai_pricing_strategy_options() : array();
$pricingMarkupPct = (float) (($tenantCfg['config'] ?? array())['pricing_markup_pct'] ?? ($rules['min_margin_percent'] ?? 15));
$sources = ($needsRules || $needsCompare || $needsUaeSources || $needsSources || $needsWizard || $needsListings)
	? epc_ape_sources_list($pdo, $siteKey)
	: array();
$kpi = ($needsCompare && !$apaiPartial) ? epc_ape_kpi($pdo, $siteKey) : array();
$whMatrix = array();
if ($needsCompare && ($tenantCfg['profile'] ?? '') === 'warehouse_supplier') {
	if (function_exists('epc_apai_link_warehouse_price_lists')) {
		epc_apai_link_warehouse_price_lists($pdo, $siteKey);
	}
	$whMatrix = epc_ape_warehouse_matrix($pdo);
}
$industryKey = function_exists('epc_apai_resolve_industry')
	? epc_apai_resolve_industry($pdo, $siteKey)
	: 'general_retail';
$discKpiEarly = $needsKpiChrome ? epc_disc_kpi($pdo, $siteKey) : array();
if ($needsKpiChrome && !empty($discKpiEarly['industry_key'])) {
	$tenantIndustryMapEarly = function_exists('epc_apai_tenant_industry_map') ? epc_apai_tenant_industry_map() : array();
	if (!isset($tenantIndustryMapEarly[$siteKey])) {
		$industryKey = (string) $discKpiEarly['industry_key'];
	}
}
$isWarehouseAutoParts = ($needsCompare && $industryKey === 'auto_parts' && ($tenantCfg['profile'] ?? '') === 'warehouse_supplier');
$matrix = ($needsCompare && !$apaiPartial && !$isWarehouseAutoParts) ? epc_ape_compare_matrix($pdo, $siteKey) : array();
$importedCompare = ($needsCompare && !$apaiPartial && !$isWarehouseAutoParts && function_exists('epc_apai_imported_compare_matrix'))
	? epc_apai_imported_compare_matrix($pdo, $siteKey)
	: array();
if ($importedCompare) {
	$matrix = $importedCompare;
}
$profiles = $needsRules ? epc_ape_profiles() : array();
$sourceTypes = ($needsRules || $needsSources || $needsUaeSources) ? epc_ape_source_types() : array();
$channelTypes = ($needsRules || $needsListings) ? epc_ape_channel_types() : array();

$discKpi = $needsKpiChrome
	? ($discKpiEarly ?: epc_disc_kpi($pdo, $siteKey))
	: array('suggested' => 0, 'imported' => 0, 'rejected' => 0, 'sources' => 0, 'taxonomy_nodes' => 0, 'industry_key' => $industryKey);
if ($needsKpiChrome && !empty($discKpi['industry_key'])) {
	$tenantIndustryMap = function_exists('epc_apai_tenant_industry_map') ? epc_apai_tenant_industry_map() : array();
	if (!isset($tenantIndustryMap[$siteKey])) {
		$industryKey = (string) $discKpi['industry_key'];
		$isWarehouseAutoParts = ($needsCompare && $industryKey === 'auto_parts' && ($tenantCfg['profile'] ?? '') === 'warehouse_supplier');
	}
}
$marketCompareMatrix = ($needsCompare && !$apaiPartial && !$isWarehouseAutoParts && function_exists('epc_disc_market_confirmed_compare_matrix'))
	? epc_disc_market_confirmed_compare_matrix($pdo, $siteKey)
	: array();
$indProfiles = epc_apai_industry_profiles();
$industryLabel = (string) (($indProfiles[$industryKey]['label'] ?? ucfirst(str_replace('_', ' ', $industryKey))));
$categoryMapCount = (($needsDiscover || $needsProductLines) && function_exists('epc_apai_category_count'))
	? epc_apai_category_count($pdo, $siteKey, $industryKey)
	: 0;
$plFastPartial = $needsProductLines && ($apaiPartial || !empty($_GET['fast_partial'])) && empty($_GET['apai_sync']);
$plPage = max(1, (int) ($_GET['pl_page'] ?? 1));
$plPerPage = $plFastPartial ? 30 : 0;
$plOffset = $plPerPage > 0 ? ($plPage - 1) * $plPerPage : 0;
$taxTree = ($needsProductLines && !$plFastPartial) ? epc_apai_tax_list_tree($pdo, $industryKey) : array();
$taxCount = ($needsKpiChrome || $needsProductLines) ? epc_apai_tax_count($pdo, $industryKey) : 0;
$discSources = ($needsDiscover || $needsUaeSources || $needsProductLines || $needsImports)
	? epc_disc_sources_for_tenant($pdo, $siteKey)
	: array();
if ($needsUaeSources) {
	if (!$apaiPartial) {
		epc_apai_install_country_sources($pdo, $siteKey);
	}
	$discSources = epc_disc_sources_for_tenant($pdo, $siteKey);
}
$tenantCountryCode = epc_apai_tenant_country($siteKey, $pdo);
$tenantCountryMeta = epc_apai_country_meta($tenantCountryCode);
$countrySourcePack = $needsUaeSources ? epc_apai_country_sources_for_tenant($pdo, $siteKey, $industryKey) : array();
$discFilterTax = max(0, (int) ($_GET['taxonomy_id'] ?? 0));
$discViewRaw = (string) ($_GET['view'] ?? $_GET['disc_visibility'] ?? '');
if ($discViewRaw === '' || $discViewRaw === 'show_all') {
	$discView = function_exists('epc_disc_default_discover_view')
		? epc_disc_default_discover_view($pdo, $siteKey)
		: 'all_suggestions';
} elseif ($discViewRaw === 'new') {
	$discView = 'all_suggestions';
} else {
	$discView = $discViewRaw;
}
$discValidViews = array('market_confirmed', 'all_suggestions', 'price_changes', 'catalogue_match', 'marketplace_opportunities');
$arbEnabled = ($needsDiscover || $needsCompare) && function_exists('epc_apai_marketplace_arbitrage_enabled') && epc_apai_marketplace_arbitrage_enabled($pdo, $siteKey);
$arbChannels = ($needsDiscover || $needsCompare) && function_exists('epc_apai_marketplace_channels_for_tenant')
	? epc_apai_marketplace_channels_for_tenant($pdo, $siteKey)
	: array();
$arbGapsMatrix = ($needsCompare && !$isWarehouseAutoParts && $arbEnabled && function_exists('epc_disc_marketplace_gaps_matrix'))
	? epc_disc_marketplace_gaps_matrix($pdo, $siteKey, array('limit' => $apaiPartial ? 20 : 50))
	: array();
if (!in_array($discView, $discValidViews, true)) {
	$discView = function_exists('epc_disc_default_discover_view')
		? epc_disc_default_discover_view($pdo, $siteKey)
		: 'all_suggestions';
}
$discViewLabels = array(
	'market_confirmed' => 'Market confirmed',
	'all_suggestions' => 'All suggestions',
	'price_changes' => 'Price changes',
	'catalogue_match' => 'My products vs market',
	'marketplace_opportunities' => 'Marketplace opportunities',
);
$discSort = (string) ($_GET['disc_sort'] ?? 'newest');
if (!in_array($discSort, array('newest', 'price_change', 'last_updated'), true)) {
	$discSort = 'newest';
}
$discFilters = $needsDiscover
	? epc_disc_default_discover_filters($pdo, $siteKey, array_merge($_GET, array(
		'disc_sort' => $discSort,
		'view' => $discView,
		'taxonomy_id' => $discFilterTax,
	)))
	: array('view' => $discView, 'disc_sort' => $discSort);
if ($needsDiscover && $apaiPartial) {
	$discFilters['fast_partial'] = true;
}
$discView = (string) ($discFilters['view'] ?? $discView);
$discViewFallback = (string) ($discFilters['fallback_from'] ?? '');
$discLastCrawlAt = $needsDiscover ? epc_disc_get_last_crawl_at($pdo, $siteKey) : 0;
$discLastScheduledCrawlAt = $needsDiscover ? epc_disc_get_last_scheduled_crawl_at($pdo, $siteKey) : 0;
$discAutoCrawlCfg = $needsDiscover ? epc_apai_auto_crawl_config($pdo, $siteKey) : array();
$discNextScheduledCrawlAt = $needsDiscover ? epc_apai_next_scheduled_crawl_at($pdo, $siteKey) : 0;
$discMinsUntilNext = ($discNextScheduledCrawlAt > 0) ? max(0, (int) ceil(($discNextScheduledCrawlAt - time()) / 60)) : 0;
$discDiscoverCounts = null;
$discQueue = array();
$discAutoSeedResult = null;
if ($needsDiscover) {
	if (function_exists('epc_disc_auto_seed_if_empty')) {
		$discAutoSeedResult = epc_disc_auto_seed_if_empty($pdo, $siteKey);
	}
	$discFilters['limit'] = $apaiPartial ? 15 : 20;
	$discQueue = epc_disc_queue_list_for_discover($pdo, $siteKey, $discFilters);
	if (function_exists('epc_disc_discover_counts')) {
		try {
			$discDiscoverCounts = epc_disc_discover_counts($pdo, $siteKey, array(
				'taxonomy_id' => $discFilterTax,
			));
		} catch (Throwable $e) {
			$discDiscoverCounts = array();
		}
	}
}
$discCrawlJob = ($needsDiscover && function_exists('epc_disc_crawl_job_active'))
	? epc_disc_crawl_job_active($pdo, $siteKey)
	: null;
$whMarketRows = array();
$whMarketCounts = array();
$whComparePrevious = array();
$whPriceListOptions = array();
$whShowPicker = $isWarehouseAutoParts;
if ($whShowPicker && function_exists('epc_disc_warehouse_market_recent')) {
	$whComparePrevious = epc_disc_warehouse_market_recent($pdo, $siteKey, 10);
}
if ($whShowPicker && function_exists('epc_disc_warehouse_price_list_options')) {
	$whPriceListOptions = epc_disc_warehouse_price_list_options($pdo, $siteKey);
}
$whBadgeLabels = function_exists('epc_disc_warehouse_market_badge_labels')
	? epc_disc_warehouse_market_badge_labels()
	: array();
$importedQueue = ($needsImports && !$apaiPartial) ? epc_disc_queue_list($pdo, $siteKey, 'imported', 0, 20) : array();
$importsFilter = (string) ($_GET['imports_filter'] ?? 'new');
if (!in_array($importsFilter, array('new', 'price_changes', 'duplicates'), true)) {
	$importsFilter = 'new';
}
$importsData = $needsImports
	? epc_disc_queue_list_for_imports($pdo, $siteKey, array(
		'filter' => $importsFilter,
		'limit' => $apaiPartial ? 15 : 20,
		'fast_partial' => $apaiPartial,
	))
	: array('items' => array(), 'groups' => array(), 'counts' => array('new' => 0, 'price_changes' => 0, 'duplicates' => 0), 'count' => 0, 'filter' => 'new');
$importsCounts = (array) ($importsData['counts'] ?? array());
$industryCategories = ($needsDiscover && !$apaiPartial && function_exists('epc_apai_list_industry_categories'))
	? epc_apai_list_industry_categories($pdo, $siteKey, $industryKey)
	: array();
$flatTax = array();
if ($needsFlatTax) {
	$flatTaxStmt = $pdo->prepare('SELECT `id`, `name_en`, `slug`, `level` FROM `epc_product_taxonomy_nodes` WHERE `active` = 1 AND `industry_key` = ? ORDER BY `level`, `sort`, `name_en`');
	$flatTaxStmt->execute(array($industryKey));
	$flatTax = $flatTaxStmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}
$productLineRankings = ($needsProductLines && function_exists('epc_apai_product_line_rankings'))
	? epc_apai_product_line_rankings($pdo, $siteKey, $industryKey, array(
		'fast_partial' => $plFastPartial,
		'limit' => $plPerPage,
		'offset' => $plOffset,
		'skip_cache' => !empty($_GET['apai_sync']),
	))
	: array('rankings' => array(), 'top' => array(), 'currency' => 'AED', 'configured_sources' => count($discSources));
$plRanked = (array) ($productLineRankings['rankings'] ?? array());
$plTop = (array) ($productLineRankings['top'] ?? array());
$plTotalRanked = (int) ($productLineRankings['total_ranked'] ?? count($plRanked));
$plHasMore = !empty($productLineRankings['has_more']);
$plCurrency = (string) ($productLineRankings['currency'] ?? 'AED');
$plConfiguredSources = (int) ($productLineRankings['configured_sources'] ?? count($discSources));
$compareFilterTaxId = max(0, (int) ($_GET['filter_taxonomy_id'] ?? 0));
if ($needsCompare && $compareFilterTaxId > 0 && function_exists('epc_apai_compare_matrix_for_taxonomy')) {
	$matrix = epc_apai_compare_matrix_for_taxonomy($pdo, $siteKey, $industryKey, $compareFilterTaxId, $matrix);
}
$tenantCfgFull = ($needsRules || $needsDiscover) ? epc_ape_tenant_config_get($pdo, $siteKey) : $tenantCfg;
$tenantSearchConfig = (array) ($tenantCfgFull['config'] ?? array());

$listings = array();
$runs = array();
if ($needsListings) {
	$listingsStmt = $pdo->prepare('SELECT cl.*, scp.`caption` FROM `epc_channel_listings` cl LEFT JOIN `shop_catalogue_products` scp ON scp.`id` = cl.`product_id` WHERE cl.`site_key` = ? ORDER BY cl.`id` DESC LIMIT 20');
	$listingsStmt->execute(array($siteKey));
	$listings = $listingsStmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}
if ($needsCompare && !$apaiPartial) {
	$runsStmt = $pdo->prepare('SELECT * FROM `epc_price_compare_runs` WHERE `site_key` = ? ORDER BY `id` DESC LIMIT 10');
	$runsStmt->execute(array($siteKey));
	$runs = $runsStmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

} catch (Throwable $e) {
	$pageLoadError = $e->getMessage();
	if (!isset($tenantCfg)) {
		$tenantCfg = array('profile' => 'warehouse_supplier', 'currency' => 'AED', 'config' => array());
	}
	if (!isset($rules)) {
		$rules = array('min_margin_percent' => 15);
	}
	if (!isset($discKpi)) {
		$discKpi = array('suggested' => 0, 'imported' => 0);
	}
	if (!isset($discQueue)) {
		$discQueue = array();
	}
	if (!isset($discSources)) {
		$discSources = array();
	}
	if (!isset($tenantCountryMeta)) {
		$tenantCountryMeta = array('label' => 'UAE', 'tld' => 'ae');
	}
	if (!isset($industryLabel)) {
		$industryLabel = 'Auto parts';
	}
	if (!isset($industryKey)) {
		$industryKey = 'auto_parts';
	}
	if (!isset($taxCount)) {
		$taxCount = 0;
	}
	if (!isset($categoryMapCount)) {
		$categoryMapCount = 0;
	}
	if (!isset($discDiscoverCounts)) {
		$discDiscoverCounts = array();
	}
	if (!isset($discViewLabels)) {
		$discViewLabels = array('all_suggestions' => 'All suggestions');
	}
	if (!isset($discView)) {
		$discView = 'all_suggestions';
	}
	if (!isset($flatTax)) {
		$flatTax = array();
	}
	if (!isset($countrySourcePack)) {
		$countrySourcePack = array();
	}
	if (!isset($sources)) {
		$sources = array();
	}
	if (!isset($sourceTypes)) {
		$sourceTypes = function_exists('epc_ape_source_types') ? epc_ape_source_types() : array();
	}
	if (!isset($tenantSearchConfig)) {
		$tenantSearchConfig = array();
	}
	if (!isset($profiles)) {
		$profiles = function_exists('epc_ape_profiles') ? epc_ape_profiles() : array();
	}
}

$guideBase = '/' . $backend . '/control/portal/epc_auto_price_guide';
?>
<?php if ($apaiShellInlineDiscover) {
	try {
		epc_apai_cp_render_shell(array(
			'isSuperCp' => $isSuperCp,
			'tenantOptions' => $tenantOptions,
			'siteKey' => $siteKey,
			'tab' => $tab,
			'pageBase' => $pageBase,
			'backend' => $backend,
			'backendRaw' => $__apaiBackendRaw,
			'flash' => $flash,
			'flashClass' => $flashClass,
			'pdo' => $pdo,
			'openTabBodyOnly' => true,
			'discoverInlined' => true,
		));
	} catch (Throwable $e) {
		echo '<div class="alert alert-danger"><strong>Auto Price AI shell error:</strong> ' . epc_ape_h($e->getMessage()) . '</div>';
		return;
	}
} ?>
<?php if (!$apaiPartial && !$apaiShellInlineDiscover) { ?>
<div class="col-lg-12 epc-ape-panel">
	<div class="epc-ape-hero">
		<h2><i class="fa fa-magic"></i> Auto Price AI</h2>
		<p class="epc-ape-hero__subtitle"><strong>Discover · Price · Import · Sell</strong> — Market: <strong><?php echo epc_ape_h($tenantCountryMeta['label']); ?></strong> (<?php echo epc_ape_h('.' . $tenantCountryMeta['tld']); ?> sources, <?php echo count($discSources); ?> configured) for <?php echo epc_ape_h($industryLabel); ?>.<?php if ($industryKey === 'auto_parts') { ?> We compare the same part across all sources. Products on 2+ sources are your target market. Match your catalogue to live market prices.<?php } elseif ($industryKey === 'tax_advisory') { ?> Benchmark service packages against competitor consultancies and FTA guidance — not retail product arbitrage.<?php } else { ?> Select products, bulk import, or search local market sites.<?php } ?><?php if ($tab === 'discover') { ?> <span class="text-muted" style="display:block;margin-top:6px;font-size:12px"><strong>Sources grow automatically:</strong> platform updates + daily sync + hourly crawl. <strong>Marketplaces:</strong> global (eBay, Amazon) + <?php echo epc_ape_h($tenantCountryMeta['label']); ?> (Noon, etc.) — not 24/7 live scraping without cron.</span><?php } ?><?php if ($tab === 'discover') {
			if ($discLastCrawlAt > 0) {
				?> <span class="epc-disc-last-crawl" id="epc-disc-global-last-crawl">Last crawl: <?php echo epc_ape_h(date('Y-m-d H:i', $discLastCrawlAt)); ?></span><?php
			} else {
				?> <span class="epc-disc-last-crawl text-muted" id="epc-disc-global-last-crawl">Last crawl: never</span><?php
			}
			if ($discLastScheduledCrawlAt > 0) {
				?> <span class="epc-disc-last-crawl text-muted" id="epc-disc-scheduled-last-crawl"> · Scheduled: <?php echo epc_ape_h(date('Y-m-d H:i', $discLastScheduledCrawlAt)); ?></span><?php
			}
			if (!empty($discAutoCrawlCfg['enabled'])) {
				?> <span class="epc-disc-next-crawl" id="epc-disc-next-crawl"> · Next auto crawl: ~<?php echo (int) $discMinsUntilNext; ?> min</span><?php
			}
		} ?></p>
		<div class="epc-ape-hero__actions">
			<a class="btn btn-sm btn-default" href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=guide'); ?>"><i class="fa fa-book"></i> Guide</a>
			<a class="btn btn-sm btn-default" href="/<?php echo $backend; ?>/shop/catalogue/products"><i class="fa fa-th-large"></i> Catalogue</a>
		</div>
	</div>

	<?php if ($pageLoadError !== '') { ?>
	<div class="alert alert-danger"><strong>Auto Price AI could not load fully.</strong> <?php echo epc_ape_h($pageLoadError); ?> — try refreshing or contact support if this persists.</div>
	<?php } ?>

	<?php if ($flash !== '') { ?>
	<div class="alert alert-<?php echo epc_ape_h($flashClass); ?>"><?php echo $flash; ?></div>
	<?php } ?>

	<?php if ($isSuperCp && $tenantOptions) { ?>
	<form method="get" class="form-inline" style="margin-bottom:14px">
		<input type="hidden" name="tab" value="<?php echo epc_ape_h($tab); ?>" />
		<label>Tenant</label>
		<select name="site_key" class="form-control input-sm" onchange="this.form.submit()">
			<?php foreach ($tenantOptions as $t) { ?>
			<option value="<?php echo epc_ape_h($t['site_key']); ?>"<?php echo $siteKey === $t['site_key'] ? ' selected' : ''; ?>><?php echo epc_ape_h($t['label']); ?></option>
			<?php } ?>
			<option value="platform"<?php echo $siteKey === 'platform' ? ' selected' : ''; ?>>Platform</option>
		</select>
	</form>
	<?php } ?>

	<div class="epc-ape-kpi" id="epc-apai-kpi">
		<div class="epc-ape-kpi__card">
			<div class="epc-ape-kpi__icon"><i class="fa fa-lightbulb-o"></i></div>
			<div class="epc-ape-kpi__body">
				<div class="epc-ape-kpi__label">Suggested</div>
				<div class="epc-ape-kpi__val"><?php echo number_format((int) $discKpi['suggested']); ?></div>
				<div class="epc-ape-kpi__hint">awaiting review</div>
			</div>
		</div>
		<div class="epc-ape-kpi__card">
			<div class="epc-ape-kpi__icon"><i class="fa fa-industry"></i></div>
			<div class="epc-ape-kpi__body">
				<div class="epc-ape-kpi__label">Industry</div>
				<div class="epc-ape-kpi__text"><?php echo epc_ape_h($industryLabel); ?></div>
			</div>
		</div>
		<div class="epc-ape-kpi__card">
			<div class="epc-ape-kpi__icon"><i class="fa fa-sitemap"></i></div>
			<div class="epc-ape-kpi__body">
				<div class="epc-ape-kpi__label">Product lines</div>
				<div class="epc-ape-kpi__val"><?php echo number_format((int) $taxCount); ?></div>
				<div class="epc-ape-kpi__hint">taxonomy nodes</div>
			</div>
		</div>
		<div class="epc-ape-kpi__card">
			<div class="epc-ape-kpi__icon"><i class="fa fa-folder-open"></i></div>
			<div class="epc-ape-kpi__body">
				<div class="epc-ape-kpi__label">Categories</div>
				<div class="epc-ape-kpi__val"><?php echo number_format((int) $categoryMapCount); ?></div>
				<div class="epc-ape-kpi__hint">catalogue maps</div>
			</div>
		</div>
		<div class="epc-ape-kpi__card">
			<div class="epc-ape-kpi__icon"><i class="fa fa-download"></i></div>
			<div class="epc-ape-kpi__body">
				<div class="epc-ape-kpi__label">Imported</div>
				<div class="epc-ape-kpi__val"><?php echo number_format((int) $discKpi['imported']); ?></div>
				<div class="epc-ape-kpi__hint">in catalogue</div>
			</div>
		</div>
	</div>

	<div class="epc-ape-tabs">
		<?php
		$tabs = array(
			'discover' => 'Discover',
			'product_lines' => 'Product lines',
			'compare' => 'Compare',
			'uae_sources' => 'Market sources',
			'imports' => 'My imports',
			'rules' => 'Rules',
			'guide' => 'Guide',
		);
		foreach ($tabs as $k => $label) {
			$cls = $tab === $k ? 'btn-primary' : 'btn-default';
			$tabHref = $pageBase . '?site_key=' . urlencode($siteKey) . '&tab=' . $k;
			echo '<a class="btn btn-sm ' . $cls . '" href="' . epc_ape_h($tabHref) . '" data-apai-tab="' . epc_ape_h($k) . '">' . epc_ape_h($label) . '</a>';
		}
		?>
	</div>

<?php } ?>

	<?php if ($tab === 'discover') {
		$discSubTabs = array();
		if ($industryKey === 'auto_parts') {
			$discSubTabs = array(
				'catalogue_match' => array('label' => 'My stock vs market', 'icon' => 'fa-balance-scale', 'hint' => 'Your catalogue priced against live sources'),
				'market_confirmed' => array('label' => 'Market confirmed', 'icon' => 'fa-check-circle', 'hint' => 'Same part on 2+ sources'),
				'all_suggestions' => array('label' => 'Suggestions', 'icon' => 'fa-lightbulb-o', 'hint' => 'New finds to review'),
				'price_changes' => array('label' => 'Price moves', 'icon' => 'fa-line-chart', 'hint' => 'Imported items with source price changes'),
			);
			if ($arbEnabled) {
				$discSubTabs = array(
					'catalogue_match' => $discSubTabs['catalogue_match'],
					'marketplace_opportunities' => array('label' => 'Sell gaps', 'icon' => 'fa-shopping-bag', 'hint' => 'Buy elsewhere → list on your marketplaces'),
					'market_confirmed' => $discSubTabs['market_confirmed'],
					'all_suggestions' => $discSubTabs['all_suggestions'],
					'price_changes' => $discSubTabs['price_changes'],
				);
			}
		} else {
			$discSubTabs = array(
				'all_suggestions' => array('label' => 'New finds', 'icon' => 'fa-lightbulb-o', 'hint' => 'Fresh suggestions'),
				'price_changes' => array('label' => 'Price moves', 'icon' => 'fa-line-chart', 'hint' => 'Source price changes'),
			);
			if ($arbEnabled) {
				$discSubTabs = array(
					'marketplace_opportunities' => array('label' => 'Sell gaps', 'icon' => 'fa-shopping-bag', 'hint' => 'Buy low, list on Noon / Amazon / eBay'),
				) + $discSubTabs;
			}
		}
		$viewMeta = $discSubTabs[$discView] ?? array('label' => ($discViewLabels[$discView] ?? 'Discover'), 'icon' => 'fa-search', 'hint' => '');
		$showingCount = count($discQueue);
		?>
	<div class="epc-disc-workspace">
		<div class="epc-disc-command">
			<div class="epc-disc-command__intro">
				<span class="epc-disc-command__eyebrow"><i class="fa fa-compass"></i> Discover</span>
				<h3><?php echo epc_ape_h($viewMeta['label']); ?></h3>
				<p>
					<?php echo epc_ape_h($industryLabel); ?> · <?php echo epc_ape_h($tenantCountryMeta['label']); ?>
					<?php if (!empty($viewMeta['hint'])) { ?> — <?php echo epc_ape_h($viewMeta['hint']); ?><?php } ?>
					· showing <strong><?php echo (int) $showingCount; ?></strong>
				</p>
			</div>
			<div class="epc-disc-command__actions">
				<button type="button" class="btn btn-primary epc-disc-cta" id="epc-disc-crawl-btn" data-crawl-mode="quick">
					<i class="fa fa-cloud-download"></i> Quick crawl
					<small>Top lines · ~30s</small>
				</button>
				<button type="button" class="btn btn-default" id="epc-disc-crawl-full-btn" data-crawl-mode="full" title="Background crawl of all sources">
					<i class="fa fa-cloud"></i> Full crawl
				</button>
				<button type="button" class="btn btn-default" id="epc-disc-fetch-btn" title="Refresh prices on visible cards">
					<i class="fa fa-refresh"></i> Refresh prices
				</button>
			</div>
		</div>

		<?php if (is_array($discAutoSeedResult) && empty($discAutoSeedResult['skipped']) && ((int) ($discAutoSeedResult['suggested'] ?? 0) > 0)) { ?>
		<div class="epc-disc-banner epc-disc-banner--ok">
			<i class="fa fa-magic"></i> Auto-loaded <?php echo (int) ($discAutoSeedResult['suggested'] ?? 0); ?> suggestion(s) from top product lines.
		</div>
		<?php } ?>

		<?php if ($discCrawlJob) { ?>
		<div class="alert alert-info epc-disc-crawl-progress" id="epc-disc-crawl-progress">
			<i class="fa fa-spinner fa-spin"></i> Crawl in progress… <span id="epc-disc-crawl-progress-msg">queued</span>
		</div>
		<?php } ?>

		<div class="epc-disc-views epc-imports-subtabs epc-disc-subtabs" id="epc-disc-subtabs">
			<?php foreach ($discSubTabs as $vk => $vmeta) {
				$vcnt = is_array($discDiscoverCounts) ? (int) ($discDiscoverCounts[$vk] ?? 0) : null;
				$vactive = $discView === $vk ? ' epc-imports-subtabs__pill--active is-active' : '';
				$vhref = $pageBase . '?site_key=' . urlencode($siteKey) . '&tab=discover&view=' . $vk;
				if ($discFilterTax > 0) {
					$vhref .= '&taxonomy_id=' . $discFilterTax;
				}
				if ($discSort !== 'newest') {
					$vhref .= '&disc_sort=' . urlencode($discSort);
				}
				$zeroCls = ($vcnt === 0) ? ' is-zero' : '';
				?>
			<a class="epc-disc-view-pill epc-imports-subtabs__pill<?php echo $vactive . $zeroCls; ?>" href="<?php echo epc_ape_h($vhref); ?>" data-disc-view="<?php echo epc_ape_h($vk); ?>" title="<?php echo epc_ape_h($vmeta['hint']); ?>">
				<i class="fa <?php echo epc_ape_h($vmeta['icon']); ?>"></i>
				<span class="epc-disc-view-pill__label"><?php echo epc_ape_h($vmeta['label']); ?></span>
				<span class="epc-disc-view-pill__count badge" data-disc-count="<?php echo epc_ape_h($vk); ?>"><?php echo $vcnt === null ? '…' : (int) $vcnt; ?></span>
			</a>
			<?php } ?>
		</div>

		<div class="epc-disc-toolbar">
			<div class="epc-disc-search">
				<div class="input-group">
					<input type="text" class="form-control" id="epc-disc-search-input" placeholder="<?php echo $industryKey === 'auto_parts' ? 'Brand + article e.g. Toyota 1310154101' : 'Search keyword or model…'; ?>" />
					<span class="input-group-btn">
						<button type="button" class="btn btn-primary" id="epc-disc-search-btn" title="Full search (all sources)"><i class="fa fa-search"></i> Search</button>
						<button type="button" class="btn btn-default" id="epc-disc-fast-search-btn" title="Fast search (3 sources)"><i class="fa fa-bolt"></i></button>
					</span>
				</div>
			</div>
			<form method="get" class="epc-disc-filters" id="epc-disc-tax-filter">
				<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
				<input type="hidden" name="tab" value="discover" />
				<input type="hidden" name="view" value="<?php echo epc_ape_h($discView); ?>" />
				<select name="taxonomy_id" id="epc-disc-taxonomy-filter" class="form-control input-sm" title="Optional product line filter">
					<option value="0">All product lines</option>
					<?php
					foreach ($flatTax as $tn) {
						$pad = str_repeat('— ', max(0, (int) $tn['level'] - 1));
						echo '<option value="' . (int) $tn['id'] . '"' . ($discFilterTax === (int) $tn['id'] ? ' selected' : '') . '>' . epc_ape_h($pad . $tn['name_en']) . '</option>';
					}
					?>
				</select>
				<select name="disc_sort" id="epc-disc-sort" class="form-control input-sm">
					<option value="newest"<?php echo $discSort === 'newest' ? ' selected' : ''; ?>>Newest</option>
					<option value="price_change"<?php echo $discSort === 'price_change' ? ' selected' : ''; ?>>Price change</option>
					<option value="last_updated"<?php echo $discSort === 'last_updated' ? ' selected' : ''; ?>>Last updated</option>
				</select>
				<button type="submit" class="btn btn-default btn-sm">Apply</button>
			</form>
			<div class="epc-disc-toolbar__meta">
				<span id="epc-disc-global-last-crawl"><?php
				if ($discLastCrawlAt > 0) {
					echo 'Last crawl ' . epc_ape_h(date('Y-m-d H:i', $discLastCrawlAt));
				} else {
					echo 'No crawl yet';
				}
				?></span>
				<a href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=product_lines'); ?>">Product lines</a>
				<a href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=uae_sources'); ?>">Sources</a>
			</div>
		</div>

		<?php if ($arbEnabled && $discView === 'marketplace_opportunities') { ?>
		<div class="epc-disc-banner epc-disc-banner--tip">
			<i class="fa fa-info-circle"></i>
			<strong>Sourcing prices ≠ sell prices.</strong>
			Buy-side quotes from <?php echo epc_ape_h(implode(', ', array_slice((array) ($arbChannels['buy'] ?? array('sharafdg', 'jumbo')), 0, 3))); ?>
			help you decide; list on <?php echo epc_ape_h(implode(', ', array_column((array) ($arbChannels['sell'] ?? array()), 'label')) ?: 'your marketplaces'); ?>.
		</div>
		<?php } ?>

		<?php if ($discViewFallback !== '') { ?>
		<div class="epc-disc-banner epc-disc-banner--tip">
			<i class="fa fa-exchange"></i>
			No items in <strong><?php echo epc_ape_h($discViewLabels[$discViewFallback] ?? $discViewFallback); ?></strong> — showing <strong><?php echo epc_ape_h($viewMeta['label']); ?></strong> instead.
			<button type="button" class="btn btn-default btn-xs" id="epc-disc-crawl-banner-btn" data-crawl-mode="quick"><i class="fa fa-cloud-download"></i> Quick crawl</button>
		</div>
		<?php } ?>

		<?php if ($discQueue && in_array($discView, array('market_confirmed', 'all_suggestions', 'marketplace_opportunities'), true)) { ?>
		<div class="epc-disc-bulk-bar" id="epc-disc-bulk-bar">
			<label style="margin:0;font-weight:normal"><input type="checkbox" id="epc-disc-select-all" /> Select all</label>
			<a href="#" id="epc-disc-clear-sel" class="btn btn-link btn-sm" style="padding-left:0">Clear</a>
			<button type="button" class="btn btn-success btn-sm" id="epc-disc-bulk-approve" disabled><i class="fa fa-plus"></i> Add selected to catalogue (0)</button>
		</div>
		<?php } ?>

		<?php if (!$discQueue) { ?>
		<div class="epc-disc-empty">
			<div class="epc-disc-empty__visual"><i class="fa <?php echo epc_ape_h($viewMeta['icon']); ?>"></i></div>
			<h4><?php
			if ($discView === 'price_changes') {
				echo 'No price moves yet';
			} elseif ($discView === 'catalogue_match') {
				echo 'No catalogue matches yet';
			} elseif ($discView === 'market_confirmed') {
				echo 'No market-confirmed parts yet';
			} elseif ($discView === 'marketplace_opportunities') {
				echo 'No sell gaps found yet';
			} else {
				echo 'Nothing in this view yet';
			}
			?></h4>
			<p><?php
			if ($discView === 'price_changes') {
				echo 'Imported products show up here when a source price moves away from your import baseline.';
			} elseif ($discView === 'catalogue_match') {
				echo 'Publish products with brand + article (or warehouse SKUs) so we can compare them to live market prices.';
			} elseif ($discView === 'market_confirmed') {
				echo 'Run a Quick crawl — parts seen on 2+ sources land here as market-confirmed.';
			} elseif ($discView === 'marketplace_opportunities') {
				echo 'Crawl buy sources first. Items you can source but are not listed on your sell marketplaces appear here.';
			} else {
				echo 'Start with Quick crawl — it scans top product lines automatically in about 30 seconds.';
			}
			?></p>
			<ol class="epc-disc-empty__steps">
				<li><strong>Crawl</strong> market sources</li>
				<li><strong>Review</strong> matches &amp; gaps</li>
				<li><strong>Import</strong> winners to catalogue</li>
			</ol>
			<div class="epc-disc-empty__actions">
				<button type="button" class="btn btn-primary" id="epc-disc-crawl-empty-btn" data-crawl-mode="quick"><i class="fa fa-cloud-download"></i> Quick crawl</button>
				<?php if ($discView !== 'catalogue_match' && is_array($discDiscoverCounts) && (int) ($discDiscoverCounts['catalogue_match'] ?? 0) > 0) { ?>
				<a class="btn btn-default" href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=discover&view=catalogue_match'); ?>"><i class="fa fa-balance-scale"></i> My stock vs market (<?php echo (int) $discDiscoverCounts['catalogue_match']; ?>)</a>
				<?php } elseif ($discView !== 'all_suggestions' && is_array($discDiscoverCounts) && (int) ($discDiscoverCounts['all_suggestions'] ?? 0) > 0) { ?>
				<a class="btn btn-default" href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=discover&view=all_suggestions'); ?>"><i class="fa fa-list"></i> Suggestions (<?php echo (int) $discDiscoverCounts['all_suggestions']; ?>)</a>
				<?php } ?>
				<a class="btn btn-default" href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=product_lines'); ?>"><i class="fa fa-sitemap"></i> Product lines</a>
			</div>
		</div>
		<?php } else { ?>
		<div class="epc-disc-grid">
					<?php foreach ($discQueue as $item) {
						$img = function_exists('epc_disc_queue_preview_image') ? epc_disc_queue_preview_image($item) : (string) ($item['images'][0] ?? '');
						$imgCount = count((array) ($item['images'] ?? array()));
						$marginOk = (float) ($item['margin_pct'] ?? 0) >= (float) ($rules['min_margin_percent'] ?? 15);
						$lastUpdated = (int) ($item['last_updated'] ?? max((int) ($item['last_fetched'] ?? 0), (int) ($item['last_crawl_at'] ?? 0), (int) ($item['updated_at'] ?? 0)));
						$priceDiffPct = (float) ($item['price_diff_pct'] ?? 0);
						$priceChanged = !empty($item['price_changed']);
						$isImported = (string) ($item['status'] ?? '') === 'imported';
						?>
					<div class="epc-disc-card" data-queue-id="<?php echo (int) $item['id']; ?>" data-last-updated="<?php echo (int) $lastUpdated; ?>" data-price-diff="<?php echo epc_ape_h((string) $priceDiffPct); ?>">
						<label class="epc-disc-card__check-wrap" title="Select for bulk import">
							<input type="checkbox" class="epc-disc-card__check" value="<?php echo (int) $item['id']; ?>" />
						</label>
						<?php if ($img !== '') { ?>
						<div class="epc-disc-card__img" style="background-image:url('<?php echo epc_ape_h($img); ?>')"></div>
						<?php } else { ?>
						<div class="epc-disc-card__img epc-disc-card__img--empty"><i class="fa fa-image"></i></div>
						<?php } ?>
						<div class="epc-disc-card__body">
							<?php if ($industryKey === 'auto_parts') { ?>
							<div class="epc-disc-part-identity">
								<?php if (!empty($item['brand']) || !empty($item['article_number'])) { ?>
								<span class="epc-disc-part-identity__brand label label-primary"><?php echo epc_ape_h($item['brand'] ?: 'OEM'); ?></span>
								<span class="epc-disc-part-identity__article"><?php echo epc_ape_h($item['article_number'] ?? ''); ?></span>
								<?php } elseif (!empty($item['needs_part_number'])) { ?>
								<span class="label label-warning">Part number required</span>
								<?php } ?>
							</div>
							<?php } ?>
							<h5><?php echo epc_ape_h($item['title']); ?></h5>
							<?php if ($imgCount > 0) { ?>
							<p class="epc-disc-card__photos"><i class="fa fa-camera"></i> <?php echo (int) min($imgCount, 5); ?> source photo<?php echo $imgCount === 1 ? '' : 's'; ?> — copied on import</p>
							<?php } ?>
							<p class="epc-disc-card__meta">
								<span class="label label-info"><?php echo epc_ape_h($item['source_domain'] ?? 'catalogue'); ?></span>
								<?php if (!empty($item['market_confirmed']) || (int) ($item['source_match_count'] ?? 0) >= 2) { ?>
								<span class="label label-success">Market confirmed · <?php echo (int) ($item['source_match_count'] ?? 2); ?> sources</span>
								<?php } ?>
								<?php if (!empty($item['arbitrage_opportunity'])) {
									$missMp = (array) ($item['missing_marketplaces'] ?? array());
									$missLbl = $missMp ? implode(', ', array_slice($missMp, 0, 3)) : 'sell marketplaces';
									?>
								<span class="label label-warning">Gap opportunity</span>
								<span class="label label-danger">Not on your marketplace</span>
								<?php } elseif (!empty($item['alternate_channel']) && empty($item['on_main_source'])) {
									$altCh = (array) $item['alternate_channel'];
									$mainLbl = epc_ape_h((string) ($altCh['main_source'] ?? 'main channel'));
									$buySrc = epc_ape_h((string) ($altCh['buy_source'] ?? ''));
									?>
								<span class="label label-warning">Not on <?php echo $mainLbl; ?> — buy from <?php echo $buySrc; ?></span>
								<?php } ?>
								<?php if ((float) ($item['spare247_price'] ?? 0) > 0) { ?>
								<span class="label label-primary">Spare247 <?php echo number_format((float) $item['spare247_price'], 2); ?></span>
								<?php } ?>
								<?php if ((int) ($item['source_match_count'] ?? 0) === 1 && empty($item['market_confirmed']) && $discView === 'all_suggestions') { ?>
								<span class="label label-warning">Single source — run crawl</span>
								<?php } ?>
								<?php if (!empty($item['is_catalogue_match'])) {
									$flag = (string) ($item['margin_flag'] ?? '');
									$flagCls = $flag === 'overpriced' ? 'label-warning' : ($flag === 'underpriced' ? 'label-danger' : 'label-success');
									?>
								<span class="label <?php echo $flagCls; ?>">Your product · <?php echo epc_ape_h(str_replace('_', ' ', $flag)); ?></span>
								<?php } ?>
								<?php if (!empty($item['taxonomy_name'])) { ?>
								<span class="label label-default"><?php echo epc_ape_h($item['taxonomy_name']); ?></span>
								<?php } ?>
								<?php if ($isImported) { ?>
								<span class="label label-success">Imported</span>
								<?php } ?>
								<?php if ($priceChanged && $priceDiffPct != 0.0) {
									$badgeCls = $priceDiffPct < 0 ? 'epc-disc-price-badge--down' : 'epc-disc-price-badge--up';
									$arrow = $priceDiffPct < 0 ? '↓' : '↑';
									$pctLabel = ($priceDiffPct > 0 ? '+' : '') . rtrim(rtrim(number_format($priceDiffPct, 1), '0'), '.') . '%';
									?>
								<span class="epc-disc-price-badge <?php echo $badgeCls; ?>" data-field="price_badge">Price <?php echo $arrow; ?> <?php echo epc_ape_h(ltrim($pctLabel, '+')); ?></span>
								<?php } elseif ($priceChanged) { ?>
								<span class="epc-disc-price-badge epc-disc-price-badge--changed" data-field="price_badge">Price changed</span>
								<?php } ?>
							</p>
							<div class="epc-disc-card__category" data-queue-id="<?php echo (int) $item['id']; ?>">
								<label class="epc-disc-cat-label">Suggested category</label>
								<div class="epc-disc-cat-advisory" data-loading="1">
									<span class="text-muted"><i class="fa fa-spinner fa-spin"></i> Analysing…</span>
								</div>
								<select class="form-control input-sm epc-disc-cat-select" name="category_override" data-queue-id="<?php echo (int) $item['id']; ?>">
									<option value="auto">Auto (recommended)</option>
									<?php foreach ($industryCategories as $ic) { ?>
									<option value="<?php echo (int) $ic['id']; ?>"><?php echo epc_ape_h($ic['label']); ?></option>
									<?php } ?>
									<option value="create_new">+ Create new from product name</option>
								</select>
							</div>
							<?php if ($lastUpdated > 0) { ?>
							<p class="epc-disc-card__fetched text-muted" style="font-size:11px;margin:0 0 6px" data-ts="<?php echo (int) $lastUpdated; ?>"><i class="fa fa-clock-o"></i> Updated <?php echo epc_ape_h(date('M j, H:i', $lastUpdated)); ?></p>
							<?php } else { ?>
							<p class="epc-disc-card__fetched text-muted" style="font-size:11px;margin:0 0 6px;display:none" data-ts="0"></p>
							<?php } ?>
							<?php if ((float) ($item['catalogue_price'] ?? 0) > 0) {
								$compareLabel = ((string) ($tenantCfgFull['profile'] ?? '') === 'warehouse_supplier') ? 'Warehouse cost' : 'Catalogue';
								?>
							<p class="epc-disc-card__catalogue text-muted" style="font-size:11px;margin:0 0 6px" data-field="catalogue_price"><?php echo epc_ape_h($compareLabel); ?>: <?php echo number_format((float) $item['catalogue_price'], 2); ?> <?php echo epc_ape_h($item['currency']); ?></p>
							<?php } ?>
							<?php if (!empty($item['specs']) && is_array($item['specs'])) { ?>
							<div class="epc-disc-card__specs">
								<?php foreach (array_slice($item['specs'], 0, 5, true) as $sk => $sv) { ?>
								<span class="epc-disc-spec-chip"><?php echo epc_ape_h($sk . ': ' . $sv); ?></span>
								<?php } ?>
							</div>
							<?php } ?>
							<?php if (!empty($item['arbitrage_opportunity'])) {
								$buySrcs = (array) ($item['buy_sources'] ?? array());
								$buyDom = (string) (($buySrcs[0]['source_domain'] ?? '') ?: ($item['source_domain'] ?? ''));
								$buyPrice = (float) ($item['buy_price_min'] ?? 0);
								$estSell = (float) ($item['estimated_marketplace_price'] ?? 0);
								$arbMargin = (float) ($item['arbitrage_margin_abs'] ?? 0);
								$arbMarginPct = (float) ($item['arbitrage_margin_pct'] ?? 0);
								$primaryMp = (string) (($arbChannels['primary_label'] ?? '') ?: 'Noon');
								$missMp = (array) ($item['missing_marketplaces'] ?? array());
								?>
							<div class="epc-disc-arbitrage-block" style="background:#fff8e6;border:1px solid #f0d78c;border-radius:4px;padding:8px 10px;margin:8px 0;font-size:12px">
								<strong>Arbitrage opportunity</strong>
								<?php if ($missMp) { ?> · Not on <?php echo epc_ape_h(implode(' or ', array_slice(array_map('ucfirst', $missMp), 0, 2))); ?><?php } ?>
								<div style="margin-top:6px">
									Buy from: <strong><?php echo epc_ape_h($buyDom); ?></strong> — <?php echo number_format($buyPrice, 0); ?> <?php echo epc_ape_h($item['currency']); ?>
								</div>
								<div>
									Est. sell on <?php echo epc_ape_h($primaryMp); ?>:
									<?php if ($estSell > 0) { ?>
									<strong>~<?php echo number_format($estSell, 0); ?> <?php echo epc_ape_h($item['currency']); ?></strong>
									<?php if (!empty($item['estimated_marketplace_known'])) { ?><small class="text-muted">(category avg)</small><?php } else { ?><small class="text-muted">(estimate)</small><?php } ?>
									<?php } else { ?>
									<em class="text-muted">Marketplace price unknown — research</em>
									<?php } ?>
								</div>
								<?php if ($arbMargin > 0) { ?>
								<div class="text-success">Margin: ~<?php echo number_format($arbMargin, 0); ?> <?php echo epc_ape_h($item['currency']); ?> (<?php echo number_format($arbMarginPct, 0); ?>%)</div>
								<?php } ?>
								<?php if ($missMp) { ?>
								<div class="text-muted" style="font-size:11px;margin-top:4px">Missing from: <?php echo epc_ape_h(implode(' · ', $missMp)); ?></div>
								<?php } ?>
							</div>
							<?php } ?>
							<?php
							$range = (array) ($item['source_price_range'] ?? array());
							if (empty($range) && function_exists('epc_disc_build_source_price_range')) {
								$range = epc_disc_build_source_price_range($pdo, $siteKey, $item);
							}
							if (!empty($range['min']) && function_exists('epc_disc_render_price_range_block')) {
								echo epc_disc_render_price_range_block($range, (array) ($item['range_flags'] ?? array()), array(
									'your_price' => (float) ($item['your_price'] ?? $item['catalogue_price'] ?? 0),
									'compare_type' => (string) ($item['compare_type'] ?? ''),
								));
							} else {
								?>
							<div class="epc-disc-card__prices">
								<div><small>Source price</small><strong data-field="source_price"><?php echo number_format((float) $item['suggested_price'], 2); ?></strong> <?php echo epc_ape_h($item['currency']); ?></div>
								<div><small>Est. cost</small><strong><?php echo number_format((float) $item['cost_estimate'], 2); ?></strong></div>
								<div><small>Sell @ margin</small><strong><?php echo number_format((float) $item['sell_price'], 2); ?></strong></div>
								<div class="<?php echo $marginOk ? 'text-success' : 'text-danger'; ?>"><small>Margin</small><strong><?php echo epc_ape_h($item['margin_pct']); ?>%</strong></div>
							</div>
							<?php } ?>
							<?php if (!empty($item['description'])) { ?>
							<p class="epc-disc-card__desc"><?php echo epc_ape_h(substr((string) $item['description'], 0, 120)); ?>…</p>
							<?php } ?>
							<div class="epc-disc-card__actions">
								<?php if (!empty($item['is_catalogue_match']) && (int) ($item['product_id'] ?? 0) > 0 && (float) (($range['target_sell_price'] ?? $range['marketplace_price'] ?? $range['max'] ?? 0)) > 0) { ?>
								<a class="btn btn-warning btn-sm" href="/<?php echo $backend; ?>/shop/catalogue/product?product_id=<?php echo (int) $item['product_id']; ?>"><i class="fa fa-tag"></i> Update price to marketplace target</a>
								<?php } elseif (in_array($discView, array('market_confirmed', 'all_suggestions', 'marketplace_opportunities'), true) && !$isImported && empty($item['is_catalogue_match'])) { ?>
								<form method="post" style="display:inline" class="epc-disc-approve-form">
									<input type="hidden" name="epc_ape_action" value="approve_discovery" />
									<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
									<input type="hidden" name="queue_id" value="<?php echo (int) $item['id']; ?>" />
									<input type="hidden" name="category_mode" value="auto" class="epc-disc-cat-mode" />
									<input type="hidden" name="category_id" value="0" class="epc-disc-cat-id" />
									<button type="submit" class="btn btn-success btn-sm"><i class="fa fa-plus"></i> <?php echo !empty($item['arbitrage_opportunity']) ? 'Add to catalogue → List on ' . epc_ape_h((string) (($arbChannels['primary_label'] ?? '') ?: 'Noon')) : 'Add to my catalogue'; ?></button>
								</form>
								<form method="post" style="display:inline">
									<input type="hidden" name="epc_ape_action" value="reject_discovery" />
									<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
									<input type="hidden" name="queue_id" value="<?php echo (int) $item['id']; ?>" />
									<button type="submit" class="btn btn-default btn-sm">Reject</button>
								</form>
								<?php } elseif ($isImported && (int) ($item['catalogue_product_id'] ?? 0) > 0) { ?>
								<a class="btn btn-default btn-sm" href="/<?php echo $backend; ?>/shop/catalogue/product?product_id=<?php echo (int) $item['catalogue_product_id']; ?>"><i class="fa fa-external-link"></i> View in catalogue</a>
								<?php } ?>
								<?php if (!empty($item['source_url'])) { ?>
								<a class="btn btn-link btn-sm" href="<?php echo epc_ape_h($item['source_url']); ?>" target="_blank" rel="noopener">View source</a>
								<?php } ?>
							</div>
						</div>
					</div>
					<?php } ?>
				</div>
				<?php } ?>

		<details class="epc-disc-advanced">
			<summary><i class="fa fa-sliders"></i> Advanced tools — taxonomy discover &amp; paste URLs</summary>
			<div class="epc-disc-advanced__grid">
				<div class="epc-disc-advanced__card">
					<h5>Run taxonomy discovery</h5>
					<form method="post">
						<input type="hidden" name="epc_ape_action" value="run_discovery" />
						<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
						<input type="hidden" name="return_tab" value="discover" />
						<div class="form-group"><label>Taxonomy slug</label>
							<input class="form-control input-sm" name="taxonomy_slug" placeholder="brakes, filters, engine-parts" value="<?php echo epc_ape_h((string) ($_GET['taxonomy'] ?? '')); ?>" />
						</div>
						<div class="form-group"><label>Keyword (optional)</label>
							<input class="form-control input-sm" name="discovery_keyword" placeholder="Toyota oil filter" />
						</div>
						<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-search"></i> Discover products</button>
					</form>
					<p class="help-block">Uses configured search / curated <?php echo epc_ape_h('.' . $tenantCountryMeta['tld']); ?> demos when APIs are offline.</p>
				</div>
				<div class="epc-disc-advanced__card">
					<h5>Paste product URLs</h5>
					<form method="post">
						<input type="hidden" name="epc_ape_action" value="batch_urls" />
						<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
						<div class="form-group"><textarea class="form-control" name="batch_urls" rows="5" placeholder="https://www.spare247.com/…&#10;https://www.autoparts.ae/…"></textarea></div>
						<button type="submit" class="btn btn-default btn-sm">Fetch &amp; queue</button>
					</form>
				</div>
			</div>
		</details>
	</div>
	<?php } ?>
	<?php if ($apaiShellInlineDiscover) { ?>
	</div>
</div>
<script>window.EPC_APAI_SHELL=<?php
	$inlineShellCfg = array(
		'active' => true,
		'ajaxUrl' => function_exists('epc_apai_ajax_url') ? epc_apai_ajax_url($__apaiBackendRaw) : ('/' . $__apaiBackendRaw . '/control/portal/ajax_auto_price'),
		'pageBase' => $pageBase,
		'backend' => $__apaiBackendRaw,
		'siteKey' => $siteKey,
		'tab' => 'discover',
		'discoverInlined' => true,
	);
	echo json_encode($inlineShellCfg, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
?>;</script>
<script>document.dispatchEvent(new CustomEvent('epc-apai-tab-loaded',{detail:{tab:'discover'}}));</script>
	<?php } ?>

	<?php if ($tab === 'product_lines') { ?>
	<div class="epc-pl-hero">
		<h3><?php echo epc_ape_h($industryLabel); ?> product lines</h3>
		<p>Ranked by market demand across <?php echo (int) $plConfiguredSources; ?> configured <?php echo epc_ape_h($tenantCountryMeta['label']); ?> retail sources — <?php echo (int) $taxCount; ?> taxonomy nodes synced to <strong><?php echo (int) $categoryMapCount; ?></strong> catalogue categories.</p>
	</div>

	<?php if ($plTop) { ?>
	<div class="hpanel"><div class="panel-heading"><h4><i class="fa fa-star"></i> Best product lines <small>Top <?php echo count($plTop); ?> by demand score</small></h4></div><div class="panel-body">
		<div class="epc-pl-top-grid">
			<?php foreach ($plTop as $line) {
				$img = (string) ($line['preview_image'] ?? '');
				$rank = (int) ($line['rank'] ?? 0);
				$trend = (string) ($line['trend'] ?? 'stable');
				?>
			<div class="epc-pl-top-card">
				<div class="epc-pl-top-card__rank">#<?php echo $rank; ?></div>
				<?php if ($img !== '') { ?>
				<div class="epc-pl-top-card__img" style="background-image:url('<?php echo epc_ape_h($img); ?>')"></div>
				<?php } else { ?>
				<div class="epc-pl-top-card__img epc-pl-top-card__img--empty"><i class="fa fa-tags"></i></div>
				<?php } ?>
				<div class="epc-pl-top-card__body">
					<h5><?php echo epc_ape_h($line['name_en']); ?></h5>
					<div class="epc-pl-top-card__meta">
						<span class="epc-pl-trend epc-pl-trend--<?php echo epc_ape_h($trend); ?>"><?php echo $trend === 'hot' ? '🔥 Hot' : 'Stable'; ?></span>
						<span class="text-muted">Score <?php echo (int) ($line['score'] ?? 0); ?></span>
					</div>
					<div class="epc-pl-source-badges">
						<?php foreach (array_slice((array) ($line['source_domains'] ?? array()), 0, 4) as $src) { ?>
						<span class="epc-pl-source-badge" title="<?php echo epc_ape_h($src['domain']); ?>"><?php echo epc_ape_h($src['label']); ?></span>
						<?php } ?>
						<?php if ((int) ($line['source_coverage'] ?? 0) > 4) { ?>
						<span class="epc-pl-source-badge epc-pl-source-badge--more">+<?php echo (int) $line['source_coverage'] - 4; ?></span>
						<?php } ?>
					</div>
					<div class="epc-pl-top-card__actions">
						<form method="post" class="epc-pl-inline-form">
							<input type="hidden" name="epc_ape_action" value="run_discovery" />
							<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
							<input type="hidden" name="taxonomy_slug" value="<?php echo epc_ape_h($line['slug']); ?>" />
							<input type="hidden" name="return_tab" value="discover" />
							<button type="submit" class="btn btn-primary btn-xs"><i class="fa fa-search"></i> Discover</button>
						</form>
						<a class="btn btn-default btn-xs" href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=discover&taxonomy_id=' . (int) $line['id']); ?>">View queue</a>
					</div>
				</div>
			</div>
			<?php } ?>
		</div>
	</div></div>
	<?php } ?>

	<div class="hpanel"><div class="panel-heading"><h4><i class="fa fa-bar-chart"></i> Ranked product lines <small><?php echo (int) $plTotalRanked; ?> lines<?php if ($plFastPartial && $plPerPage > 0) { ?>, showing <?php echo count($plRanked); ?><?php } ?></small></h4></div><div class="panel-body">
		<?php if (!$plRanked && $plTotalRanked === 0) { ?>
		<p class="text-muted">No taxonomy nodes for this industry — run seed or sync categories first.</p>
		<?php } else { ?>
		<div class="epc-pl-ranked-grid" id="epc-pl-ranked-grid" data-pl-page="<?php echo (int) $plPage; ?>" data-pl-per-page="<?php echo (int) $plPerPage; ?>">
			<?php foreach ($plRanked as $line) {
				$pmin = (float) ($line['price_min'] ?? 0);
				$pmax = (float) ($line['price_max'] ?? 0);
				$priceRange = '';
				$priceDeferred = $plFastPartial && $pmin <= 0 && $pmax <= 0;
				if ($pmin > 0 && $pmax > 0) {
					$priceRange = number_format($pmin, 0) . '–' . number_format($pmax, 0) . ' ' . epc_ape_h($plCurrency);
				} elseif ($pmin > 0) {
					$priceRange = 'from ' . number_format($pmin, 0) . ' ' . epc_ape_h($plCurrency);
				} elseif ($priceDeferred) {
					$priceRange = '';
				} else {
					$priceRange = '—';
				}
				$trend = (string) ($line['trend'] ?? 'stable');
				?>
			<div class="epc-pl-ranked-card" data-taxonomy-id="<?php echo (int) $line['id']; ?>">
				<div class="epc-pl-ranked-card__head">
					<span class="epc-pl-ranked-card__rank">#<?php echo (int) ($line['rank'] ?? 0); ?></span>
					<strong><?php echo epc_ape_h($line['name_en']); ?></strong>
					<?php if ((int) ($line['level'] ?? 1) > 1) { ?>
					<small class="text-muted">L<?php echo (int) $line['level']; ?></small>
					<?php } ?>
					<span class="epc-pl-trend epc-pl-trend--<?php echo epc_ape_h($trend); ?> pull-right"><?php echo $trend === 'hot' ? 'Hot' : 'Stable'; ?></span>
				</div>
				<div class="epc-pl-ranked-card__stats">
					<div><small>Demand score</small><strong><?php echo (int) ($line['score'] ?? 0); ?></strong></div>
					<div><small>Source coverage</small><strong><?php echo (int) ($line['source_coverage'] ?? 0); ?>/<?php echo (int) $plConfiguredSources; ?></strong></div>
					<div class="epc-pl-market-range" data-taxonomy-id="<?php echo (int) $line['id']; ?>">
						<small>Market range</small>
						<?php if ($priceDeferred) { ?>
						<strong><button type="button" class="btn btn-link btn-xs epc-pl-load-prices" data-taxonomy-id="<?php echo (int) $line['id']; ?>">Load prices</button></strong>
						<?php } else { ?>
						<strong><?php echo epc_ape_h($priceRange); ?></strong>
						<?php } ?>
					</div>
					<div><small>Suggested</small><strong><?php echo (int) ($line['suggested_count'] ?? 0); ?></strong></div>
				</div>
				<div class="epc-pl-source-badges">
					<?php foreach ((array) ($line['source_domains'] ?? array()) as $src) { ?>
					<span class="epc-pl-source-badge" title="<?php echo epc_ape_h($src['domain'] . ' (' . $src['count'] . ')'); ?>"><?php echo epc_ape_h($src['label']); ?></span>
					<?php } ?>
					<?php if ((int) ($line['source_coverage'] ?? 0) === 0) { ?>
					<span class="text-muted" style="font-size:11px">No source data yet</span>
					<?php } ?>
				</div>
				<div class="epc-pl-ranked-card__actions">
					<form method="post" class="epc-pl-inline-form">
						<input type="hidden" name="epc_ape_action" value="run_discovery" />
						<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
						<input type="hidden" name="taxonomy_slug" value="<?php echo epc_ape_h($line['slug']); ?>" />
						<input type="hidden" name="return_tab" value="discover" />
						<button type="submit" class="btn btn-primary btn-xs"><i class="fa fa-search"></i> Discover in this category</button>
					</form>
					<a class="btn btn-default btn-xs" href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=compare&filter_taxonomy_id=' . (int) $line['id']); ?>"><i class="fa fa-table"></i> Compare matrix</a>
					<a class="btn btn-link btn-xs" href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=discover&taxonomy_id=' . (int) $line['id']); ?>">Queue (<?php echo (int) ($line['queue_count'] ?? 0); ?>)</a>
				</div>
			</div>
			<?php } ?>
		</div>
		<?php if ($plHasMore) { ?>
		<p style="margin-top:14px;text-align:center">
			<button type="button" class="btn btn-default btn-sm" id="epc-pl-load-more" data-next-page="<?php echo (int) ($plPage + 1); ?>">
				<i class="fa fa-chevron-down"></i> Load more product lines (<?php echo max(0, $plTotalRanked - $plOffset - count($plRanked)); ?> remaining)
			</button>
		</p>
		<?php } ?>
		<?php } ?>
	</div></div>

	<div class="row">
		<div class="col-md-12">
			<div class="hpanel"><div class="panel-heading epc-pl-tree-toggle">
				<h4><i class="fa fa-sitemap"></i> Full taxonomy tree <small><?php echo (int) $taxCount; ?> nodes</small>
					<?php if (!$plFastPartial && $taxTree) { ?>
					<button type="button" class="btn btn-default btn-xs pull-right" id="epc-pl-tree-collapse-btn" aria-expanded="true">Collapse tree</button>
					<?php } ?>
				</h4>
			</div><div class="panel-body epc-tax-tree" id="epc-pl-tax-tree">
				<?php if ($plFastPartial) { ?>
				<p class="text-muted" style="font-size:12px">Industry taxonomy — <?php echo (int) $taxCount; ?> nodes synced to <strong><?php echo (int) $categoryMapCount; ?></strong> catalogue categories.</p>
				<button type="button" class="btn btn-default btn-sm" id="epc-pl-load-tax-tree"><i class="fa fa-sitemap"></i> Load full taxonomy tree</button>
				<?php } else { ?>
				<p class="text-muted" style="font-size:12px">Industry taxonomy — synced to <strong><?php echo (int) $categoryMapCount; ?></strong> catalogue categories in CP (<code>shop_catalogue_categories</code>).</p>
				<form method="post" style="margin-bottom:12px">
					<input type="hidden" name="epc_ape_action" value="sync_categories" />
					<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
					<button type="submit" class="btn btn-primary btn-sm"><i class="fa fa-sitemap"></i> Sync categories to catalogue</button>
					<a class="btn btn-default btn-sm" href="/<?php echo $backend; ?>/shop/catalogue/products">Open CP catalogue</a>
				</form>
				<?php
				$renderTax = function ($nodes, $depth = 0) use (&$renderTax, $pageBase, $siteKey, $plRanked) {
					if (!$nodes) {
						return;
					}
					$rankById = array();
					foreach ($plRanked as $pr) {
						$rankById[(int) $pr['id']] = (int) ($pr['rank'] ?? 0);
					}
					echo '<ul class="epc-tax-tree__list" data-depth="' . (int) $depth . '">';
					foreach ($nodes as $n) {
						$nid = (int) ($n['id'] ?? 0);
						$rank = $rankById[$nid] ?? 0;
						echo '<li>';
						if ($rank > 0) {
							echo '<span class="epc-pl-tree-rank">#' . $rank . '</span> ';
						}
						echo '<span class="epc-tax-tree__name">' . epc_ape_h($n['name_en']) . '</span> ';
						echo '<code class="epc-tax-tree__slug">' . epc_ape_h($n['slug']) . '</code>';
						echo ' <form method="post" class="epc-tax-tree__action epc-pl-inline-form" style="display:inline">';
						echo '<input type="hidden" name="epc_ape_action" value="run_discovery" />';
						echo '<input type="hidden" name="site_key" value="' . epc_ape_h($siteKey) . '" />';
						echo '<input type="hidden" name="taxonomy_slug" value="' . epc_ape_h($n['slug']) . '" />';
						echo '<input type="hidden" name="return_tab" value="discover" />';
						echo '<button type="submit" class="btn btn-xs btn-primary">Discover</button>';
						echo '</form>';
						if (!empty($n['children'])) {
							$renderTax($n['children'], $depth + 1);
						}
						echo '</li>';
					}
					echo '</ul>';
				};
				$renderTax($taxTree);
				?>
				<?php } ?>
			</div></div>
		</div>
	</div>
	<?php } ?>

	<?php if ($tab === 'imports') {
		$importsItems = (array) ($importsData['items'] ?? array());
		$importsGroups = (array) ($importsData['groups'] ?? array());
		$renderImportsPriceBadge = function (array $item) {
			$priceDiffPct = (float) ($item['price_diff_pct'] ?? 0);
			if (empty($item['price_changed'])) {
				return '';
			}
			if ($priceDiffPct != 0.0) {
				$badgeCls = $priceDiffPct < 0 ? 'epc-disc-price-badge--down' : 'epc-disc-price-badge--up';
				$arrow = $priceDiffPct < 0 ? '↓' : '↑';
				$pctLabel = ltrim(($priceDiffPct > 0 ? '+' : '') . rtrim(rtrim(number_format($priceDiffPct, 1), '0'), '.') . '%', '+');
				return '<span class="epc-disc-price-badge ' . $badgeCls . '">Price ' . $arrow . ' ' . epc_ape_h($pctLabel) . '</span>';
			}
			return '<span class="epc-disc-price-badge epc-disc-price-badge--changed">Price changed</span>';
		};
	?>
	<div class="row">
		<div class="col-md-12">
			<div class="hpanel"><div class="panel-heading"><h4>Imports queue <small><?php echo (int) count($importedQueue); ?> in catalogue · review before adding</small></h4></div><div class="panel-body">
				<div class="epc-imports-subtabs" id="epc-imports-subtabs">
					<?php
					$subTabs = array(
						'new' => 'New imports',
						'price_changes' => 'Price changes',
						'duplicates' => 'Duplicates',
					);
					foreach ($subTabs as $fk => $flabel) {
						$cnt = (int) ($importsCounts[$fk] ?? 0);
						$active = $importsFilter === $fk ? ' epc-imports-subtabs__pill--active' : '';
						echo '<a class="epc-imports-subtabs__pill' . $active . '" href="' . epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=imports&imports_filter=' . $fk) . '" data-imports-filter="' . epc_ape_h($fk) . '">';
						echo epc_ape_h($flabel) . ' <span class="badge" data-imports-count="' . epc_ape_h($fk) . '">' . $cnt . '</span></a>';
					}
					?>
				</div>

				<div id="epc-imports-list" data-filter="<?php echo epc_ape_h($importsFilter); ?>">
				<?php if ($importsFilter === 'duplicates') { ?>
					<?php if (!$importsGroups) { ?>
					<p class="text-muted epc-imports-empty">No duplicate import candidates — each queued product is unique by SKU, title, and source.</p>
					<?php } else { foreach ($importsGroups as $group) {
						$gItems = (array) ($group['items'] ?? array());
						$gCount = (int) ($group['count'] ?? count($gItems));
						?>
					<div class="epc-imports-dup-group" data-dup-key="<?php echo epc_ape_h((string) ($group['dup_key'] ?? '')); ?>">
						<div class="epc-imports-dup-group__head">
							<span class="label label-warning">Duplicate ×<?php echo $gCount; ?></span>
							<small class="text-muted"><?php echo epc_ape_h((string) ($gItems[0]['title'] ?? '')); ?></small>
						</div>
						<div class="epc-disc-grid">
						<?php foreach ($gItems as $item) {
							$img = function_exists('epc_disc_queue_preview_image') ? epc_disc_queue_preview_image($item) : (string) ($item['images'][0] ?? '');
							?>
							<div class="epc-disc-card epc-imports-dup-card" data-queue-id="<?php echo (int) $item['id']; ?>">
								<?php if ($img !== '') { ?>
								<div class="epc-disc-card__img" style="background-image:url('<?php echo epc_ape_h($img); ?>')"></div>
								<?php } else { ?>
								<div class="epc-disc-card__img epc-disc-card__img--empty"><i class="fa fa-image"></i></div>
								<?php } ?>
								<div class="epc-disc-card__body">
									<h5><?php echo epc_ape_h($item['title']); ?></h5>
									<p class="epc-disc-card__meta">
										<span class="label label-info"><?php echo epc_ape_h($item['source_domain']); ?></span>
										<?php if (!empty($item['sku'])) { ?><span class="label label-default">SKU <?php echo epc_ape_h($item['sku']); ?></span><?php } ?>
										<?php echo $renderImportsPriceBadge($item); ?>
									</p>
									<div class="epc-disc-card__prices">
										<div><small>Source</small><strong><?php echo number_format((float) $item['suggested_price'], 2); ?></strong> <?php echo epc_ape_h($item['currency']); ?></div>
										<div><small>Sell</small><strong><?php echo number_format((float) $item['sell_price'], 2); ?></strong></div>
									</div>
									<div class="epc-disc-card__actions">
										<button type="button" class="btn btn-success btn-sm epc-imports-keep-btn" data-keep-id="<?php echo (int) $item['id']; ?>"><i class="fa fa-check"></i> Keep this one</button>
										<?php if (!empty($item['source_url'])) { ?>
										<a class="btn btn-link btn-sm" href="<?php echo epc_ape_h($item['source_url']); ?>" target="_blank" rel="noopener">View source</a>
										<?php } ?>
									</div>
								</div>
							</div>
						<?php } ?>
						</div>
					</div>
					<?php } } ?>
				<?php } elseif (!$importsItems) { ?>
					<p class="text-muted epc-imports-empty"><?php
					if ($importsFilter === 'price_changes') {
						echo 'No price changes — source prices match your catalogue.';
					} else {
						echo 'No new import candidates — use <a href="' . epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=discover') . '">Discover</a> to find products.';
					}
					?></p>
				<?php } else { ?>
					<?php if ($importsFilter === 'new') { ?>
					<div class="epc-disc-bulk-bar epc-imports-bulk-bar" style="margin-bottom:12px;display:flex;flex-wrap:wrap;gap:8px;align-items:center">
						<label style="margin:0;font-weight:normal"><input type="checkbox" id="epc-imports-select-all" /> Select all</label>
						<button type="button" class="btn btn-success btn-sm" id="epc-imports-bulk-approve" disabled><i class="fa fa-plus"></i> Add selected (0)</button>
					</div>
					<?php } ?>
					<div class="epc-disc-grid" id="epc-imports-grid">
					<?php foreach ($importsItems as $item) {
						$img = function_exists('epc_disc_queue_preview_image') ? epc_disc_queue_preview_image($item) : (string) ($item['images'][0] ?? '');
						$isImported = (string) ($item['status'] ?? '') === 'imported';
						$productId = (int) ($item['product_id'] ?? 0);
						?>
						<div class="epc-disc-card" data-queue-id="<?php echo (int) $item['id']; ?>">
							<?php if ($importsFilter === 'new') { ?>
							<label class="epc-disc-card__check-wrap" title="Select for bulk import">
								<input type="checkbox" class="epc-imports-card__check" value="<?php echo (int) $item['id']; ?>" />
							</label>
							<?php } ?>
							<?php if ($img !== '') { ?>
							<div class="epc-disc-card__img" style="background-image:url('<?php echo epc_ape_h($img); ?>')"></div>
							<?php } else { ?>
							<div class="epc-disc-card__img epc-disc-card__img--empty"><i class="fa fa-image"></i></div>
							<?php } ?>
							<div class="epc-disc-card__body">
								<h5><?php echo epc_ape_h($item['title']); ?></h5>
								<p class="epc-disc-card__meta">
									<span class="label label-info"><?php echo epc_ape_h($item['source_domain']); ?></span>
									<?php if ($isImported) { ?><span class="label label-success">Imported</span><?php } ?>
									<?php if (!empty($item['sku'])) { ?><span class="label label-default">SKU <?php echo epc_ape_h($item['sku']); ?></span><?php } ?>
									<?php echo $renderImportsPriceBadge($item); ?>
								</p>
								<?php if ((float) ($item['catalogue_price'] ?? 0) > 0) { ?>
								<p class="text-muted" style="font-size:11px;margin:0 0 6px">Catalogue: <?php echo number_format((float) $item['catalogue_price'], 2); ?> <?php echo epc_ape_h($item['currency']); ?></p>
								<?php } ?>
								<?php
								$impRange = (array) ($item['source_price_range'] ?? array());
								if (!empty($impRange['min']) && function_exists('epc_disc_render_price_range_block')) {
									echo epc_disc_render_price_range_block($impRange, (array) ($item['range_flags'] ?? array()));
								} else {
									?>
								<div class="epc-disc-card__prices">
									<div><small>Source price</small><strong><?php echo number_format((float) $item['suggested_price'], 2); ?></strong> <?php echo epc_ape_h($item['currency']); ?></div>
									<div><small>Sell @ margin</small><strong><?php echo number_format((float) $item['sell_price'], 2); ?></strong></div>
								</div>
								<?php } ?>
								<div class="epc-disc-card__actions">
									<?php if ($importsFilter === 'new') { ?>
									<form method="post" style="display:inline">
										<input type="hidden" name="epc_ape_action" value="approve_discovery" />
										<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
										<input type="hidden" name="queue_id" value="<?php echo (int) $item['id']; ?>" />
										<button type="submit" class="btn btn-success btn-sm"><i class="fa fa-plus"></i> Add to catalogue</button>
									</form>
									<form method="post" style="display:inline">
										<input type="hidden" name="epc_ape_action" value="reject_discovery" />
										<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
										<input type="hidden" name="return_tab" value="imports" />
										<input type="hidden" name="queue_id" value="<?php echo (int) $item['id']; ?>" />
										<button type="submit" class="btn btn-default btn-sm">Dismiss</button>
									</form>
									<?php } elseif ($isImported && $productId > 0) { ?>
									<a class="btn btn-default btn-sm" href="/<?php echo $backend; ?>/shop/catalogue/product?product_id=<?php echo $productId; ?>">Catalogue #<?php echo $productId; ?></a>
									<form method="post" style="display:inline">
										<input type="hidden" name="epc_ape_action" value="refresh_discovery_images" />
										<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
										<input type="hidden" name="queue_id" value="<?php echo (int) $item['id']; ?>" />
										<button type="submit" class="btn btn-default btn-xs"><i class="fa fa-refresh"></i> Refresh photos</button>
									</form>
									<?php } ?>
									<?php if (!empty($item['source_url'])) { ?>
									<a class="btn btn-link btn-sm" href="<?php echo epc_ape_h($item['source_url']); ?>" target="_blank" rel="noopener">View source</a>
									<?php } ?>
								</div>
							</div>
						</div>
					<?php } ?>
					</div>
				<?php } ?>
				</div>

				<?php if ($importedQueue) { ?>
				<hr />
				<h5>In catalogue <small class="text-muted"><?php echo count($importedQueue); ?> imported</small></h5>
				<div class="table-responsive">
					<table class="table table-condensed table-striped epc-imports-table">
						<thead><tr><th></th><th>Product</th><th>Source</th><th>Sell</th><th>Catalogue</th><th></th></tr></thead>
						<tbody>
						<?php foreach (array_slice($importedQueue, 0, 20) as $item) {
							$localImgs = (array) ($item['local_images'] ?? array());
							$thumb = !empty($localImgs[0]) ? (string) $localImgs[0] : (function_exists('epc_disc_queue_preview_image') ? epc_disc_queue_preview_image($item) : (string) ($item['images'][0] ?? ''));
							$productId = (int) ($item['product_id'] ?? 0);
							$storeUrl = $productId > 0 ? epc_ape_catalogue_product_url($pdo, $productId) : '';
							?>
						<tr>
							<td class="epc-imports-table__thumb">
								<?php if ($thumb !== '') { ?>
								<span class="epc-imports-table__img" style="background-image:url('<?php echo epc_ape_h($thumb); ?>')"></span>
								<?php } else { ?>
								<span class="epc-imports-table__img epc-imports-table__img--empty"><i class="fa fa-image"></i></span>
								<?php } ?>
							</td>
							<td><strong><?php echo epc_ape_h($item['title']); ?></strong></td>
							<td><span class="label label-info"><?php echo epc_ape_h($item['source_domain']); ?></span></td>
							<td><?php echo number_format((float) $item['sell_price'], 2); ?> <?php echo epc_ape_h($item['currency']); ?></td>
							<td>
								<?php if ($productId > 0) { ?>
								<a href="/<?php echo $backend; ?>/shop/catalogue/product?product_id=<?php echo $productId; ?>">#<?php echo $productId; ?></a>
								<?php if ($storeUrl !== '') { ?><br><a href="<?php echo epc_ape_h($storeUrl); ?>" target="_blank" rel="noopener">Storefront</a><?php } ?>
								<?php } else { ?>—<?php } ?>
							</td>
							<td>
								<form method="post" style="display:inline">
									<input type="hidden" name="epc_ape_action" value="refresh_discovery_images" />
									<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
									<input type="hidden" name="queue_id" value="<?php echo (int) $item['id']; ?>" />
									<button type="submit" class="btn btn-default btn-xs"><i class="fa fa-refresh"></i></button>
								</form>
							</td>
						</tr>
						<?php } ?>
						</tbody>
					</table>
				</div>
				<?php } ?>
			</div></div>
		</div>
	</div>
	<?php } ?>

	<?php if ($tab === 'uae_sources') {
		$sourcesTabError = '';
		try { ?>
	<div class="row">
		<div class="col-md-4">
			<div class="hpanel"><div class="panel-heading"><h4>Market: <?php echo epc_ape_h($tenantCountryMeta['label']); ?></h4></div><div class="panel-body">
				<p class="text-muted" style="font-size:12px">Discovery merges <strong><?php echo count($countrySourcePack); ?></strong> country-pack domains, industry overlays, and your custom websites. Country from tax toolkit / ERP settings.</p>
				<p class="text-muted" style="font-size:11px;margin-bottom:0"><a href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=discover'); ?>">Discover tab</a> search and Product lines <strong>Discover in this category</strong> use the merged list below.</p>
			</div></div>
			<div class="hpanel"><div class="panel-heading"><h4>Add custom source</h4></div><div class="panel-body">
				<form id="epc-disc-src-form" class="epc-disc-src-form">
					<input type="hidden" name="id" id="epc-disc-src-id" value="0" />
					<div class="form-group"><label>Domain or URL</label><input class="form-control input-sm" name="domain" id="epc-disc-src-domain" placeholder="customshop.ae or https://myvendor.com" required /></div>
					<div class="form-group"><label>Label (display name)</label><input class="form-control input-sm" name="label" id="epc-disc-src-label" placeholder="My Vendor UAE" /></div>
					<div class="form-group"><label>Product line (optional)</label>
						<select class="form-control input-sm" name="taxonomy_node_id" id="epc-disc-src-taxonomy">
							<option value="0">All product lines (global)</option>
							<?php foreach ($flatTax as $tn) {
								$pad = str_repeat('— ', max(0, (int) $tn['level'] - 1));
								echo '<option value="' . (int) $tn['id'] . '" data-slug="' . epc_ape_h($tn['slug']) . '">' . epc_ape_h($pad . $tn['name_en']) . '</option>';
							} ?>
						</select>
						<p class="help-block" style="font-size:11px;margin-bottom:0">When set, this source is prioritized for that product line only.</p>
					</div>
					<div class="form-group"><label><input type="checkbox" name="enabled" id="epc-disc-src-enabled" value="1" checked> Enabled</label></div>
					<div class="form-group epc-disc-src-auth-block">
						<label><input type="checkbox" name="requires_login" id="epc-disc-src-requires-login" value="1"> Requires login</label>
						<p class="help-block" style="font-size:11px;margin:4px 0 8px">For restricted B2B sites (e.g. spare247.com). Enter username and password below — stored per tenant, used only for price crawl.</p>
						<div id="epc-disc-src-auth-fields" class="epc-disc-src-auth-fields">
							<div class="form-group" style="margin-bottom:8px">
								<label>Auth type</label>
								<select class="form-control input-sm" name="auth_type" id="epc-disc-src-auth-type">
									<option value="none">None</option>
									<option value="form_login">Form login (B2B portal)</option>
									<option value="basic">HTTP Basic</option>
								</select>
							</div>
							<div class="form-group" style="margin-bottom:8px">
								<label>Username</label>
								<input type="text" class="form-control input-sm" name="auth_username" id="epc-disc-src-auth-username" autocomplete="off" placeholder="Portal username or email" />
							</div>
							<div class="form-group" style="margin-bottom:8px">
								<label>Password</label>
								<input type="password" class="form-control input-sm" name="auth_password" id="epc-disc-src-auth-password" autocomplete="new-password" placeholder="Enter password" />
							</div>
							<div class="form-group epc-disc-src-form-login-only" style="margin-bottom:8px">
								<label>Login URL (optional)</label>
								<input class="form-control input-sm" name="login_url" id="epc-disc-src-login-url" placeholder="https://www.spare247.com/login" />
							</div>
							<div class="form-group epc-disc-src-form-login-only" style="margin-bottom:8px">
								<label>Login form selector (optional)</label>
								<input class="form-control input-sm" name="login_form_selector" id="epc-disc-src-login-selector" placeholder="form[action*=login]" />
							</div>
							<div class="form-group" style="margin-bottom:0">
								<button type="button" class="btn btn-default btn-sm" id="epc-disc-src-test-login"><i class="fa fa-sign-in"></i> Test login</button>
								<span id="epc-disc-src-test-result" class="epc-disc-src-test-result" style="display:none"></span>
							</div>
						</div>
					</div>
					<button type="submit" class="btn btn-primary btn-sm" id="epc-disc-src-submit"><i class="fa fa-plus"></i> Add custom source</button>
					<button type="button" class="btn btn-link btn-sm" id="epc-disc-src-cancel" style="display:none">Cancel edit</button>
				</form>
			</div></div>
			<?php if ($isSuperCp) { ?>
			<div class="hpanel"><div class="panel-heading"><h4>Auto Price AI config</h4></div><div class="panel-body">
				<p class="text-muted" style="font-size:11px">Optional — live Google/SerpAPI discovery scoped to <?php echo epc_ape_h($tenantCountryMeta['label']); ?> + OpenAI product copy. Without keys, rule-based cleanup + curated market seed.</p>
				<form method="post">
					<input type="hidden" name="epc_ape_action" value="save_search_api" />
					<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
					<div class="form-group"><label>SerpAPI key</label><input class="form-control input-sm" name="serpapi_key" value="<?php echo epc_ape_h($tenantSearchConfig['serpapi_key'] ?? ''); ?>" autocomplete="off" /></div>
					<div class="form-group"><label>Google CSE key</label><input class="form-control input-sm" name="google_cse_key" value="<?php echo epc_ape_h($tenantSearchConfig['google_cse_key'] ?? ''); ?>" autocomplete="off" /></div>
					<div class="form-group"><label>Google CSE CX</label><input class="form-control input-sm" name="google_cse_cx" value="<?php echo epc_ape_h($tenantSearchConfig['google_cse_cx'] ?? ''); ?>" /></div>
					<div class="form-group"><label>OpenAI key (optional)</label><input class="form-control input-sm" name="openai_key" value="<?php echo epc_ape_h($tenantSearchConfig['openai_key'] ?? ''); ?>" autocomplete="off" placeholder="sk-…" /></div>
					<div class="form-group"><label>Default margin %</label><input type="number" step="0.1" class="form-control input-sm" name="default_margin_pct" value="<?php echo epc_ape_h($tenantSearchConfig['default_margin_pct'] ?? ($rules['min_margin_percent'] ?? 15)); ?>" /></div>
					<div class="form-group"><label><input type="checkbox" name="auto_suggest_enabled" value="1"<?php echo !empty($tenantSearchConfig['auto_suggest_enabled']) ? ' checked' : ''; ?>> Auto-suggest on discovery run</label></div>
					<div class="form-group"><label><input type="checkbox" name="show_market_prices_on_frontend" value="1"<?php echo (array_key_exists('show_market_prices_on_frontend', $tenantSearchConfig) ? !empty($tenantSearchConfig['show_market_prices_on_frontend']) : (($tenantCfg['profile'] ?? '') === 'marketplace_arbitrage')) ? ' checked' : ''; ?>> Show market prices on storefront product pages</label></div>
					<button type="submit" class="btn btn-default btn-sm">Save config</button>
				</form>
			</div></div>
			<?php } elseif (!$isSuperCp) { ?>
			<div class="hpanel"><div class="panel-heading"><h4>Search API</h4></div><div class="panel-body">
				<p class="text-muted" style="font-size:12px">Configure SerpAPI / Google CSE / OpenAI in Super CP with tenant selector, or paste product URLs in Discover tab.</p>
			</div></div>
			<?php } ?>
		</div>
		<div class="col-md-8">
			<div class="hpanel"><div class="panel-heading"><h4>Discovery sources (<span id="epc-disc-src-count"><?php echo count($discSources); ?></span>) — <?php echo epc_ape_h($tenantCountryMeta['label']); ?></h4></div><div class="panel-body table-responsive">
				<table class="table table-condensed table-striped" id="epc-disc-src-table">
					<thead><tr><th>Origin</th><th>Domain</th><th>Label</th><th>Product line</th><th>Auth</th><th>Status</th><th>Last crawl</th><th></th></tr></thead>
					<tbody id="epc-disc-src-tbody">
					<?php
					$taxNameById = array();
					foreach ($flatTax as $tn) {
						$taxNameById[(int) $tn['id']] = (string) $tn['name_en'];
					}
					foreach ($discSources as $ds) {
						$fmt = epc_disc_source_format_row($ds);
						$originLabel = $fmt['origin'] === 'custom' ? 'Custom' : 'Country pack';
						$originClass = $fmt['origin'] === 'custom' ? 'label-success' : 'label-default';
						$scopeLabel = 'All lines';
						if ((int) ($fmt['taxonomy_node_id'] ?? 0) > 0 && isset($taxNameById[(int) $fmt['taxonomy_node_id']])) {
							$scopeLabel = $taxNameById[(int) $fmt['taxonomy_node_id']];
						} elseif ((string) ($fmt['product_line_slug'] ?? '') !== '') {
							$scopeLabel = (string) $fmt['product_line_slug'];
						}
						?>
					<tr data-source-id="<?php echo (int) $fmt['id']; ?>" data-editable="<?php echo $fmt['editable'] ? '1' : '0'; ?>">
						<td><span class="label <?php echo epc_ape_h($originClass); ?>"><?php echo epc_ape_h($originLabel); ?></span></td>
						<td><code><?php echo epc_ape_h($fmt['domain']); ?></code></td>
						<td><?php echo epc_ape_h($fmt['label']); ?></td>
						<td><?php echo epc_ape_h($scopeLabel); ?></td>
						<td><?php
						if (!empty($fmt['last_test_ok'])) {
							echo '<span class="label label-success">Login OK ✓</span>';
						} elseif (!empty($fmt['last_test_at']) && empty($fmt['last_test_ok'])) {
							echo '<span class="label label-danger">Login failed</span>';
						} elseif (!empty($fmt['login_configured'])) {
							echo '<span class="label label-info">Login configured</span>';
						} else {
							echo '<span class="text-muted">—</span>';
						}
						?></td>
						<td><?php echo $fmt['enabled'] ? '<span class="text-success">Enabled</span>' : '<span class="text-muted">Disabled</span>'; ?></td>
						<td><?php echo (int) ($fmt['last_crawl'] ?? 0) ? date('m-d H:i', (int) $fmt['last_crawl']) : '—'; ?></td>
						<td class="epc-disc-src-actions">
							<?php if ($fmt['editable']) { ?>
							<button type="button" class="btn btn-xs btn-default epc-disc-src-edit" data-source='<?php echo epc_ape_h(json_encode($fmt, JSON_UNESCAPED_UNICODE)); ?>'>Edit</button>
							<button type="button" class="btn btn-xs btn-warning epc-disc-src-toggle"><?php echo $fmt['enabled'] ? 'Disable' : 'Enable'; ?></button>
							<button type="button" class="btn btn-xs btn-danger epc-disc-src-delete">×</button>
							<?php } else { ?>
							<button type="button" class="btn btn-xs btn-warning epc-disc-src-toggle"><?php echo $fmt['enabled'] ? 'Disable' : 'Enable'; ?></button>
							<?php } ?>
						</td>
					</tr>
					<?php } ?>
					<?php if (!$discSources) { ?><tr class="epc-disc-src-empty"><td colspan="8" class="text-muted">No sources — run setup seed or add a custom website.</td></tr><?php } ?>
					</tbody>
				</table>
			</div></div>
			<div class="hpanel"><div class="panel-heading"><h4>Price compare sources</h4></div><div class="panel-body table-responsive">
				<p class="text-muted" style="font-size:12px">Legacy compare matrix sources (Amazon.ae, Noon, warehouse cost).</p>
				<table class="table table-condensed table-striped">
					<thead><tr><th>Type</th><th>Name</th><th>Last check</th></tr></thead>
					<tbody>
					<?php foreach ($sources as $s) { ?>
					<tr>
						<td><code><?php echo epc_ape_h($s['source_type']); ?></code></td>
						<td><?php echo epc_ape_h($s['name']); ?></td>
						<td><?php echo (int) ($s['last_checked_at'] ?? 0) ? date('m-d H:i', (int) $s['last_checked_at']) : '—'; ?></td>
					</tr>
					<?php } ?>
					</tbody>
				</table>
				<form method="post" class="form-inline" style="margin-top:10px">
					<input type="hidden" name="epc_ape_action" value="save_source" />
					<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
					<select name="source_type" class="form-control input-sm">
						<?php foreach ($sourceTypes as $k => $lbl) { ?>
						<option value="<?php echo epc_ape_h($k); ?>"><?php echo epc_ape_h($lbl); ?></option>
						<?php } ?>
					</select>
					<input class="form-control input-sm" name="name" placeholder="Source name" required />
					<button type="submit" class="btn btn-default btn-sm">Add compare source</button>
				</form>
			</div></div>
		</div>
	</div>
	<?php
		} catch (Throwable $e) {
			$sourcesTabError = $e->getMessage();
		}
		if ($sourcesTabError !== '') { ?>
	<div class="alert alert-danger"><strong>Market sources tab could not load.</strong> <?php echo epc_ape_h($sourcesTabError); ?> — other tabs may still work; try refreshing or contact support.</div>
	<?php }
	} ?>

	<?php if ($tab === 'guide') {
		$guidePanelPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/epc_auto_price_guide_panel.php';
		if (is_file($guidePanelPath)) {
			require $guidePanelPath;
		} else {
			echo '<div class="alert alert-danger">Guide panel file missing. Deploy <code>cp/content/control/portal/epc_auto_price_guide_panel.php</code>.</div>';
		}
	} ?>

	<?php if ($tab === 'rules') { ?>
	<div class="row">
		<div class="col-md-5">
			<div class="hpanel"><div class="panel-heading"><h4>Tenant profile</h4></div><div class="panel-body">
				<form method="post" class="form-horizontal">
					<input type="hidden" name="epc_ape_action" value="save_tenant_config" />
					<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
					<div class="form-group"><label class="col-sm-4">Profile</label><div class="col-sm-8">
						<select name="profile" class="form-control input-sm">
							<?php foreach ($profiles as $k => $p) { ?>
							<option value="<?php echo epc_ape_h($k); ?>"<?php echo ($tenantCfg['profile'] ?? '') === $k ? ' selected' : ''; ?>><?php echo epc_ape_h($p['label']); ?></option>
							<?php } ?>
						</select>
						<p class="help-block"><?php echo epc_ape_h($profiles[$tenantCfg['profile']]['hint'] ?? ''); ?></p>
					</div></div>
					<div class="form-group"><label class="col-sm-4">Currency</label><div class="col-sm-8"><input class="form-control input-sm" name="currency" value="<?php echo epc_ape_h($tenantCfg['currency'] ?? 'AED'); ?>" /></div></div>
					<div class="form-group"><div class="col-sm-8 col-sm-offset-4"><button type="submit" class="btn btn-primary btn-sm">Save profile</button></div></div>
				</form>
			</div></div>
		</div>
		<div class="col-md-7">
			<div class="hpanel"><div class="panel-heading"><h4>Pricing rules</h4></div><div class="panel-body">
				<form method="post" class="form-horizontal">
					<input type="hidden" name="epc_ape_action" value="save_rules" />
					<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
					<div class="form-group"><label class="col-sm-4">Pricing strategy</label><div class="col-sm-8">
						<select name="pricing_strategy" class="form-control input-sm">
							<?php foreach ($pricingStrategyOptions as $psk => $pslabel) { ?>
							<option value="<?php echo epc_ape_h($psk); ?>"<?php echo $pricingStrategy === $psk ? ' selected' : ''; ?>><?php echo epc_ape_h($pslabel); ?></option>
							<?php } ?>
						</select>
						<p class="help-block" style="font-size:11px;margin-top:4px">On import: catalog cost = lowest source price; sell price follows strategy. Default for warehouse / arbitrage tenants.</p>
					</div></div>
					<div class="form-group"><label class="col-sm-4">Markup %</label><div class="col-sm-8"><input type="number" step="0.1" class="form-control input-sm" name="pricing_markup_pct" value="<?php echo epc_ape_h($pricingMarkupPct); ?>" /><p class="help-block" style="font-size:11px">Used when strategy is "Lowest cost + fixed markup %"</p></div></div>
					<div class="form-group"><label class="col-sm-4">Min margin %</label><div class="col-sm-8"><input type="number" step="0.1" class="form-control input-sm" name="min_margin_percent" value="<?php echo epc_ape_h($rules['min_margin_percent'] ?? 15); ?>" /></div></div>
					<div class="form-group"><label class="col-sm-4">Schedule (hours)</label><div class="col-sm-8"><input type="number" class="form-control input-sm" name="schedule_hours" value="<?php echo epc_ape_h($rules['schedule_hours'] ?? 24); ?>" /></div></div>
					<div class="form-group"><label class="col-sm-4">Cross-list channels</label><div class="col-sm-8"><input class="form-control input-sm" name="cross_list_channels" value="<?php echo epc_ape_h($rules['cross_list_channels'] ?? 'storefront,ebay'); ?>" /></div></div>
					<?php if ($arbEnabled || in_array((string) ($tenantCfg['profile'] ?? ''), array('marketplace_arbitrage', 'warehouse_supplier'), true)) {
						$arbCfg = (array) (($tenantCfgFull['config']['marketplace_arbitrage'] ?? array()));
						$sellSelected = (array) ($arbCfg['sell_marketplaces'] ?? ($arbChannels['sell_domains'] ?? array()));
						$registry = function_exists('epc_apai_sell_marketplace_registry') ? epc_apai_sell_marketplace_registry($tenantCountryCode) : array();
						?>
					<hr />
					<h5 style="margin:0 0 10px">Marketplace arbitrage</h5>
					<div class="form-group"><label class="col-sm-4">Sell marketplaces</label><div class="col-sm-8">
						<?php foreach ($registry as $rk => $rentry) {
							$rdom = (string) ($rentry['domain'] ?? '');
							$checked = in_array($rdom, $sellSelected, true) ? ' checked' : '';
							?>
						<label class="checkbox-inline" style="margin-right:10px"><input type="checkbox" name="sell_marketplaces[]" value="<?php echo epc_ape_h($rdom); ?>"<?php echo $checked; ?>> <?php echo epc_ape_h((string) ($rentry['label'] ?? $rdom)); ?></label>
						<?php } ?>
						<p class="help-block" style="font-size:11px">Where you list products for sale (Noon, Amazon, eBay). We only sell on these marketplaces.</p>
					</div></div>
					<div class="alert alert-warning" style="font-size:11px;margin:0 12px 12px;padding:8px 10px">
						<strong>Buy sources</strong> (Sharaf DG, Jumbo, spare247, autoparts.ae…) are shown for <em>sourcing only</em> — we do not sell on those sites. Discover cards show buy price ranges; sell target comes from marketplace listings only.
					</div>
					<div class="form-group"><label class="col-sm-4">Primary marketplace</label><div class="col-sm-8">
						<select name="primary_marketplace" class="form-control input-sm">
							<?php foreach ($registry as $rk => $rentry) {
								$sel = ((string) ($arbCfg['primary_marketplace'] ?? $arbChannels['primary'] ?? 'noon') === (string) $rk) ? ' selected' : '';
								?>
							<option value="<?php echo epc_ape_h($rk); ?>"<?php echo $sel; ?>><?php echo epc_ape_h((string) ($rentry['label'] ?? $rk)); ?></option>
							<?php } ?>
						</select>
					</div></div>
					<div class="form-group"><label class="col-sm-4">Arb. min margin %</label><div class="col-sm-8"><input type="number" step="0.1" class="form-control input-sm" name="arb_min_margin_pct" value="<?php echo epc_ape_h((string) ($arbCfg['min_margin_pct'] ?? $arbChannels['min_margin_pct'] ?? $rules['min_margin_percent'] ?? 15)); ?>" /></div></div>
					<div class="form-group"><div class="col-sm-8 col-sm-offset-4">
						<label class="checkbox-inline"><input type="checkbox" name="marketplace_arbitrage_enabled" value="1"<?php echo ($arbEnabled || !array_key_exists('enabled', $arbCfg)) ? ' checked' : ''; ?>> Enable marketplace gap detection</label>
					</div></div>
					<?php } ?>
					<div class="form-group"><div class="col-sm-8 col-sm-offset-4">
						<label class="checkbox-inline"><input type="checkbox" name="auto_update_prices" value="1"<?php echo !empty($rules['auto_update_prices']) ? ' checked' : ''; ?>> Auto-update catalogue prices</label><br>
						<label class="checkbox-inline"><input type="checkbox" name="auto_cross_list" value="1"<?php echo !empty($rules['auto_cross_list']) ? ' checked' : ''; ?>> Auto cross-list (future)</label>
					</div></div>
					<div class="form-group"><label class="col-sm-4">Notes</label><div class="col-sm-8"><textarea class="form-control input-sm" name="notes" rows="2"><?php echo epc_ape_h($rules['notes'] ?? ''); ?></textarea></div></div>
					<div class="form-group"><div class="col-sm-8 col-sm-offset-4"><button type="submit" class="btn btn-primary btn-sm">Save rules</button></div></div>
				</form>
			</div></div>
		</div>
	</div>
	<?php } ?>

	<?php if ($tab === 'compare') { ?>
	<?php if ($arbGapsMatrix) { ?>
	<div class="hpanel"><div class="panel-heading"><h4>Marketplace arbitrage <small>buy from sources · sell on <?php echo epc_ape_h((string) ($arbChannels['primary_label'] ?? 'Noon')); ?> only</small></h4></div><div class="panel-body table-responsive">
		<p class="text-muted" style="font-size:12px">Products on buy sources (Sharaf DG, Jumbo, spare247…) but not on your sell marketplaces. Buy sources are for sourcing only — we never sell on those sites.</p>
		<table class="table table-condensed table-striped epc-ape-matrix">
			<thead><tr>
				<th>Product</th>
				<th>Buy range (sources)</th>
				<th>Marketplace price</th>
				<th>Your price</th>
				<th>Advice</th>
				<th>Margin</th>
				<th></th>
			</tr></thead>
			<tbody>
			<?php foreach ($arbGapsMatrix as $grow) {
				$buyMin = (float) ($grow['buy_price'] ?? 0);
				$buyMax = (float) ($grow['buy_price_max'] ?? 0);
				$mpPrice = (float) ($grow['marketplace_price'] ?? $grow['estimated_sell'] ?? 0);
				$advice = (array) ($grow['pricing_advice'] ?? array());
				$advLevel = (string) ($advice['level'] ?? 'success');
				$advCls = $advLevel === 'danger' ? 'danger' : ($advLevel === 'warning' ? 'warning' : 'success');
				?>
			<tr>
				<td><strong><?php echo epc_ape_h($grow['title']); ?></strong>
					<?php if (!empty($grow['brand'])) { ?><br><small><?php echo epc_ape_h($grow['brand'] . ' ' . ($grow['article_number'] ?? '')); ?></small><?php } ?>
				</td>
				<td class="epc-ape-cell-lowest"><?php
				if ($buyMax > $buyMin) {
					echo number_format($buyMin, 0) . '–' . number_format($buyMax, 0);
				} else {
					echo number_format($buyMin, 2);
				}
				echo ' <small>' . epc_ape_h(implode(' · ', array_slice((array) ($grow['buy_source_labels'] ?? array($grow['buy_source'] ?? '')), 0, 3))) . '</small>';
				?></td>
				<td><?php if ($mpPrice > 0) {
					echo number_format($mpPrice, 0) . ' <small class="text-muted">' . (!empty($grow['marketplace_known']) ? 'listed/benchmark' : 'estimate') . '</small>';
				} else {
					echo '<span class="text-muted">Research needed</span>';
				} ?></td>
				<td><span class="text-muted">—</span></td>
				<td><?php if (!empty($advice['badge'])) { ?><span class="label label-<?php echo epc_ape_h($advCls); ?>"><?php echo epc_ape_h($advice['badge']); ?></span><?php } ?></td>
				<td><strong class="text-success"><?php echo number_format((float) $grow['margin_abs'], 0); ?></strong> (<?php echo number_format((float) $grow['margin_pct'], 0); ?>%)</td>
				<td><a class="btn btn-xs btn-success" href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=discover&view=marketplace_opportunities'); ?>">Discover</a></td>
			</tr>
			<?php } ?>
			</tbody>
		</table>
	</div></div>
	<?php } ?>
	<?php if ($marketCompareMatrix) { ?>
	<div class="hpanel"><div class="panel-heading"><h4>Market confirmed compare <small>buy sources vs marketplace sell price</small></h4></div><div class="panel-body table-responsive">
		<p class="text-muted" style="font-size:12px">Cross-source matched products — buy range from sourcing sites only; sell target from Noon/Amazon/eBay listings.<?php if ($industryKey === 'auto_parts') { ?> Spare247 shown as reference cost when credentialed.<?php } ?></p>
		<table class="table table-condensed table-striped epc-ape-matrix">
			<thead><tr>
				<th><?php echo $industryKey === 'auto_parts' ? 'Brand · Article' : 'Product'; ?></th>
				<th>Buy range (sources)</th>
				<th>Marketplace price</th>
				<th>Your price</th>
				<th>Advice</th>
				<th>Margin</th>
			</tr></thead>
			<tbody>
			<?php foreach ($marketCompareMatrix as $mrow) {
				$range = (array) ($mrow['source_price_range'] ?? array());
				$buyMin = (float) ($range['buy_min'] ?? $range['min'] ?? 0);
				$buyMax = (float) ($range['buy_max'] ?? $range['max'] ?? 0);
				$mpPrice = (float) ($range['target_sell_price'] ?? $range['marketplace_price'] ?? 0);
				$marginAbs = (float) ($range['margin_abs'] ?? 0);
				$advice = (array) ($mrow['pricing_advice'] ?? $range['pricing_advice'] ?? array());
				$advLevel = (string) ($advice['level'] ?? 'info');
				$advCls = $advLevel === 'danger' ? 'danger' : ($advLevel === 'warning' ? 'warning' : ($advLevel === 'success' ? 'success' : 'default'));
				?>
			<tr>
				<td>
					<?php if ($industryKey === 'auto_parts') { ?>
					<strong><?php echo epc_ape_h($mrow['brand'] ?: 'OEM'); ?></strong> · <?php echo epc_ape_h($mrow['article_number'] ?? ''); ?>
					<br><small class="text-muted"><?php echo epc_ape_h($mrow['brand_article_key'] ?? $mrow['identity_key'] ?? ''); ?></small>
					<?php } else { ?>
					<strong><?php echo epc_ape_h($mrow['title'] ?? ''); ?></strong>
					<br><small class="text-muted"><?php echo epc_ape_h($mrow['identity_key'] ?? ''); ?></small>
					<?php } ?>
				</td>
				<td class="epc-ape-cell-lowest"><?php
				if ($buyMin > 0) {
					echo $buyMax > $buyMin ? number_format($buyMin, 2) . '–' . number_format($buyMax, 2) : number_format($buyMin, 2);
				} else {
					echo '—';
				}
				?></td>
				<td class="epc-ape-cell-highest"><?php echo $mpPrice > 0 ? number_format($mpPrice, 2) : '<span class="text-muted">Not listed</span>'; ?></td>
				<td><?php
				$yp = (float) ($mrow['your_price'] ?? 0);
				if ($yp > 0) {
					echo number_format($yp, 2);
				} elseif ((float) ($mrow['your_warehouse_price'] ?? 0) > 0) {
					echo number_format((float) $mrow['your_warehouse_price'], 2) . ' <small>(warehouse)</small>';
				} else {
					echo '—';
				}
				?></td>
				<td><?php if (!empty($advice['badge'])) { ?><span class="label label-<?php echo epc_ape_h($advCls); ?>"><?php echo epc_ape_h($advice['badge']); ?></span><?php } ?></td>
				<td><?php echo $marginAbs > 0 ? '<strong class="text-success">' . number_format($marginAbs, 2) . '</strong>' : '—'; ?></td>
			</tr>
			<?php } ?>
			</tbody>
		</table>
	</div></div>
	<?php } ?>
	<?php if ($compareFilterTaxId > 0) {
		$filterTaxName = '';
		foreach ($flatTax as $ft) {
			if ((int) $ft['id'] === $compareFilterTaxId) {
				$filterTaxName = (string) $ft['name_en'];
				break;
			}
		}
		?>
	<div class="alert alert-info" style="margin-bottom:12px">
		Filtered to product line: <strong><?php echo epc_ape_h($filterTaxName ?: ('#' . $compareFilterTaxId)); ?></strong>
		<a class="btn btn-default btn-xs pull-right" href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&tab=compare'); ?>">Clear filter</a>
	</div>
	<?php } ?>
	<?php if (($tenantCfg['profile'] ?? '') === 'warehouse_supplier') { ?>
	<div class="hpanel"><div class="panel-heading"><h4>Warehouse × price list matrix</h4></div><div class="panel-body table-responsive">
		<table class="table table-condensed table-striped epc-ape-matrix">
			<thead><tr><th>Storage</th><th>Price list</th><th>SKU rows</th><th>Min price</th><th>Max price</th></tr></thead>
			<tbody>
			<?php foreach ($whMatrix as $row) { ?>
			<tr>
				<td><?php echo epc_ape_h($row['storage_name']); ?><?php if (!empty($row['link_source']) && $row['link_source'] !== 'price_id') { ?> <small class="text-muted">(<?php echo epc_ape_h($row['link_source']); ?>)</small><?php } ?></td>
				<td><?php echo epc_ape_h($row['price_list_name'] ?? ('#' . (int) $row['price_list_id'])); ?> <small class="text-muted">#<?php echo (int) $row['price_list_id']; ?></small></td>
				<td><?php echo (int) $row['product_count']; ?></td>
				<td><?php echo number_format((float) $row['min_price'], 2); ?></td>
				<td><?php echo number_format((float) $row['max_price'], 2); ?></td>
			</tr>
			<?php } ?>
			<?php if (!$whMatrix) { ?><tr><td colspan="5" class="text-muted">No warehouse price lists found — upload price lists in Prices manager or run <code>epc_apai_link_warehouse_price_lists</code>.</td></tr><?php } ?>
			</tbody>
		</table>
	</div></div>
	<?php if ($whShowPicker) {
		$compareAjaxUrl = function_exists('epc_apai_ajax_url')
			? epc_apai_ajax_url($__apaiBackendRaw)
			: ('/' . $__apaiBackendRaw . '/control/portal/ajax_auto_price');
		?>
	<div class="hpanel epc-wh-compare" id="epc-wh-compare" data-site-key="<?php echo epc_ape_h($siteKey); ?>">
		<div class="panel-heading"><h4>Your warehouse <small>full price list — pick up to 10 for market compare</small></h4></div>
		<div class="panel-body">
			<p class="text-muted" style="font-size:12px;margin-bottom:10px" id="epc-wh-list-summary">Loading warehouse items…</p>
			<div class="row" style="margin-bottom:10px">
				<div class="col-sm-3">
					<label class="sr-only">Price list</label>
					<select class="form-control input-sm" id="epc-wh-filter-pl">
						<option value="0">All price lists</option>
						<?php foreach ($whPriceListOptions as $plOpt) { ?>
						<option value="<?php echo (int) $plOpt['id']; ?>"><?php echo epc_ape_h($plOpt['label']); ?><?php if ((int) ($plOpt['row_count'] ?? 0) > 0) { ?> (<?php echo (int) $plOpt['row_count']; ?>)<?php } ?></option>
						<?php } ?>
					</select>
				</div>
				<div class="col-sm-4">
					<input type="search" class="form-control input-sm" id="epc-wh-search" placeholder="Search brand or article…" />
				</div>
				<div class="col-sm-5 text-right">
					<button type="button" class="btn btn-default btn-sm" id="epc-wh-reload"><i class="fa fa-refresh"></i> Reload list</button>
				</div>
			</div>
			<div class="table-responsive">
				<table class="table table-condensed table-striped epc-ape-matrix" id="epc-wh-list-table">
					<thead><tr>
						<th style="width:28px"><input type="checkbox" id="epc-wh-check-page" title="Select page" /></th>
						<th>Brand</th>
						<th>Article</th>
						<th>Warehouse</th>
						<th>Cost</th>
						<th>In catalogue?</th>
					</tr></thead>
					<tbody id="epc-wh-list-body"><tr><td colspan="6" class="text-muted text-center">Loading…</td></tr></tbody>
				</table>
			</div>
			<div class="clearfix" style="margin-top:8px">
				<ul class="pagination pagination-sm" id="epc-wh-pagination" style="margin:0"></ul>
				<p class="text-muted pull-right" style="font-size:11px;margin:6px 0 0" id="epc-wh-page-meta"></p>
			</div>
		</div>
	</div>
	<div class="hpanel epc-wh-compare-selected">
		<div class="panel-heading"><h4>Compare selected <small>max 10 with live market</small></h4></div>
		<div class="panel-body">
			<p class="text-muted" style="font-size:12px"><span id="epc-wh-selected-count">0</span> selected — choose up to 10 warehouse rows above.</p>
			<div id="epc-wh-selected-chips" style="margin-bottom:10px"></div>
			<button type="button" class="btn btn-primary btn-sm" id="epc-wh-compare-btn" disabled><i class="fa fa-balance-scale"></i> Compare selected with market</button>
			<div id="epc-wh-compare-progress" style="display:none;margin-top:12px">
				<div class="progress" style="margin-bottom:6px;height:8px"><div class="progress-bar progress-bar-info" id="epc-wh-progress-bar" style="width:0%"></div></div>
				<p class="text-muted" style="font-size:11px;margin:0" id="epc-wh-progress-msg">Starting…</p>
			</div>
			<div class="table-responsive" id="epc-wh-results-wrap" style="display:none;margin-top:12px">
				<table class="table table-condensed table-striped epc-ape-matrix">
					<thead><tr>
						<th>Brand · Article</th>
						<th>Warehouse cost</th>
						<th>Market lowest</th>
						<th>Market highest</th>
						<th>Margin</th>
						<th>Advice</th>
					</tr></thead>
					<tbody id="epc-wh-results-body"></tbody>
				</table>
			</div>
		</div>
	</div>
	<div class="hpanel epc-wh-compare-previous">
		<div class="panel-heading"><h4>Previously compared <small>last 10 cached</small></h4></div>
		<div class="panel-body table-responsive">
			<table class="table table-condensed table-striped epc-ape-matrix">
				<thead><tr>
					<th>Brand · Article</th>
					<th>Warehouse cost</th>
					<th>Market min</th>
					<th>Market max</th>
					<th>Margin</th>
					<th>Badge</th>
				</tr></thead>
				<tbody id="epc-wh-previous-body">
				<?php foreach ($whComparePrevious as $wrow) {
					$badge = (string) ($wrow['badge'] ?? 'no_market_data');
					$badgeCls = $badge === 'good_margin' ? 'success' : ($badge === 'below_market' ? 'info' : ($badge === 'over_market' ? 'danger' : 'default'));
					?>
				<tr>
					<td><strong><?php echo epc_ape_h($wrow['brand'] ?? ''); ?></strong> · <?php echo epc_ape_h($wrow['article_show'] ?? $wrow['article'] ?? ''); ?></td>
					<td><?php echo number_format((float) ($wrow['warehouse_cost'] ?? 0), 2); ?></td>
					<td class="epc-ape-cell-lowest"><?php echo (float) ($wrow['market_min'] ?? 0) > 0 ? number_format((float) $wrow['market_min'], 2) : '—'; ?></td>
					<td class="epc-ape-cell-highest"><?php echo (float) ($wrow['market_max'] ?? 0) > 0 ? number_format((float) $wrow['market_max'], 2) : '—'; ?></td>
					<td><?php echo (float) ($wrow['margin_abs'] ?? 0) != 0.0 ? number_format((float) $wrow['margin_abs'], 2) : '—'; ?></td>
					<td><span class="label label-<?php echo epc_ape_h($badgeCls); ?>"><?php echo epc_ape_h($whBadgeLabels[$badge] ?? $badge); ?></span></td>
				</tr>
				<?php } ?>
				<?php if (!$whComparePrevious) { ?><tr><td colspan="6" class="text-muted">No comparisons yet — select up to 10 warehouse items and click Compare selected with market.</td></tr><?php } ?>
				</tbody>
			</table>
		</div>
	</div>
	<script>window.EPC_APAI_COMPARE=<?php echo json_encode(array(
		'active' => true,
		'ajaxUrl' => $compareAjaxUrl,
		'siteKey' => $siteKey,
		'maxSelected' => 10,
		'perPage' => 50,
		'badgeLabels' => $whBadgeLabels,
	), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;</script>
	<?php } ?>
	<?php if ($industryKey === 'auto_parts' && $importedCompare) { ?>
	<div class="hpanel"><div class="panel-heading"><h4>Parts compare by brand + article <small>grouped by brand_article_key</small></h4></div><div class="panel-body">
		<p class="text-muted" style="font-size:12px">Imported spare parts — matched and grouped by <code>brand:article</code> (e.g. Toyota · 1310154101), not product title.</p>
		<?php
		$compareGroups = array();
		foreach ($importedCompare as $crow) {
			$gk = (string) ($crow['brand_article_key'] ?? '');
			if ($gk === '') {
				$gk = 'product:' . (int) ($crow['product_id'] ?? 0);
			}
			if (!isset($compareGroups[$gk])) {
				$compareGroups[$gk] = array();
			}
			$compareGroups[$gk][] = $crow;
		}
		ksort($compareGroups);
		foreach ($compareGroups as $gkey => $gitems) {
			$head = $gitems[0];
			?>
		<div class="epc-ape-spec-compare epc-ape-spec-compare--parts" style="margin-bottom:18px">
			<div class="epc-disc-part-identity epc-disc-part-identity--compare">
				<?php if (!empty($head['brand']) || !empty($head['article_number'])) { ?>
				<span class="epc-disc-part-identity__brand label label-primary"><?php echo epc_ape_h($head['brand'] ?: 'OEM'); ?></span>
				<span class="epc-disc-part-identity__article"><?php echo epc_ape_h($head['article_number'] ?? ''); ?></span>
				<small class="text-muted"><?php echo epc_ape_h($gkey); ?></small>
				<?php } else { ?>
				<span class="label label-warning">Part number required</span>
				<?php } ?>
			</div>
			<?php foreach ($gitems as $row) {
				$specKeys = array('brand', 'article_number', 'brand_article_key');
				?>
			<h5 style="margin-top:8px"><?php echo epc_ape_h($row['title'] ?: ('Product #' . ($row['product_id'] ?? 0))); ?>
				<?php if (!empty($row['product_id'])) { ?><small>#<?php echo (int) $row['product_id']; ?></small><?php } ?>
			</h5>
			<table class="table table-condensed table-striped epc-ape-matrix">
				<thead><tr><th>Source</th><th>Price</th><?php foreach ($specKeys as $sk) { ?><th><?php echo epc_ape_h($sk); ?></th><?php } ?></tr></thead>
				<tbody>
				<?php
				$rowMin = (float) ($row['min_price'] ?? $row['lowest_price'] ?? 0);
				$rowMax = (float) ($row['max_price'] ?? $row['highest_price'] ?? $rowMin);
				foreach ((array) ($row['sources'] ?? array()) as $src) {
					$srcSpecs = (array) ($src['specs'] ?? array());
					if (function_exists('epc_apai_specs_enrich_brand_article')) {
						$srcSpecs = epc_apai_specs_enrich_brand_article($srcSpecs);
					}
					$srcPrice = (float) ($src['price'] ?? 0);
					$priceCls = '';
					if ($srcPrice > 0 && abs($srcPrice - $rowMin) < 0.01) {
						$priceCls = 'epc-ape-cell-lowest';
					} elseif ($srcPrice > 0 && abs($srcPrice - $rowMax) < 0.01) {
						$priceCls = 'epc-ape-cell-highest';
					}
					?>
				<tr>
					<td><?php echo epc_ape_h((string) ($src['source_domain'] ?? '')); ?></td>
					<td class="<?php echo $priceCls; ?>"><strong><?php echo number_format($srcPrice, 2); ?></strong> <?php echo epc_ape_h($src['currency'] ?? 'AED'); ?></td>
					<?php foreach ($specKeys as $sk) { ?>
					<td><?php echo epc_ape_h((string) ($srcSpecs[$sk] ?? '—')); ?></td>
					<?php } ?>
				</tr>
				<?php } ?>
				<?php if ($rowMin > 0) { ?>
				<tr class="epc-ape-matrix-summary">
					<td><strong>Summary</strong></td>
					<td colspan="<?php echo count($specKeys) + 1; ?>">
						Min <strong class="epc-ape-cell-lowest"><?php echo number_format($rowMin, 2); ?></strong>
						· Max <strong class="epc-ape-cell-highest"><?php echo number_format($rowMax, 2); ?></strong>
						· Margin <strong><?php echo number_format((float) ($row['margin_abs'] ?? 0), 2); ?> (<?php echo number_format((float) ($row['margin_pct'] ?? 0), 1); ?>%)</strong>
					</td>
				</tr>
				<?php } ?>
				</tbody>
			</table>
			<?php } ?>
		</div>
		<?php } ?>
		<?php if (!$importedCompare) { ?><p class="text-muted">No imported parts to compare — discover by brand + article first.</p><?php } ?>
	</div></div>
	<?php } ?>
	<?php } ?>
	<?php if (($tenantCfg['profile'] ?? '') !== 'warehouse_supplier') { ?>
	<div class="hpanel"><div class="panel-heading"><h4>Specification compare matrix <small>sorted by margin % · min margin <?php echo epc_ape_h($rules['min_margin_percent'] ?? 15); ?>%</small></h4></div><div class="panel-body">
		<p class="text-muted" style="font-size:12px">Imported products — compare buy-source prices vs marketplace sell targets. <span class="epc-ape-cell-lowest">Green</span> = lowest buy (cost), <span class="epc-ape-cell-highest">blue</span> = marketplace sell price.</p>
		<?php foreach ($matrix as $row) {
			$conflicts = (array) ($row['spec_conflicts'] ?? array());
			$specKeys = (array) ($row['spec_keys'] ?? array());
			?>
		<div class="epc-ape-spec-compare" style="margin-bottom:18px">
			<h5><?php echo epc_ape_h($row['title'] ?: ('Product #' . ($row['product_id'] ?? 0))); ?>
				<?php if (!empty($row['product_id'])) { ?>
				<small>#<?php echo (int) $row['product_id']; ?></small>
				<?php } ?>
			</h5>
			<?php if ($conflicts) { ?>
			<div class="alert alert-warning" style="padding:8px 12px;font-size:12px;margin-bottom:8px">
				<strong>Spec mismatch:</strong>
				<?php foreach ($conflicts as $c) {
					echo epc_ape_h(($c['key'] ?? '') . ' — ' . implode(' vs ', (array) ($c['values'] ?? array()))) . '; ';
				} ?>
			</div>
			<?php } ?>
			<table class="table table-condensed table-striped epc-ape-matrix">
				<thead><tr><th>Source</th><th>Price</th><?php foreach ($specKeys as $sk) { ?><th><?php echo epc_ape_h($sk); ?></th><?php } ?></tr></thead>
				<tbody>
				<?php
				$rowMin = (float) ($row['min_price'] ?? $row['lowest_price'] ?? 0);
				$rowMax = (float) ($row['max_price'] ?? $row['highest_price'] ?? $rowMin);
				foreach ((array) ($row['sources'] ?? array()) as $src) {
					$srcSpecs = (array) ($src['specs'] ?? array());
					$domain = (string) ($src['source_domain'] ?? $src['source_name'] ?? '');
					$srcPrice = (float) ($src['price'] ?? 0);
					$priceCls = '';
					if ($srcPrice > 0 && abs($srcPrice - $rowMin) < 0.01) {
						$priceCls = 'epc-ape-cell-lowest';
					} elseif ($srcPrice > 0 && abs($srcPrice - $rowMax) < 0.01) {
						$priceCls = 'epc-ape-cell-highest';
					}
					?>
				<tr>
					<td><?php echo epc_ape_h($domain); ?></td>
					<td class="<?php echo $priceCls; ?>"><strong><?php echo number_format($srcPrice, 2); ?></strong> <?php echo epc_ape_h($src['currency'] ?? 'AED'); ?></td>
					<?php foreach ($specKeys as $sk) {
						$val = (string) ($srcSpecs[$sk] ?? '—');
						$warn = false;
						foreach ($conflicts as $c) {
							if (($c['key'] ?? '') === $sk) { $warn = true; break; }
						}
						?>
					<td class="<?php echo $warn ? 'epc-ape-spec-warn' : ''; ?>"><?php echo epc_ape_h($val); ?></td>
					<?php } ?>
				</tr>
				<?php } ?>
				<?php if ($rowMin > 0) { ?>
				<tr class="epc-ape-matrix-summary">
					<td><strong>Min | Max | Margin</strong></td>
					<td colspan="<?php echo count($specKeys) + 1; ?>">
						<span class="epc-ape-cell-lowest"><?php echo number_format($rowMin, 2); ?></span>
						| <span class="epc-ape-cell-highest"><?php echo number_format($rowMax, 2); ?></span>
						| <strong><?php echo number_format((float) ($row['margin_abs'] ?? 0), 2); ?> (<?php echo number_format((float) ($row['margin_pct'] ?? 0), 1); ?>%)</strong>
					</td>
				</tr>
				<?php } ?>
				</tbody>
			</table>
			<?php if (!empty($row['sell_price']) && !empty($row['lowest_price'])) { ?>
			<p style="font-size:12px;margin:0">Our sell: <strong><?php echo number_format((float) $row['sell_price'], 2); ?></strong> AED · Cost (min): <strong class="epc-ape-cell-lowest"><?php echo number_format((float) $row['lowest_price'], 2); ?></strong> · Target (max): <strong class="epc-ape-cell-highest"><?php echo number_format($rowMax, 2); ?></strong>
				<?php if (isset($row['margin_vs_market'])) { ?>
				· Margin vs market: <?php echo epc_ape_h($row['margin_vs_market']); ?>%
				<?php } ?>
			</p>
			<?php } ?>
		</div>
		<?php } ?>
		<?php if (!$matrix) { ?><p class="text-muted">No compare rows — import products from Discover tab or run tenant seed.</p><?php } ?>
	</div></div>
	<?php if (!$importedCompare) { ?>
	<div class="hpanel"><div class="panel-heading"><h4>Legacy price sources matrix</h4></div><div class="panel-body table-responsive">
		<table class="table table-condensed table-striped epc-ape-matrix">
			<thead><tr><th>Product</th><th>Cost</th><th>Lowest</th><th>Margin</th><th>Sources</th></tr></thead>
			<tbody>
			<?php foreach (epc_ape_compare_matrix($pdo, $siteKey) as $row) { ?>
			<tr>
				<td><?php echo epc_ape_h($row['title'] ?: ('#' . $row['product_id'])); ?></td>
				<td><?php echo $row['warehouse_cost'] > 0 ? number_format((float) $row['warehouse_cost'], 2) : '—'; ?></td>
				<td><?php echo $row['lowest_price'] !== null ? number_format((float) $row['lowest_price'], 2) : '—'; ?></td>
				<td><?php echo $row['margin_percent'] !== null ? epc_ape_h($row['margin_percent']) . '%' : '—'; ?></td>
				<td><?php foreach ($row['sources'] as $src) { ?><span class="label label-default"><?php echo epc_ape_h($src['source_name']); ?>: <?php echo number_format((float) $src['price'], 2); ?></span> <?php } ?></td>
			</tr>
			<?php } ?>
			</tbody>
		</table>
	</div></div>
	<?php } ?>
	<?php } ?>

	<div class="hpanel"><div class="panel-heading"><h4>Add compare row / manual price</h4></div><div class="panel-body">
		<form method="post" class="form-inline">
			<input type="hidden" name="epc_ape_action" value="add_compare_row" />
			<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
			<select name="source_id" class="form-control input-sm" required>
				<option value="">Source…</option>
				<?php foreach ($sources as $s) { ?>
				<option value="<?php echo (int) $s['id']; ?>"><?php echo epc_ape_h($s['name']); ?></option>
				<?php } ?>
			</select>
			<input class="form-control input-sm" name="title" placeholder="Title" />
			<input class="form-control input-sm" name="external_sku" placeholder="SKU" />
			<input class="form-control input-sm" name="external_url" placeholder="URL (optional)" style="min-width:180px" />
			<input type="number" step="0.01" class="form-control input-sm" name="last_price" placeholder="Price" />
			<input type="number" step="0.01" class="form-control input-sm" name="warehouse_cost" placeholder="Cost" />
			<button type="submit" class="btn btn-primary btn-sm">Add</button>
		</form>
		<form method="post" style="margin-top:10px">
			<input type="hidden" name="epc_ape_action" value="run_fetch" />
			<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
			<button type="submit" class="btn btn-default btn-sm"><i class="fa fa-refresh"></i> Fetch prices now (og:meta / manual)</button>
		</form>
	</div></div>

	<?php if ($runs) { ?>
	<div class="hpanel"><div class="panel-heading"><h4>Run history</h4></div><div class="panel-body table-responsive">
		<table class="table table-condensed"><thead><tr><th>When</th><th>Trigger</th><th>Summary</th><th>Status</th></tr></thead><tbody>
		<?php foreach ($runs as $r) { ?>
		<tr>
			<td><?php echo date('Y-m-d H:i', (int) ($r['finished_at'] ?? 0)); ?></td>
			<td><?php echo epc_ape_h($r['trigger_type']); ?></td>
			<td><?php echo epc_ape_h($r['summary']); ?></td>
			<td><?php echo epc_ape_h($r['status']); ?></td>
		</tr>
		<?php } ?>
		</tbody></table>
	</div></div>
	<?php } ?>
	<?php } ?>

	<?php if ($tab === 'wizard') { ?>
	<div class="row">
		<div class="col-md-6">
			<div class="hpanel"><div class="panel-heading"><h4>Product copy wizard</h4></div><div class="panel-body">
				<p class="text-muted" style="font-size:12px">Paste marketplace URL — MVP extracts og:title, og:description, og:price. Creates <code>shop_catalogue_products</code> entry + optional source link.</p>
				<form method="post">
					<input type="hidden" name="epc_ape_action" value="product_wizard" />
					<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
					<div class="form-group"><label>Marketplace URL</label><input class="form-control" name="wizard_url" placeholder="https://www.amazon.ae/… or noon.com/…" /></div>
					<div class="form-group"><label>Title (override)</label><input class="form-control" name="wizard_title" /></div>
					<div class="form-group"><label>Sell price</label><input type="number" step="0.01" class="form-control" name="wizard_price" /></div>
					<div class="form-group"><label>Warehouse cost</label><input type="number" step="0.01" class="form-control" name="wizard_cost" /></div>
					<div class="form-group"><label>Link to source</label>
						<select name="wizard_source_id" class="form-control">
							<option value="0">— none —</option>
							<?php foreach ($sources as $s) { ?>
							<option value="<?php echo (int) $s['id']; ?>"><?php echo epc_ape_h($s['name']); ?></option>
							<?php } ?>
						</select>
					</div>
					<button type="submit" class="btn btn-primary"><i class="fa fa-magic"></i> Create product</button>
				</form>
			</div></div>
		</div>
	</div>
	<?php } ?>

	<?php if ($tab === 'listings') { ?>
	<div class="row">
		<div class="col-md-5">
			<div class="hpanel"><div class="panel-heading"><h4>Cross-list action</h4></div><div class="panel-body">
				<form method="post">
					<input type="hidden" name="epc_ape_action" value="cross_list" />
					<input type="hidden" name="site_key" value="<?php echo epc_ape_h($siteKey); ?>" />
					<div class="form-group"><label>Catalogue product ID</label><input type="number" class="form-control" name="product_id" required /></div>
					<div class="form-group"><label>Channel</label>
						<select name="channel_type" class="form-control">
							<?php foreach ($channelTypes as $k => $lbl) { ?>
							<option value="<?php echo epc_ape_h($k); ?>"><?php echo epc_ape_h($lbl); ?></option>
							<?php } ?>
						</select>
					</div>
					<div class="form-group"><label>List price</label><input type="number" step="0.01" class="form-control" name="list_price" required /></div>
					<button type="submit" class="btn btn-primary btn-sm">Create listing draft</button>
				</form>
				<p style="margin-top:12px"><a class="btn btn-default btn-sm" href="<?php echo epc_ape_h($pageBase . '?site_key=' . urlencode($siteKey) . '&export=ebay_csv'); ?>"><i class="fa fa-download"></i> Export eBay CSV</a></p>
			</div></div>
		</div>
		<div class="col-md-7">
			<div class="hpanel"><div class="panel-heading"><h4>Channel listings</h4></div><div class="panel-body table-responsive">
				<table class="table table-condensed table-striped">
					<thead><tr><th>ID</th><th>Product</th><th>Channel</th><th>Price</th><th>Status</th></tr></thead>
					<tbody>
					<?php foreach ($listings as $l) { ?>
					<tr>
						<td><?php echo (int) $l['id']; ?></td>
						<td><?php echo epc_ape_h($l['caption'] ?? ('#' . $l['product_id'])); ?></td>
						<td><code><?php echo epc_ape_h($l['channel_type']); ?></code></td>
						<td><?php echo number_format((float) $l['list_price'], 2); ?> <?php echo epc_ape_h($l['currency']); ?></td>
						<td><?php echo epc_ape_h($l['status']); ?></td>
					</tr>
					<?php } ?>
					<?php if (!$listings) { ?><tr><td colspan="5" class="text-muted">No listings yet.</td></tr><?php } ?>
					</tbody>
				</table>
			</div></div>
		</div>
	</div>
	<?php } ?>
<?php if (!$apaiPartial) { ?>
</div>
<?php } ?>

<?php } /* end !$apaiShellMode || $apaiShellInlineDiscover */ ?>
