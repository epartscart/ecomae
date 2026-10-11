<?php
// PHP 8.3 goldens for plan Q1-starboard (full CP brochure). Marketing + live parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function star_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once __DIR__ . '/epc_marketing_brochure.php';", '// leftover marketing brochure stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/epc_cp_brochure_live.php';", '// leftover live inventory stubbed', $code);
	$code = str_replace("\techo epc_cp_full_brochure_render_html(\$opts);\n\texit;", "\techo epc_cp_full_brochure_render_html(\$opts);\n\treturn;", $code);
	file_put_contents($dest, $code);
}

function star_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_brochure_h')) {
		eval('function epc_brochure_h($v) { return htmlspecialchars((string) $v, ENT_QUOTES, "UTF-8"); }');
		eval('function epc_brochure_profile($brand) { $brand = preg_replace("/[^a-z0-9_]/", "", strtolower((string) $brand)); if ($brand === "ecomae") { return array("id"=>"ecomae","name"=>"ECOM AE","legal"=>"Electronic World Group","url"=>"https://www.ecomae.com","cp_url"=>"https://www.ecomae.com/cp","cover"=>"/c/ecomae.jpg","contact_email"=>"hello@ecomae.com"); } return array("id"=>"epartscart","name"=>"eParts Cart","legal"=>"Electronic World Group","url"=>"https://www.epartscart.com","cp_url"=>"https://www.epartscart.com/cp","cover"=>"/c/parts.jpg","contact_email"=>"hello@epartscart.com"); }');
		eval('function epc_brochure_css($p) { return "BASECSS:" . (string) ($p["id"] ?? ""); }');
	}
	if (!function_exists('epc_cp_brochure_build_live_inventory')) {
		eval('function epc_cp_brochure_build_live_inventory() { return (array) ($GLOBALS["STAR_LIVE"] ?? array("areas"=>array(),"meta"=>array("generated_at"=>1700000000,"sources"=>array("curated inventory"),"total"=>0,"area_count"=>0))); }');
		eval('function epc_cp_brochure_area_visuals() { return (array) ($GLOBALS["STAR_VIS"] ?? array()); }');
		eval('function epc_cp_brochure_item_photo_meta($item, $area) { return array("photo"=>"/p/".($item["name"] ?? "x").".jpg","label"=>"Ops","topic"=>"default"); }');
		eval('function epc_cp_brochure_item_image($item, $area) { return "/i/".($item["name"] ?? "x").".jpg"; }');
	}
}

function star_seed(): void
{
	$GLOBALS['STAR_LIVE'] = array(
		'areas' => array(
			'Shop / OMS' => array(
				array('name' => 'Order desk', 'does' => 'Run orders', 'url' => '/cp/orders', 'scope' => 'client', 'icon' => 'fa-inbox'),
				array('name' => 'Epc leftover', 'does' => 'skip', 'url' => '/cp/orders', 'scope' => 'client', 'icon' => 'fa-cube'),
				array('name' => 'Fleet host', 'does' => 'Host tenants', 'url' => '', 'scope' => 'super', 'icon' => 'fa-cloud'),
				array('name' => 'Shared desk', 'does' => 'Both sides', 'url' => '/cp/shared', 'scope' => 'both', 'icon' => 'fa-users'),
			),
			'Empty Area' => array(),
		),
		'meta' => array(
			'generated_at' => 1700000000,
			'sources' => array('curated inventory', 'capability append'),
			'total' => 4,
			'area_count' => 1,
		),
	);
	$GLOBALS['STAR_VIS'] = array(
		'Shop / OMS' => array('icon' => 'fa-shopping-cart', 'image' => '/v/orders.png', 'blurb' => 'Daily desk'),
	);
}

function star_include(): void
{
	include_once $GLOBALS['STAR_PAGE'];
}

