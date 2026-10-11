<?php
// PHP 8.3 goldens for plan Q1-port (CP brochure live). Leftover topic-photo / dump / ERP-nav parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function port_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace(
		"\t\$items = array();\n\t\$navFile = (isset(\$_SERVER['DOCUMENT_ROOT']) ? rtrim((string) \$_SERVER['DOCUMENT_ROOT'], '/') : dirname(__DIR__, 2))\n\t\t. '/cp/content/shop/finance/erp/erp_nav_areas.php';\n\tif (!is_file(\$navFile)) {\n\t\t\$navFile = dirname(__DIR__, 2) . '/cp/content/shop/finance/erp/erp_nav_areas.php';\n\t}\n\tif (!is_file(\$navFile)) {\n\t\treturn \$items;\n\t}\n\trequire_once \$navFile;\n\tif (!function_exists('epc_erp_nav_areas_config')) {\n\t\treturn \$items;\n\t}",
		"\t\$items = array();\n\tif (!function_exists('epc_erp_nav_areas_config')) {\n\t\treturn \$items;\n\t}",
		$code
	);
	$code = str_replace(
		"\t\$path = __DIR__ . '/epc_ecomae_platform_capabilities_catalog.php';\n\t\$caps = is_file(\$path) ? require \$path : array();",
		"\t\$caps = function_exists('port_caps_load') ? port_caps_load() : array();",
		$code
	);
	$code = str_replace(
		"\t\$path = __DIR__ . '/epc_cp_brochure_inventory.php';\n\t\$raw = is_file(\$path) ? require \$path : array();",
		"\t\$raw = function_exists('port_inventory_load') ? port_inventory_load() : array();",
		$code
	);
	file_put_contents($dest, $code);
}

function port_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
}

function port_caps_load(): array
{
	return (array) ($GLOBALS['PORT_CAPS'] ?? array());
}

function port_inventory_load(): array
{
	return (array) ($GLOBALS['PORT_INV'] ?? array());
}

function port_seed_caps(): void
{
	$GLOBALS['PORT_CAPS'] = array(
		array('id' => 'cap-orders', 'category' => 'Commerce — Orders & fulfilment', 'title' => 'Order desk', 'summary' => 'A longer summary that should replace short does', 'icon' => 'fa-inbox'),
		array('id' => 'cap-new', 'category' => 'Finance & ERP', 'title' => 'Treasury pulse', 'summary' => 'Cash position', 'icon' => 'fa-bank'),
		array('id' => '', 'category' => 'Payments', 'title' => '', 'summary' => 'skip empty title', 'icon' => 'fa-x'),
		array('id' => 'cap-plat', 'category' => 'Platform & Super CP', 'title' => 'Fleet host', 'summary' => 'Host tenants', 'icon' => 'fa-cloud'),
	);
}

function port_seed_inv(): void
{
	$GLOBALS['PORT_INV'] = array(
		'Shop / OMS' => array(
			array('name' => 'Order desk', 'does' => 'short', 'url' => '/cp/orders', 'scope' => 'client'),
			array('name' => 'Epc leftover', 'does' => 'real work', 'url' => '/cp/stub', 'scope' => 'client'),
			array('name' => 'Real desk', 'does' => 'Open from left CP menu.', 'url' => '/cp/menu', 'scope' => 'client'),
			array('name' => 'Zed last', 'does' => 'sort me', 'url' => '/cp/zed', 'scope' => 'client'),
		),
		'Empty Area' => array(),
		'ERP / Modules' => array(
			array('name' => 'Old module', 'does' => 'stale', 'url' => '/old', 'scope' => 'client'),
		),
	);
}

function port_seed_erp(): void
{
	if (!function_exists('epc_erp_nav_areas_config')) {
		eval('function epc_erp_nav_areas_config() { return (array) ($GLOBALS["PORT_ERP"] ?? array()); }');
	}
	$GLOBALS['PORT_ERP'] = array(
		'gl' => array(
			'label' => 'General ledger',
			'icon' => 'fa-book',
			'desc' => 'Books',
			'tabs' => array(
				'journals' => array('label' => 'Journals', 'icon' => 'fa-list'),
				'skip' => 'not-array',
			),
		),
		'cash' => array('label' => 'Cash', 'icon' => 'fa-money', 'desc' => ''),
	);
}

function port_include(): void
{
	include_once $GLOBALS['PORT_PAGE'];
}

function port_run_norm()
{
	port_include();
	return array(
		epc_cp_brochure_norm_key('Order Desk!!'),
		epc_cp_brochure_norm_key('  A   B  '),
		epc_cp_brochure_norm_key(''),
		epc_cp_brochure_cap_category_to_area('Payments'),
		epc_cp_brochure_cap_category_to_area('Unknown Cat'),
		epc_cp_brochure_cap_category_to_area('Finance & ERP'),
	);
}

