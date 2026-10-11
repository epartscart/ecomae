<?php
// PHP 8.3 goldens for plan Q1-swell (multi-vendor price ingest). History/import/extra/ACL parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function swell_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function swell_patch_commerce(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once __DIR__ . '/docpart_price_upload_history.php';", '// history parent stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/epc_price_import_helpers.php';", '// import helpers stubbed', $code);
	file_put_contents($dest, $code);
}

function swell_patch_page(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once __DIR__ . '/docpart_price_upload_history.php';", '// history parent stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/epc_price_import_helpers.php';", '// import helpers stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/epc_commerce_price_ingest.php';", '// commerce included separately', $code);
	$code = str_replace("require_once __DIR__ . '/epc_multivendor_min_price_acl.php';", '// ACL constants injected', $code);
	$code = str_replace("require_once __DIR__ . '/epc_price_extra_fields.php';", '// extra parent stubbed', $code);
	$code = preg_replace(
		"/if \\(is_file\\(__DIR__ \\. '\\/docpart_article_match\\.php'\\)\\) \\{\\s*require_once __DIR__ \\. '\\/docpart_article_match\\.php';\\s*\\}/",
		'// article match not included',
		$code
	);
	file_put_contents($dest, $code);
}

function swell_stubs(): void
{
	if (!defined('EPC_MV_MIN_TIER')) {
		define('EPC_MV_MIN_TIER', 'epc_mv_min');
	}
	if (!defined('EPC_MV_MAX_TIER')) {
		define('EPC_MV_MAX_TIER', 'epc_mv_max');
	}
	if (!function_exists('epc_parse_stock_quantity')) {
		function epc_parse_stock_quantity($raw): int
		{
			$raw = trim((string) $raw);
			if ($raw === '') {
				return 0;
			}
			$compact = str_replace(array(' ', "\xc2\xa0"), '', $raw);
			if (preg_match('/^-?\d+[.,]\d+$/', $compact)) {
				$parts = preg_split('/[.,]/', $compact);
				$compact = (string) ($parts[0] ?? '0');
			}
			$digits = preg_replace('/[^0-9]/', '', $compact);
			if ($digits === '') {
				return 0;
			}
			$qty = (int) $digits;
			if ($qty > 999999) {
				$qty = 999999;
			}
			return $qty;
		}
	}
	if (!function_exists('epc_price_link_storage_to_list')) {
		function epc_price_link_storage_to_list($db, $listName, $priceId)
		{
			return true;
		}
	}
	if (!function_exists('epc_price_resolve_or_create_list')) {
		function epc_price_resolve_or_create_list($db, $id, $listName)
		{
			$q = $db->prepare('SELECT * FROM `shop_docpart_prices` WHERE `name` = ? LIMIT 1');
			$q->execute(array($listName));
			$row = $q->fetch(PDO::FETCH_ASSOC);
			if ($row) {
				return $row;
			}
			$db->prepare('INSERT INTO `shop_docpart_prices` (`name`) VALUES (?)')->execute(array($listName));
			$newId = (int) $db->lastInsertId();
			$q = $db->prepare('SELECT * FROM `shop_docpart_prices` WHERE `id` = ?');
			$q->execute(array($newId));
			return $q->fetch(PDO::FETCH_ASSOC);
		}
	}
	if (!function_exists('epc_price_history_archive_file')) {
		function epc_price_history_archive_file($path, $priceId, $name)
		{
			return 'archive/' . $name;
		}
	}
	if (!function_exists('epc_price_history_save')) {
		function epc_price_history_save($db, $arr)
		{
			return 7;
		}
	}
	if (!function_exists('epc_price_history_count_brands')) {
		function epc_price_history_count_brands($db, $priceId)
		{
			return 1;
		}
	}
	if (!function_exists('epc_price_history_set_active')) {
		function epc_price_history_set_active($db, $priceId, $historyId)
		{
		}
	}
	if (!function_exists('epc_price_extra_map_header_columns')) {
		function epc_price_extra_map_header_columns($headerRow, $coreMap)
		{
			return array();
		}
	}
	if (!function_exists('epc_price_extra_extract_from_row')) {
		function epc_price_extra_extract_from_row($raw, $extraColMap)
		{
			return array();
		}
	}
	if (!function_exists('epc_price_extra_merge')) {
		function epc_price_extra_merge($a, $b)
		{
			return $a;
		}
	}
	if (!function_exists('epc_price_extra_encode')) {
		function epc_price_extra_encode($extras)
		{
			return '';
		}
	}
	if (!function_exists('epc_price_extra_decode')) {
		function epc_price_extra_decode($raw)
		{
			return array();
		}
	}
	if (!function_exists('epc_price_extra_ensure_schema')) {
		function epc_price_extra_ensure_schema($db)
		{
			return true;
		}
	}
	if (!function_exists('epc_price_extra_clear_for_price')) {
		function epc_price_extra_clear_for_price($db, $priceId)
		{
		}
	}
	if (!function_exists('epc_price_extra_save_for_row')) {
		function epc_price_extra_save_for_row($db, $priceDataId, $priceId, $extras, $manufacturer, $article, $articleShow, $name)
		{
		}
	}
}