function star_run_dedupe()
{
	star_include();
	$items = array(
		array('name' => 'Epc leftover', 'does' => 'skip', 'url' => '/cp/orders'),
		array('name' => 'Order desk', 'does' => 'Run orders', 'url' => '/cp/orders'),
		array('name' => 'No url', 'does' => 'plain', 'url' => ''),
		array('name' => 'Zed', 'does' => 'Open from left CP menu.', 'url' => '/cp/zed'),
	);
	$out = epc_cp_brochure_dedupe_items($items);
	$names = array();
	foreach ($out as $row) {
		$names[] = $row['name'];
	}
	return array(count($out), $names);
}

function star_run_filter()
{
	star_seed();
	star_include();
	$load = epc_cp_brochure_load_inventory();
	$client = epc_cp_brochure_filtered_bundle('CLIENT!');
	$super = epc_cp_brochure_filtered_bundle('super');
	$all = epc_cp_brochure_filtered_bundle('nope');
	$inv = epc_cp_brochure_filtered_inventory('all');
	$clientAreas = array_keys($client['areas']);
	$clientNames = array();
	foreach ($client['areas']['Shop / OMS'] as $row) {
		$clientNames[] = $row['name'];
	}
	$superNames = array();
	foreach ($super['areas']['Shop / OMS'] as $row) {
		$superNames[] = $row['name'];
	}
	return array(
		count($load['Shop / OMS']),
		$client['meta']['scope'],
		$client['meta']['total'],
		$clientAreas,
		$clientNames,
		$super['meta']['total'],
		$superNames,
		$all['meta']['scope'],
		count($inv['Shop / OMS']),
		isset($client['areas']['Empty Area']) ? 1 : 0,
	);
}

function star_run_css()
{
	star_include();
	$css = epc_cp_brochure_css(epc_brochure_profile('ecomae'));
	return array(strlen($css), substr($css, 0, 24), strpos($css, '.epc-br--deck') !== false ? 1 : 0);
}

function star_run_html()
{
	star_seed();
	star_include();
	$deck = epc_cp_full_brochure_render_html(array('brand' => 'ecomae', 'scope' => 'client', 'view' => 'deck'));
	$cat = epc_cp_full_brochure_render_html(array('brand' => 'Eparts-Cart!', 'scope' => 'all', 'view' => 'catalog'));
	$print = epc_cp_full_brochure_render_html(array('brand' => 'ecomae', 'print' => true));
	$print0 = epc_cp_full_brochure_render_html(array('brand' => 'ecomae', 'print' => '0'));
	return array(
		strlen($deck),
		substr($deck, 0, 80),
		(strpos($deck, 'ECOM AE') !== false) ? 1 : 0,
		(strpos($deck, '14 Nov 2023') !== false) ? 1 : 0,
		(strpos($deck, 'Order desk') !== false) ? 1 : 0,
		(strpos($deck, 'Fleet host') !== false) ? 1 : 0,
		(strpos($deck, 'scope=client&view=deck') !== false) ? 1 : 0,
		strlen($cat),
		(strpos($cat, 'eParts Cart') !== false) ? 1 : 0,
		(strpos($cat, 'Catalogue view') !== false) ? 1 : 0,
		(strpos($cat, 'Fleet host') !== false) ? 1 : 0,
		(strpos($print, 'window.print') !== false) ? 1 : 0,
		(strpos($print0, 'setTimeout') !== false) ? 1 : 0,
	);
}

function star_run_exit()
{
	star_seed();
	star_include();
	ob_start();
	epc_cp_full_brochure_render_and_exit(array('brand' => 'ecomae', 'scope' => 'client'));
	$html = (string) ob_get_clean();
	return array(strlen($html), substr($html, 0, 40), (strpos($html, 'ECOM AE') !== false) ? 1 : 0);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_star_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	star_patch($root . '/content/general_pages/epc_cp_full_brochure.php', $tmp . '/full.php');
	$GLOBALS['STAR_PAGE'] = $tmp . '/full.php';
	star_boot();
	$fn = 'star_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/full.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1star_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1star_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