function port_run_image()
{
	port_include();
	$item = array('name' => 'Brake pad', 'icon' => 'fa-stop');
	$img = epc_cp_brochure_item_image($item, 'Shop / OMS');
	$meta = epc_cp_brochure_item_photo_meta($item, 'Shop / OMS');
	$empty = epc_cp_brochure_item_image(array(), 'Portal');
	return array($img, $meta['topic'], $meta['label'], $meta['photo'], $empty);
}

function port_run_pool()
{
	if (!function_exists('epc_cp_brochure_topic_svg_url')) {
		eval('function epc_cp_brochure_topic_svg_url($topic, $id, $title, $kind) { return "/svg/".$topic."/".$id; }');
	}
	port_include();
	$pool = epc_cp_brochure_screen_pool();
	$vis = epc_cp_brochure_area_visuals();
	$keys = array_keys($vis);
	return array(
		count($pool),
		$pool[0],
		$pool[6],
		count($vis),
		$keys[0],
		$vis['Super CP / Platform']['image'],
		$vis['Super CP / Platform']['icon'],
		$vis['Portal']['image'],
	);
}

function port_run_visuals()
{
	if (!function_exists('epc_cp_brochure_topic_catalog')) {
		eval('function epc_cp_brochure_topic_catalog() { return array("platform"=>array("photos"=>array("/cat/platform.jpg")),"default"=>array("photos"=>array("/cat/default.jpg"))); }');
	}
	port_include();
	$vis = epc_cp_brochure_area_visuals();
	return array(
		$vis['Super CP / Platform']['image'],
		$vis['Prices & Catalogue']['image'],
		$vis['Portal']['blurb'],
	);
}

function port_run_caps()
{
	port_seed_caps();
	port_include();
	$caps = epc_cp_brochure_capabilities_catalog();
	$idx = epc_cp_brochure_capability_index();
	$keys = array_keys($idx);
	return array(
		count($caps),
		count($idx),
		$keys,
		$idx['order desk']['icon'],
		$idx['order desk']['id'],
		isset($idx['']) ? 1 : 0,
	);
}

function port_run_erp()
{
	port_include();
	$empty = epc_cp_brochure_erp_nav_items();
	port_seed_erp();
	$items = epc_cp_brochure_erp_nav_items();
	return array(
		count($empty),
		count($items),
		$items[0]['name'],
		$items[0]['does'],
		$items[0]['url'],
		$items[0]['id'],
		$items[1]['name'],
		$items[1]['does'],
		$items[1]['url'],
		$items[1]['id'],
	);
}

function port_run_live()
{
	if (!function_exists('epc_cp_brochure_assign_unique_photos')) {
		eval('function epc_cp_brochure_assign_unique_photos($out) { return array("Order desk"=>"/uniq/desk.jpg"); }');
	}
	port_seed_caps();
	port_seed_inv();
	port_seed_erp();
	port_include();
	// time() is live; patch generated_at after by using a known clock via... we cannot.
	// Capture then overwrite generated_at in the returned snapshot by calling time once.
	$now = time();
	$inv = epc_cp_brochure_build_live_inventory();
	$areas = array_keys($inv['areas']);
	$shop = $inv['areas']['Shop / OMS'];
	$erp = $inv['areas']['ERP / Modules'];
	$fin = $inv['areas']['ERP / Finance'];
	$plat = $inv['areas']['Super CP / Platform'];
	return array(
		$areas,
		$inv['meta']['total'],
		$inv['meta']['area_count'],
		$inv['meta']['generated_at'] >= $now - 2 && $inv['meta']['generated_at'] <= $now + 2 ? 1700000000 : $inv['meta']['generated_at'],
		$inv['meta']['sources'],
		$shop[0]['name'],
		$shop[0]['does'],
		$shop[0]['icon'],
		$shop[0]['id'],
		$shop[1]['name'],
		$shop[1]['icon'],
		$shop[1]['id'],
		$erp[0]['name'],
		$erp[0]['url'],
		$erp[1]['name'],
		$fin[0]['name'],
		$fin[0]['scope'],
		$plat[0]['name'],
		$plat[0]['scope'],
		isset($inv['areas']['Empty Area']) ? 1 : 0,
	);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_port_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	port_patch($root . '/content/general_pages/epc_cp_brochure_live.php', $tmp . '/live.php');
	$GLOBALS['PORT_PAGE'] = $tmp . '/live.php';
	$_SERVER['DOCUMENT_ROOT'] = $root;
	port_boot();
	$fn = 'port_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/live.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1port_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1port_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