function swell_include(): void
{
	swell_stubs();
	include_once $GLOBALS['SWELL_COMMERCE'];
	include_once $GLOBALS['SWELL_PAGE'];
}

function swell_schema(PDO $db): void
{
	$db->exec('CREATE TABLE shop_docpart_prices (
		id INT PRIMARY KEY AUTO_INCREMENT,
		name VARCHAR(128),
		link VARCHAR(500) DEFAULT \'\',
		load_mode INT DEFAULT 0,
		file_name_substring VARCHAR(128) DEFAULT \'\',
		message_header_substring VARCHAR(500) DEFAULT \'\',
		last_updated INT DEFAULT 0,
		records_count INT DEFAULT 0
	)');
	$db->exec('CREATE TABLE shop_docpart_prices_data (
		id INT PRIMARY KEY,
		price_id INT,
		manufacturer VARCHAR(128),
		article VARCHAR(64),
		article_show VARCHAR(64),
		name VARCHAR(255),
		`exist` INT,
		price DECIMAL(12,2),
		time_to_exe INT,
		storage VARCHAR(64),
		min_order INT
	)');
	$db->exec('CREATE TABLE shop_storages (
		id INT PRIMARY KEY AUTO_INCREMENT,
		name VARCHAR(128),
		interface_type INT,
		users TEXT,
		connection_options TEXT,
		currency INT,
		short_name VARCHAR(128),
		hidden INT,
		bg_line_color INT
	)');
	$db->exec('CREATE TABLE shop_offices (id INT PRIMARY KEY AUTO_INCREMENT)');
	$db->exec('INSERT INTO shop_offices (id) VALUES (1)');
	$db->exec('CREATE TABLE shop_offices_storages_map (
		office_id INT, storage_id INT, group_id INT, min_point INT, max_point INT, markup INT, additional_time INT
	)');
	$db->exec('CREATE TABLE users (id INT PRIMARY KEY AUTO_INCREMENT, user_type INT)');
	$db->exec('INSERT INTO users (id, user_type) VALUES (3, 2)');
}

function swell_collapse_view(array $rows): array
{
	$out = array();
	foreach ($rows as $row) {
		$out[] = array(
			'name' => (string) ($row['name'] ?? ''),
			'exist' => (int) ($row['exist'] ?? 0),
			'price' => (float) ($row['price'] ?? 0),
			'storage' => (string) ($row['storage'] ?? ''),
			'tier' => (string) ($row['epc_price_tier'] ?? ''),
		);
	}
	return $out;
}

