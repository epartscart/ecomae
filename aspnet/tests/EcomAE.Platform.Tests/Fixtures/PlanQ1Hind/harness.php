<?php
// PHP 8.3 goldens for plan Q1-hind (EParts Mod catalog page).
// Real catalog file is included. Do not stub hostname / auto-parts:
// the tenant parent already defines those and the leftover parent
// would redeclare them. GET never mints a session.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function hind_boot(string $root, string $host = 'www.ecomae.com', string $lang = '/en'): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$_SERVER['DOCUMENT_ROOT'] = $root;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_SERVER['HTTP_HOST'] = $host;
	$GLOBALS['multilang_params'] = array('lang_href' => $lang);
	$GLOBALS['DP_Config'] = (object) array(
		'chpu_search_config' => array(
			'chpu_search_on' => 0,
			'level_1' => array('url' => 'parts'),
			'level_2' => array('mode_1' => array('url' => 'brands')),
			'slash_code' => '%2F',
		),
	);
	// leftover unique portal settings load requires config.php; PDO then
	// fails closed to defaults. Unlink the stub when the child exits.
	$cfg = $root . '/config.php';
	if (!is_file($cfg)) {
		file_put_contents($cfg, "<?php\nclass DP_Config {\n\tpublic \$host = '127.0.0.1';\n\tpublic \$db = 'hind_no_db';\n\tpublic \$user = 'nobody';\n\tpublic \$password = 'x';\n}\n");
		register_shutdown_function(static function () use ($cfg): void {
			if (is_file($cfg) && str_contains((string) @file_get_contents($cfg), 'hind_no_db')) {
				@unlink($cfg);
			}
		});
	}
}

function hind_render(): string
{
	$root = $_SERVER['DOCUMENT_ROOT'];
	$multilang_params = $GLOBALS['multilang_params'];
	$DP_Config = $GLOBALS['DP_Config'];
	ob_start();
	require $root . '/content/eparts_mod_catalog.php';
	return (string) ob_get_clean();
}

function hind_page_flags(string $html): array
{
	return array(
		'denied' => (strpos($html, 'EParts Mod is not available on this site.') !== false) ? 1 : 0,
		'root' => (strpos($html, 'id="epc-em-root"') !== false) ? 1 : 0,
		'cart' => (strpos($html, '/shop/cart') !== false) ? 1 : 0,
		'help' => (strpos($html, '/contacts') !== false) ? 1 : 0,
		'key_alert' => (strpos($html, 'config.epc-partsapi.php') !== false) ? 1 : 0,
		'vin' => (strpos($html, 'id="epc-em-vin"') !== false) ? 1 : 0,
		'plate' => (strpos($html, 'id="epc-em-plate"') !== false) ? 1 : 0,
		'version' => (strpos($html, '0.5.26-home-critical-bundle') !== false) ? 1 : 0,
		'session' => (strpos($html, 'PHPSESSID') !== false) ? 1 : 0,
		'prefix' => substr($html, 0, 80),
	);
}

function hind_text($v): string
{
	return (string) ($v === null ? '' : $v);
}

function hind_esc($v): string
{
	return strtr(hind_text($v), array('&' => '&amp;', '<' => '&lt;', '>' => '&gt;', '"' => '&quot;', "'" => '&#039;'));
}

function hind_api_items($d): array
{
	if (is_array($d) && array_is_list($d)) {
		return $d;
	}
	if (is_array($d) && isset($d['data']) && is_array($d['data'])) {
		return $d['data'];
	}
	return array();
}

function hind_is_sub_err($e): int
{
	if (!is_array($e)) {
		return 0;
	}
	return (!empty($e['subscription_required']) || (($e['http_status'] ?? 0) === 401) || (($e['http_status'] ?? 0) === 403)) ? 1 : 0;
}

function hind_norm_article($article): string
{
	return strtoupper(preg_replace('/[\s\-_`\/\'"\\\\.,#]/', '', hind_text($article)));
}

