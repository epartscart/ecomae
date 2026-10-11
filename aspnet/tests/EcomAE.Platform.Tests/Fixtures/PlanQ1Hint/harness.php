<?php
// PHP 8.3 goldens for plan Q1-hint (EParts CATA config library).
// Real config file is included. Leftover unique portal stays its own gap:
// hostname / auto-parts flags are stubbed before include so that parent is
// not loaded. GET never mints a session. No PDO — DB helpers return empty.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function hint_stub_portal(): void
{
	if (!function_exists('epc_portal_is_epartscart_hostname')) {
		function epc_portal_is_epartscart_hostname(): bool
		{
			$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
			if (strpos($host, ':') !== false) {
				$host = explode(':', $host, 2)[0];
			}
			$host = preg_replace('/^www\./', '', $host);
			return $host === 'epartscart.com';
		}
	}
	if (!function_exists('epc_portal_is_auto_parts_site')) {
		function epc_portal_is_auto_parts_site(): bool
		{
			return !empty($GLOBALS['hint_auto_parts']);
		}
	}
}

function hint_boot(string $root): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$_SERVER['DOCUMENT_ROOT'] = $root;
	if (!isset($_SERVER['REQUEST_METHOD'])) {
		$_SERVER['REQUEST_METHOD'] = 'GET';
	}
	$_SERVER['HTTP_HOST'] = $_SERVER['HTTP_HOST'] ?? 'www.ecomae.com';
	$GLOBALS['hint_auto_parts'] = false;
	$GLOBALS['multilang_params'] = array();
	hint_stub_portal();
	require_once $root . '/content/general_pages/epc_cata_config.php';
}

function hint_cat_digest($rows): array
{
	$names = array();
	$ids = array();
	foreach ((array) $rows as $r) {
		if (!is_array($r)) {
			continue;
		}
		$names[] = (string) ($r['CATEGORY_NAME'] ?? '');
		$ids[] = (int) ($r['STR_ID'] ?? 0);
	}
	$first = null;
	$last = null;
	if ($rows) {
		$vals = array_values((array) $rows);
		$first = $vals[0];
		$last = $vals[count($vals) - 1];
	}
	return array(
		'count' => count($names),
		'names' => $names,
		'ids' => $ids,
		'first' => $first,
		'last' => $last,
	);
}

function hint_monitor_stable(array $mon): array
{
	$keys = array();
	$pct = array();
	$phase = array();
	foreach ((array) ($mon['datasets'] ?? array()) as $row) {
		$keys[] = (string) ($row['key'] ?? '');
		$pct[] = $row['pct'] ?? 0;
		$phase[] = (string) ($row['phase'] ?? '');
	}
	return array(
		'ok' => !empty($mon['ok']) ? 1 : 0,
		'rows' => $mon['rows'] ?? array(),
		'stock_note' => (string) ($mon['stock_note'] ?? ''),
		'dataset_keys' => $keys,
		'dataset_pct' => $pct,
		'dataset_phase' => $phase,
	);
}

function hint_run_empty(): array
{
	$providers = epc_cata_providers();
	$labels = array();
	foreach ($providers as $id => $meta) {
		$labels[$id] = (string) ($meta['label'] ?? '');
	}
	return array(
		epc_cata_provider_ids(),
		$labels,
		epc_cata_normalize_provider('local'),
		epc_cata_normalize_provider('tecdoc'),
		epc_cata_normalize_provider('eparts'),
		epc_cata_normalize_provider('partsapi'),
		epc_cata_normalize_provider('umapi'),
		epc_cata_normalize_provider('nope'),
		epc_cata_normalize_provider('Local-TecDoc'),
		epc_cata_normalize_provider('LOCAL'),
		epc_cata_tables(),
		EPC_CATA_VERSION,
		epc_cata_settings_table(),
		epc_cata_route_url(),
		array_keys(epc_cata_catalog_targets()),
		epc_cata_sanitize_category_name('', 4),
		epc_cata_sanitize_category_name('???', 7),
		epc_cata_sanitize_category_name('Filters', 2),
		epc_cata_sanitize_category_name('?', 0),
		epc_cata_storefront_slug('Brake System'),
		epc_cata_storefront_slug('Fuel Mixture Formation'),
		epc_cata_storefront_slug('  Air  Conditioning '),
		epc_cata_normalize_vin(''),
		epc_cata_normalize_vin('WBA12345678'),
		epc_cata_normalize_vin('wba12345678'),
		epc_cata_normalize_vin('WBA-123 456789'),
		epc_cata_normalize_vin('SHORT'),
		epc_cata_ensure_tables() ? 1 : 0,
		epc_cata_settings_ensure() ? 1 : 0,
		epc_cata_setting_set('k', 'v') ? 1 : 0,
		epc_cata_local_catalog_pdo() === null ? 1 : 0,
		epc_cata_db() === null ? 1 : 0,
		epc_cata_legacy_browse_counts(),
		epc_cata_legacy_vin_counts(),
		epc_cata_presentation_defaults()['default_section'],
		epc_cata_presentation_defaults()['default_article_view'],
		epc_cata_providers_enabled_map(),
		epc_cata_category_config_setting_key(),
		epc_cata_normalize_catalog_target('eparts-cata'),
		epc_cata_normalize_catalog_target('EPARTS-CATA'),
		epc_cata_normalize_catalog_target('nope'),
	);
}