function swell_run_names()
{
	swell_include();
	$aliases = epc_multivendor_header_aliases();
	$map = epc_multivendor_map_headers(array(
		'Part Number', 'Brand', 'Qty', 'Sales Price', 'Name',
		'Vendor full name', 'Vendor short', 'Data type', 'Delivery', 'Min order',
	));
	$emptyMap = epc_multivendor_map_headers(array('', 'unknown'));
	$subMap = epc_multivendor_map_headers(array(
		'sku code', 'vendor company legal', 'wh code', 'selling amount', 'item title', 'price type',
	));
	$inv = epc_multivendor_collapse_product_candidates(array(
		array('name' => 'Oil', 'exist' => 2, 'price' => 12.5, 'extras' => array()),
		array('name' => 'Oil filter long', 'exist' => 3, 'price' => 11, 'extras' => array()),
		array('name' => 'X', 'exist' => 1, 'price' => 0, 'extras' => array()),
	), 'inventory');
	$sales = epc_multivendor_collapse_product_candidates(array(
		array('name' => 'FILTER', 'exist' => 12, 'price' => 18.0, 'extras' => array()),
		array('name' => 'FILTER', 'exist' => 5, 'price' => 22.5, 'extras' => array()),
		array('name' => 'FILTER', 'exist' => 2, 'price' => 29.9, 'extras' => array()),
	), 'sales');
	$same = epc_multivendor_collapse_product_candidates(array(
		array('name' => 'A', 'exist' => 2, 'price' => 10, 'extras' => array()),
		array('name' => 'B', 'exist' => 3, 'price' => 10, 'extras' => array()),
	), 'sales');
	$portal = epc_vendor_portal_sample_csv();
	$sample = epc_multivendor_sample_csv();
	return array(
		array_keys($aliases),
		$aliases['vendor_short'][0],
		epc_multivendor_normalize_data_type('INV'),
		epc_multivendor_normalize_data_type('sale'),
		epc_multivendor_normalize_data_type('buying'),
		epc_multivendor_normalize_data_type('stock'),
		epc_multivendor_normalize_data_type('default', 'sales'),
		epc_multivendor_normalize_data_type('', 'purchase'),
		epc_multivendor_normalize_data_type('weird', 'nope'),
		epc_multivendor_is_combine_mode('combine') ? 1 : 0,
		epc_multivendor_is_combine_mode('from file') ? 1 : 0,
		epc_multivendor_is_combine_mode('inventory') ? 1 : 0,
		epc_multivendor_resolve_data_type_mode(''),
		epc_multivendor_resolve_data_type_mode('mixed'),
		epc_multivendor_resolve_data_type_mode('sales'),
		epc_multivendor_data_type_list_suffix('sales'),
		epc_multivendor_data_type_list_suffix('purchase'),
		epc_multivendor_data_type_list_suffix('inventory'),
		epc_multivendor_sanitize_short(" S/UAE#'\"\\  "),
		epc_multivendor_sanitize_full("  S-UAE   Trading  "),
		epc_multivendor_vendor_key('Acme Trading', 's-uae'),
		epc_multivendor_vendor_key('only-code'),
		epc_multivendor_vendor_key('', ''),
		epc_multivendor_list_base_name('S-UAE', 'S-UAE Trading LLC'),
		epc_multivendor_list_base_name('S-UAE', 's-uae'),
		epc_multivendor_list_base_name('', 'Full Only'),
		epc_multivendor_list_base_name('', ''),
		$map,
		$emptyMap,
		$subMap,
		swell_collapse_view($inv),
		swell_collapse_view($sales),
		swell_collapse_view($same),
		substr($portal, 0, 40),
		substr_count($portal, "\n"),
		substr($sample, 0, 50),
		substr_count($sample, "\n"),
		epc_commerce_excel_to_csv('/tmp/missing.csv')['message'],
		epc_commerce_excel_to_csv('/tmp/missing.pdf')['ok'] ? 1 : 0,
	);
}