function hind_part_search_url(string $lang, bool $chpu, string $parts, string $brands, string $slash, string $brand, string $article): string
{
	$a = hind_norm_article($article);
	$b = trim(hind_text($brand));
	if ($a === '') {
		return $lang . '/shop/part_search';
	}
	if ($chpu) {
		if ($b === '') {
			return $lang . '/' . $parts . '/' . $brands . '/' . rawurlencode($a);
		}
		return $lang . '/' . $parts . '/' . rawurlencode(str_replace('/', $slash, $b)) . '/' . rawurlencode($a);
	}
	$u = $lang . '/shop/part_search?article=' . rawurlencode($a);
	if ($b !== '') {
		$u .= '&brend=' . rawurlencode($b);
	}
	return $u;
}

function hind_product_url(string $lang, string $brand, string $article): string
{
	$a = hind_norm_article($article);
	$slug = preg_replace('/[^A-Z0-9]/', '', strtoupper(hind_text($brand)));
	if ($slug === '' || $a === '') {
		return $lang . '/eparts-product';
	}
	return $lang . '/product/' . rawurlencode($slug) . '/' . rawurlencode($a);
}

function hind_filter_sub(array $rows, string $subName): array
{
	if ($subName === '') {
		return $rows;
	}
	$term = strtolower($subName);
	$filtered = array();
	foreach ($rows as $row) {
		$group = strtolower(hind_text($row['PRODUCT_GROUP'] ?? ($row['name'] ?? ($row['ART_PRODUCT_NAME'] ?? ($row['partname'] ?? '')))));
		if (strpos($group, $term) !== false || strpos($term, $group) !== false) {
			$filtered[] = $row;
		}
	}
	return $filtered ? $filtered : $rows;
}

function hind_cats_with_sub(array $cats, array $map): array
{
	$out = array();
	foreach ($cats as $c) {
		$sid = (string) ($c['STR_ID'] ?? ($c['CATEGORY_ID'] ?? ($c['id'] ?? '')));
		if (empty($c['children']) && !empty($map[$sid])) {
			$c['children'] = $map[$sid];
		}
		$out[] = $c;
	}
	return $out;
}

function hind_shop_url_for($action, $err, array $methodShops, array $actionShops, string $fallback): string
{
	if (is_array($err) && !empty($err['shop_url'])) {
		return (string) $err['shop_url'];
	}
	if (is_array($err) && !empty($err['method']) && isset($methodShops[$err['method']]['shop_url'])) {
		return (string) $methodShops[$err['method']]['shop_url'];
	}
	if ($action && isset($actionShops[$action]['shop_url'])) {
		return (string) $actionShops[$action]['shop_url'];
	}
	return $fallback;
}

function hind_sub_msg($e, $action, array $actionShops): string
{
	if (is_array($e) && !empty($e['error'])) {
		return (string) $e['error'];
	}
	$meta = $actionShops[$action] ?? null;
	if (is_array($meta)) {
		return 'An EParts API subscription is required for ' . $meta['label'] . ' (' . $meta['method'] . '). Add the key to config.epc-partsapi.php under method_keys after subscribing.';
	}
	return 'An EParts API subscription is required for vehicle models (getModels). Add the method key to config.epc-partsapi.php (method_keys) after subscribing.';
}