function hint_run_gates(): array
{
	$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
	$GLOBALS['hint_auto_parts'] = false;
	$eparts = epc_cata_enabled_for_request() ? 1 : 0;
	$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
	$other = epc_cata_enabled_for_request() ? 1 : 0;
	$GLOBALS['hint_auto_parts'] = true;
	$auto = epc_cata_enabled_for_request() ? 1 : 0;
	$GLOBALS['hint_auto_parts'] = false;
	return array(
		$eparts,
		$other,
		$auto,
		epc_cata_provider_enabled('') ? 1 : 0,
		epc_cata_provider_enabled('nope') ? 1 : 0,
		epc_cata_provider_enabled('umapi') ? 1 : 0,
		epc_cata_should_use_default_category_tree('') ? 1 : 0,
		epc_cata_should_use_default_category_tree('local_tecdoc') ? 1 : 0,
		epc_cata_should_use_default_category_tree('umapi') ? 1 : 0,
		epc_cata_should_use_default_category_tree('tecdoc') ? 1 : 0,
		epc_cata_provider_origin('umapi'),
		epc_cata_provider_origin('local'),
		epc_cata_provider_origin('eparts'),
		epc_cata_provider_origin('carcat'),
		epc_cata_provider_origin('laximo'),
		epc_cata_provider_origin('levam'),
		epc_cata_provider_origin('nope'),
	);
}