function swell_run_rows()
{
	swell_include();
	$dir = sys_get_temp_dir() . '/ecomae_swell_rows_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($dir, 0777, true);
	$ok = $dir . '/ok.csv';
	$semi = $dir . '/semi.csv';
	$noArt = $dir . '/noart.csv';
	$noPrice = $dir . '/noprice.csv';
	$noShort = $dir . '/noshort.csv';
	$noFull = $dir . '/nofull.csv';
	$noType = $dir . '/notype.csv';
	file_put_contents($ok, "Brand,Article,Name,Qty,Price,Vendor full name,Vendor short,Data type,Delivery\nTOYOTA,446610010,PAD KIT,8,103.51,S-UAE Trading LLC,S-UAE,inventory,0\nDENSO,0671007450,FILTER,12,18.00,S-UAE Trading LLC,S-UAE,sales,0\nDENSO,0671007450,FILTER,5,22.50,S-UAE Trading LLC,S-UAE,sales,0\nDENSO,0671007450,FILTER,2,29.90,S-UAE Trading LLC,S-UAE,sales,0\nBOSCH,F026400039,FILTER,4,15.00,Gulf Parts Trading,S-UAE,inventory,0\n,SKIP,x,1,9,S-UAE Trading LLC,S-UAE,inventory,0\n");
	file_put_contents($semi, "sku;price;vendor_short;vendor_full;data_type;qty\nOC47;10;S-UAE;Acme;inventory;4\n");
	file_put_contents($noArt, "Name,Qty,Price,Vendor full name,Vendor short,Data type\nfoo,1,2,Acme,AC,inventory\n");
	file_put_contents($noPrice, "Brand,Article,Vendor full name,Vendor short,Data type\nBosch,OC47,Acme,AC,inventory\n");
	file_put_contents($noShort, "Brand,Article,Price,Vendor full name,Data type\nBosch,OC47,10,Acme,inventory\n");
	file_put_contents($noFull, "Brand,Article,Price,Vendor short,Data type\nBosch,OC47,10,AC,inventory\n");
	file_put_contents($noType, "Brand,Article,Price,Vendor full name,Vendor short\nBosch,OC47,10,Acme,AC\n");
	$delim = epc_commerce_detect_delimiter($semi);
	$badArt = epc_multivendor_read_source_rows($noArt, 'combine');
	$badPrice = epc_multivendor_read_source_rows($noPrice, 'combine');
	$badShort = epc_multivendor_read_source_rows($noShort, 'combine');
	$badFull = epc_multivendor_read_source_rows($noFull, 'combine');
	$badType = epc_multivendor_read_source_rows($noType, 'combine');
	$okRead = epc_multivendor_read_source_rows($ok, 'combine');
	$invOnly = epc_multivendor_read_source_rows($ok, 'inventory');
	$groups = epc_multivendor_group_by_vendor($okRead['rows']);
	$groupView = array();
	foreach ($groups as $g) {
		$prods = array();
		foreach ($g['products'] as $p) {
			$prods[] = array(
				'article' => (string) ($p['article'] ?? ''),
				'name' => (string) ($p['name'] ?? ''),
				'exist' => (int) ($p['exist'] ?? 0),
				'price' => (float) ($p['price'] ?? 0),
				'storage' => (string) ($p['storage'] ?? ''),
				'tier' => (string) ($p['epc_price_tier'] ?? ''),
				'vendor_short' => (string) ($p['vendor_short'] ?? ''),
			);
		}
		$groupView[] = array(
			'vendor_full' => (string) $g['vendor_full'],
			'vendor_short' => (string) $g['vendor_short'],
			'data_type' => (string) $g['data_type'],
			'n' => count($prods),
			'products' => $prods,
		);
	}
	$csvPath = $dir . '/out.csv';
	$firstGroup = reset($groups);
	$wrote = epc_multivendor_write_docpart_csv($csvPath, $firstGroup['products'] ?? array()) ? 1 : 0;
	$csv = is_file($csvPath) ? file_get_contents($csvPath) : '';
	foreach (array($ok, $semi, $noArt, $noPrice, $noShort, $noFull, $noType, $csvPath) as $f) {
		@unlink($f);
	}
	@rmdir($dir);
	return array(
		$delim,
		$badArt['ok'] ? 1 : 0,
		$badArt['message'],
		$badPrice['ok'] ? 1 : 0,
		$badPrice['message'],
		$badShort['ok'] ? 1 : 0,
		$badShort['message'],
		$badFull['ok'] ? 1 : 0,
		$badFull['message'],
		$badType['ok'] ? 1 : 0,
		$badType['message'],
		$okRead['ok'] ? 1 : 0,
		count($okRead['rows']),
		$okRead['rows'][0]['article'],
		$okRead['rows'][0]['vendor_short'],
		$okRead['rows'][0]['data_type'],
		(int) ($okRead['rows_skipped'] ?? 0),
		$okRead['mode'],
		$invOnly['ok'] ? 1 : 0,
		count($invOnly['rows']),
		count($groups),
		$groupView,
		$wrote,
		$csv,
	);
}