function hind_run_empty(): array
{
	$methodShops = array('getModels' => array('shop_url' => 'https://partsapi.ru/account/shop?method=getModels'));
	$actionShops = array('models' => array('method' => 'getModels', 'label' => 'Vehicle models', 'shop_url' => 'https://partsapi.ru/account/shop?method=getModels'));
	$rows = array(
		array('PRODUCT_GROUP' => 'Oil Filter', 'name' => 'Oil Filter'),
		array('PRODUCT_GROUP' => 'Brake Pad Set', 'name' => 'Brake Pad Set'),
	);
	$cats = array(array('STR_ID' => 2, 'CATEGORY_NAME' => 'Filters'));
	$map = array('2' => array(array('name' => 'Air')));
	$attached = hind_cats_with_sub($cats, $map);
	return array(
		hind_text(null),
		hind_text(0),
		hind_esc('A & B <x> "q" \'z\''),
		count(hind_api_items(array(1, 2))),
		count(hind_api_items(array('data' => array('a', 'b', 'c')))),
		count(hind_api_items(array('nope' => 1))),
		hind_is_sub_err(array('subscription_required' => true)),
		hind_is_sub_err(array('http_status' => 401)),
		hind_is_sub_err(array('http_status' => 200)),
		hind_part_search_url('/en', false, 'parts', 'brands', '%2F', 'VAG', '1J0-971 972'),
		hind_part_search_url('/en', false, 'parts', 'brands', '%2F', '', ''),
		hind_part_search_url('/ar', true, 'parts', 'brands', '%2F', 'VAG/OEM', '1J0971972'),
		hind_product_url('/en', 'MANN-FILTER', 'W712/95'),
		hind_product_url('/en', '', 'abc'),
		hind_part_search_url('/en', false, 'parts', 'brands', '%2F', 'BMW', '11427566327') === '/en/shop/cart' ? 1 : 0,
		count(hind_filter_sub($rows, 'oil')),
		count(hind_filter_sub($rows, 'missing-group')),
		count($attached[0]['children'] ?? array()),
		hind_shop_url_for('models', null, $methodShops, $actionShops, 'https://partsapi.ru/account/shop'),
		hind_shop_url_for('models', array('shop_url' => 'https://example/shop'), $methodShops, $actionShops, 'https://partsapi.ru/account/shop'),
		hind_sub_msg(array('error' => 'Need key'), 'models', $actionShops),
		hind_sub_msg(null, 'models', $actionShops),
	);
}

function hind_run_gates(): array
{
	hind_boot($GLOBALS['hind_root'], 'www.ecomae.com', '/en');
	$denied = hind_render();
	hind_boot($GLOBALS['hind_root'], 'www.epartscart.com', '/en');
	$ok = hind_render();
	return array(
		hind_page_flags($denied),
		hind_page_flags($ok),
		(strpos($ok, 'data-lang-href="/en"') !== false) ? 1 : 0,
		(strpos($denied, 'epc-em-root') !== false) ? 1 : 0,
	);
}

function hind_run_live(): array
{
	hind_boot($GLOBALS['hind_root'], 'www.epartscart.com', '/en');
	$html = hind_render();
	$demo = array(
		'ok' => true,
		'demo' => true,
		'manufacturer_count' => 12,
		'category_count' => 0,
		'article_count' => 5,
		'manufacturers' => array('BMW', 'MERCEDES-BENZ', 'VW'),
	);
	$banner = '<div class="epc-vc-message" style="margin-bottom:12px;">Category <strong>' . hind_esc('Filters') . '</strong> selected. Choose make, model and engine to browse parts.</div>';
	return array(
		hind_page_flags($html),
		(strpos($html, 'EParts API key not configured on server') !== false) ? 1 : 0,
		(strpos($html, 'Select a vehicle') !== false) ? 1 : 0,
		(strpos($html, '/content/general_pages/epc_vc_catalog.css?v=') !== false) ? 1 : 0,
		$demo['manufacturer_count'],
		$demo['article_count'],
		$banner,
		(strpos($html, 'Set-Cookie') !== false) ? 1 : 0,
	);
}

function hind_run_tenant(): array
{
	hind_boot($GLOBALS['hind_root'], 'www.epartscart.com', '/ar/');
	$ar = hind_render();
	hind_boot($GLOBALS['hind_root'], 'www.epartscart.com', '/en');
	$en = hind_render();
	return array(
		hind_page_flags($ar),
		hind_page_flags($en),
		(strpos($ar, 'data-lang-href="/ar"') !== false) ? 1 : 0,
		(strpos($en, 'data-lang-href="/en"') !== false) ? 1 : 0,
		(strpos($ar, '/ar/shop/cart') !== false) ? 1 : 0,
		(strpos($en, '/en/shop/cart') !== false) ? 1 : 0,
		(strpos($ar, '/en/shop/cart') !== false) ? 1 : 0,
		(strpos($en, '/ar/shop/cart') !== false) ? 1 : 0,
		(strpos($ar, '/ar/contacts') !== false) ? 1 : 0,
		(strpos($en, '/en/contacts') !== false) ? 1 : 0,
	);
}

$GLOBALS['hind_root'] = $root;
if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$fn = 'hind_run_' . $case['name'];
	try {
		if ($case['name'] === 'empty') {
			hind_boot($root, 'www.ecomae.com', '/en');
		}
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1hind_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1hind_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