function hint_run_live(): array
{
	$defaults = epc_cata_default_categories();
	$cached = epc_cata_default_categories_cached();
	$store = epc_cata_storefront_categories();
	$filters = epc_cata_storefront_resolve_category('Filters');
	$byId = epc_cata_storefront_resolve_category('2');
	$bySlug = epc_cata_storefront_resolve_category('brake-system');
	$missing = epc_cata_storefront_resolve_category('nope-group');
	$hrefEn = epc_cata_storefront_category_href('/en', $store[0] ?? array());
	$hrefAr = epc_cata_storefront_category_href('/ar/', $store[3] ?? array(), 'Pads');
	$searchRow = array('STR_ID' => 2, 'CATEGORY_NAME' => 'Filters', 'catalog_target' => 'parts-search', 'tecdoc_pt_id' => 99);
	$hrefSearch = epc_cata_storefront_category_href('', $searchRow, 'Oil Filter');
	$payloadNested = epc_cata_payload_data(array('data' => array(array('id' => 1), array('id' => 2))));
	$payloadList = epc_cata_payload_data(array(array('a' => 1), array('a' => 2)));
	$payloadEmpty = epc_cata_payload_data(array());
	$modEmpty = epc_cata_modification_from_row(array());
	$modTitle = epc_cata_modification_from_row(array('title' => '1.6 TDI', 'ext_id' => 44, 'source' => 'umapi', 'section' => 'passenger'));
	$modRaw = epc_cata_modification_from_row(array(
		'raw_json' => json_encode(array('PC_ID' => 88, 'MODIFICATION' => '2.0 TDI', 'POWER_KW' => '110')),
		'source' => 'umapi',
	));
	$normRows = epc_cata_normalize_category_rows(array(
		array('STR_ID' => 9, 'CATEGORY_NAME' => '???'),
		array('STR_ID' => 2, 'CATEGORY_NAME' => 'Filters', 'ORDER' => 1),
		'bad',
		array('CATEGORY_NAME' => '', 'STR_ID' => 0),
	));
	$entry = epc_cata_category_config_normalize_entry(array('STR_ID' => 2, 'display_name' => 'Oil Filters', 'catalog_target' => 'eparts-cata'), 3);
	$ajax = epc_cata_category_config_for_ajax();
	$jsLight = epc_cata_presentation_js_config_light();
	$jsFull = epc_cata_presentation_js_config();
	$td = epc_cata_tecdoc_config();
	$local = epc_cata_local_catalog_config();
	$shell = epc_cata_cp_status_shell();
	$cachedStatus = epc_cata_cp_status_cached();
	$mon = hint_monitor_stable(epc_cata_cp_import_monitor());
	return array(
		hint_cat_digest($defaults),
		hint_cat_digest($cached),
		hint_cat_digest($store),
		(string) (($store[0]['catalog_target'] ?? '')),
		$filters !== null ? 1 : 0,
		$byId !== null ? 1 : 0,
		$bySlug !== null ? 1 : 0,
		$missing === null ? 1 : 0,
		(string) (($filters['category']['CATEGORY_NAME'] ?? '')),
		(string) (($bySlug['category']['CATEGORY_NAME'] ?? '')),
		$hrefEn,
		$hrefAr,
		$hrefSearch,
		count($payloadNested),
		count($payloadList),
		count($payloadEmpty),
		$modEmpty['title'],
		$modTitle['title'],
		$modTitle['ext_id'],
		$modRaw['title'],
		$modRaw['PC_ID'],
		$modRaw['power_kw'],
		hint_cat_digest($normRows),
		$entry['CATEGORY_NAME'] ?? '',
		$entry['catalog_target'] ?? '',
		$entry['ORDER'] ?? 0,
		count($ajax['categories'] ?? array()),
		$ajax['customized'] ? 1 : 0,
		$ajax['storefront_count'] ?? 0,
		$ajax['icon_base'] ?? '',
		$jsLight['version'] ?? '',
		!empty($jsLight['categoriesDeferred']) ? 1 : 0,
		count($jsLight['categories'] ?? array()),
		$jsFull['version'] ?? '',
		!empty($jsFull['categoriesDeferred']) ? 1 : 0,
		(string) ($td['data_root'] ?? ''),
		(int) ($td['min_year'] ?? 0),
		(string) ($td['table_prefix'] ?? ''),
		!empty($td['stream_mode']) ? 1 : 0,
		(string) ($local['table_prefix'] ?? ''),
		(string) ($local['host'] ?? ''),
		!empty($shell['ok']) ? 1 : 0,
		(string) ($shell['version'] ?? ''),
		(int) (($shell['totals']['articles'] ?? -1)),
		!empty($cachedStatus['ok']) ? 1 : 0,
		(string) ($cachedStatus['error'] ?? ''),
		$mon,
		substr(epc_cata_default_categories_json_path(), -strlen('/content/general_pages/epc_cata_default_categories.json')) === '/content/general_pages/epc_cata_default_categories.json' ? 1 : 0,
		substr(epc_cata_default_subcategories_json_path(), -strlen('/content/general_pages/epc_cata_default_subcategories.json')) === '/content/general_pages/epc_cata_default_subcategories.json' ? 1 : 0,
		substr(epc_cata_category_icon_dir(), -strlen('/content/files/epc-cata/category-icons/')) === '/content/files/epc-cata/category-icons/' ? 1 : 0,
		count(epc_cata_category_icons_available()),
		epc_cata_category_config_raw() === null ? 1 : 0,
		epc_cata_category_config_reset() ? 1 : 0,
		epc_cata_category_config_save_bulk(array()) ? 1 : 0,
		epc_cata_category_config_save_one(array('STR_ID' => 2)) ? 1 : 0,
		epc_cata_file_config(),
	);
}

function hint_run_tenant(): array
{
	$GLOBALS['multilang_params'] = array();
	$none = epc_cata_storefront_url();
	$GLOBALS['multilang_params'] = array('lang_href' => '/ar/');
	$ar = epc_cata_storefront_url();
	$GLOBALS['multilang_params'] = array('lang_href' => '/en');
	$en = epc_cata_storefront_url();
	$GLOBALS['multilang_params'] = array('lang_href' => '/ar/');
	$stillAr = epc_cata_storefront_url();
	$GLOBALS['multilang_params'] = array('lang_href' => '/en/');
	$backEn = epc_cata_storefront_url();

	$tmp = sys_get_temp_dir() . '/plan_q1hint_' . getmypid();
	@mkdir($tmp, 0777, true);
	file_put_contents($tmp . '/config.epc-cata.php', "<?php\nreturn array('enabled' => 0);\n");
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$_SERVER['HTTP_HOST'] = 'shop.example';
	$GLOBALS['hint_auto_parts'] = true;
	// file_config is static — only this process, and it was already called
	// in live/empty? No: this is a fresh subprocess. First file_config call.
	$disabled = epc_cata_enabled_for_request() ? 1 : 0;
	$file = epc_cata_file_config();
	$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
	$epartsStill = epc_cata_enabled_for_request() ? 1 : 0;
	@unlink($tmp . '/config.epc-cata.php');
	@rmdir($tmp);
	return array(
		$none,
		$ar,
		$en,
		$stillAr,
		$backEn,
		$none === $ar ? 1 : 0,
		$ar === $en ? 1 : 0,
		$stillAr === $ar ? 1 : 0,
		$disabled,
		isset($file['enabled']) ? (int) $file['enabled'] : -1,
		$epartsStill,
	);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	hint_boot($root);
	$fn = 'hint_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		exit(1);
	}
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1hint_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1hint_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