function swell_run_import()
{
	swell_include();
	list($user, $pass, $host, $port) = swell_dsn();
	$schema = 'ecomae_cpw_swell_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
		swell_schema($db);
		$dir = sys_get_temp_dir() . '/ecomae_swell_imp_' . substr(md5(uniqid('', true)), 0, 8);
		@mkdir($dir, 0777, true);
		$csv = $dir . '/doc.csv';
		epc_multivendor_write_docpart_csv($csv, array(
			array(
				'manufacturer' => 'Bosch',
				'article' => 'OC47',
				'article_show' => 'OC-47',
				'name' => 'Oil filter',
				'exist' => 2,
				'price' => 12.5,
				'time_to_exe' => 0,
				'min_order' => 0,
				'storage' => EPC_MV_MAX_TIER,
				'epc_price_tier' => 'max',
			),
			array(
				'manufacturer' => 'X',
				'article' => '',
				'article_show' => '',
				'name' => 'skip',
				'exist' => 1,
				'price' => 0,
			),
		));
		$price = epc_price_resolve_or_create_list($db, 0, 'S-UAE · S-UAE Trading LLC');
		$local = epc_multivendor_import_csv_local($db, $price, $csv);
		$sid = epc_multivendor_ensure_warehouse($db, 'S-UAE Trading LLC', 'S-UAE', (int) $price['id'], true);
		$codes = epc_multivendor_vendor_codes_list($db);
		$badId = epc_multivendor_vendor_code_save($db, 0, 'XX');
		$empty = epc_multivendor_vendor_code_save($db, $sid, '');
		$okSave = epc_multivendor_vendor_code_save($db, $sid, 'S-UAE2', 'S-UAE Trading LLC');
		@unlink($csv);
		@rmdir($dir);
		return array(
			$local['status'] ? 1 : 0,
			$local['records_handled'],
			$local['rows_skipped'],
			(int) ($local['extras_saved'] ?? 0),
			$sid > 0 ? 1 : 0,
			count($codes),
			$codes[0]['vendor_code'] ?? '',
			$codes[0]['vendor_full'] ?? '',
			!empty($codes[0]['is_multivendor']) ? 1 : 0,
			$badId['ok'] ? 1 : 0,
			$badId['message'],
			$empty['ok'] ? 1 : 0,
			$empty['message'],
			$okSave['ok'] ? 1 : 0,
			$okSave['vendor']['vendor_code'] ?? '',
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function swell_run_ingest()
{
	swell_include();
	list($user, $pass, $host, $port) = swell_dsn();
	$schema = 'ecomae_cpw_swell_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
		swell_schema($db);
		$dir = sys_get_temp_dir() . '/ecomae_swell_ing_' . substr(md5(uniqid('', true)), 0, 8);
		@mkdir($dir, 0777, true);
		$src = $dir . '/src.csv';
		file_put_contents($src, "Brand,Article,Name,Qty,Price,Vendor full name,Vendor short,Data type\nTOYOTA,446610010,PAD KIT,8,103.51,S-UAE Trading LLC,S-UAE,inventory\nDENSO,0671007450,FILTER,12,18.00,S-UAE Trading LLC,S-UAE,sales\nDENSO,0671007450,FILTER,5,22.50,S-UAE Trading LLC,S-UAE,sales\nDENSO,0671007450,FILTER,2,29.90,S-UAE Trading LLC,S-UAE,sales\nBOSCH,F026400039,FILTER,4,15.00,Gulf Parts Trading,S-UAE,inventory\n");
		$bad = epc_multivendor_ingest_file($db, $src, 'src.csv', 'nope');
		$ok = epc_multivendor_ingest_file($db, $src, 'src.csv', 'combine');
		$portalOther = epc_multivendor_ingest_for_vendor($db, $src, 'src.csv', 'S-UAE Trading LLC', 'OTHER', 'inventory', 3);
		$portalOk = epc_multivendor_ingest_for_vendor($db, $src, 'src.csv', 'S-UAE Trading LLC', 'S-UAE', 'inventory', 3);
		@unlink($src);
		@rmdir($dir);
		$vendors = array();
		foreach ($ok['vendors'] as $item) {
			$vendors[] = array(
				'status' => !empty($item['status']) ? 1 : 0,
				'price_name' => (string) ($item['price_name'] ?? ''),
				'data_type' => (string) ($item['data_type'] ?? ''),
				'vendor_short' => (string) ($item['vendor_short'] ?? ''),
				'records_handled' => (int) ($item['records_handled'] ?? 0),
				'records_in_db' => (int) ($item['records_in_db'] ?? 0),
				'history_id' => (int) ($item['history_id'] ?? 0),
				'storage_id' => (int) ($item['storage_id'] ?? 0) > 0 ? 1 : 0,
			);
		}
		return array(
			$bad['status'] ? 1 : 0,
			$bad['message'],
			$ok['status'] ? 1 : 0,
			$ok['message'],
			$ok['data_type_default'],
			$ok['vendors_total'],
			$ok['vendors_ok'],
			$ok['vendors_failed'],
			$ok['rows_source'],
			$ok['rows_imported'],
			$ok['warehouses_linked'],
			$vendors,
			$portalOther['status'] ? 1 : 0,
			$portalOther['message'],
			(int) ($portalOther['rows_rejected_other_vendor'] ?? 0),
			$portalOk['status'] ? 1 : 0,
			$portalOk['message'],
			$portalOk['vendor_short'],
			$portalOk['rows_imported'],
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_swell_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	swell_patch_commerce($root . '/content/shop/docpart/epc_commerce_price_ingest.php', $tmp . '/commerce.php');
	swell_patch_page($root . '/content/shop/docpart/epc_multivendor_price_ingest.php', $tmp . '/page.php');
	$GLOBALS['SWELL_COMMERCE'] = $tmp . '/commerce.php';
	$GLOBALS['SWELL_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $root;
	$fn = 'swell_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/commerce.php');
	@unlink($tmp . '/page.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1swell_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1swell_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
