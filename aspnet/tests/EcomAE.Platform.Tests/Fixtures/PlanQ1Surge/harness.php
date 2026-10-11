<?php
// PHP 8.3 goldens for plan Q1-surge (commerce price ingest). History/import parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function surge_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function surge_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once __DIR__ . '/docpart_price_upload_history.php';", '// history parent stubbed', $code);
	$code = str_replace("require_once __DIR__ . '/epc_price_import_helpers.php';", '// import helpers stubbed', $code);
	file_put_contents($dest, $code);
}

function surge_stubs(): void
{
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
}

function surge_include(): void
{
	surge_stubs();
	include_once $GLOBALS['SURGE_PAGE'];
}

function surge_schema(PDO $db): void
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

function surge_run_names()
{
	surge_include();
	$aliases = epc_commerce_header_aliases();
	$map = epc_commerce_map_headers(array('Part Number', 'Brand', 'Qty', 'Sales Price', 'Cost Price', 'Vendor'));
	$emptyMap = epc_commerce_map_headers(array('', 'unknown'));
	$enc = epc_commerce_meta_encode('sales', 'ACME', 12.5, 'ACME-S');
	$dec = epc_commerce_meta_decode($enc);
	return array(
		$aliases['manufacturer'],
		epc_commerce_normalize_header_cell("  Part\tNumber  "),
		epc_commerce_normalize_header_cell("SALES PRICE"),
		$map,
		$emptyMap,
		epc_commerce_parse_number(''),
		epc_commerce_parse_number('1.234,56'),
		epc_commerce_parse_number('12.50'),
		epc_commerce_parse_number('AED 8'),
		epc_commerce_parse_number('1.234.56'),
		epc_commerce_normalize_article('oc-47 / a'),
		epc_commerce_normalize_article("oc`47\n"),
		epc_commerce_clip("café/#x", 4),
		epc_commerce_clip("a/'\"\\#", 20),
		epc_commerce_role_suffix('sales'),
		epc_commerce_role_suffix('p'),
		epc_commerce_role_suffix('local'),
		epc_commerce_role_suffix('nope'),
		epc_commerce_list_name('sales', ''),
		epc_commerce_list_name('sales', 'ACME-S'),
		epc_commerce_list_name('purchase', 'ACME', 'Bosch Parts'),
		epc_commerce_list_name('purchase', 'ACME', 'Mann.P'),
		epc_commerce_list_name('inventory', 'ACME-L'),
		epc_commerce_list_name('weird', 'ACME'),
		epc_commerce_role_from_list_name('ACME-S'),
		epc_commerce_role_from_list_name('Mann.P'),
		epc_commerce_role_from_list_name('ACME-L'),
		epc_commerce_role_from_list_name('plain'),
		epc_commerce_base_from_list_name('ACME-S'),
		epc_commerce_base_from_list_name('Mann.P'),
		epc_commerce_base_from_list_name('ACME-L'),
		substr($enc, 0, 13),
		$dec['role'],
		$dec['base'],
		$dec['margin'],
		$dec['list'],
		epc_commerce_meta_decode('nope'),
		epc_commerce_meta_decode(''),
		epc_commerce_normalize_source_url('https://drive.google.com/file/d/abc123/view'),
		epc_commerce_normalize_source_url('https://drive.google.com/open?id=abc123'),
		epc_commerce_normalize_source_url('https://docs.google.com/spreadsheets/d/sheet9/edit'),
		epc_commerce_normalize_source_url('https://www.dropbox.com/s/x/file.csv?dl=0'),
		epc_commerce_normalize_source_url('https://www.dropbox.com/s/x/file.csv'),
		epc_commerce_normalize_source_url('https://example.com/a.csv'),
		epc_commerce_excel_to_csv('/tmp/missing.csv')['message'],
		epc_commerce_excel_to_csv('/tmp/missing.pdf')['ok'] ? 1 : 0,
	);
}

function surge_run_rows()
{
	surge_include();
	$dir = sys_get_temp_dir() . '/ecomae_surge_rows_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($dir, 0777, true);
	$sales = $dir . '/sales.csv';
	$semi = $dir . '/semi.csv';
	$noArt = $dir . '/noart.csv';
	$noPrice = $dir . '/noprice.csv';
	$purchase = $dir . '/purchase.csv';
	file_put_contents($sales, "Part Number,Brand,Qty,Sales Price,Name\nOC-47,Bosch,2,12.5,Oil filter\nOC-47,Bosch,1,11,Oil filter cheap\n,Skip,1,9,Empty article\nHU712,Mann,3,0,Zero\nHU712,Mann,1,8.25,Filter\n");
	file_put_contents($semi, "sku;cost;supplier;qty\nOC47;10;Febi;4\nOC47;9;Febi;2\nOC47;12;Valeo;1\n");
	file_put_contents($noArt, "name,qty,price\nfoo,1,2\n");
	file_put_contents($noPrice, "sku,name\nOC47,Filter\n");
	file_put_contents($purchase, "article,cost,supplier,exist\nOC-47,10,Febi,4\nOC-47,9,Febi,2\nOC-47,12,Valeo,1\n");
	$delim = epc_commerce_detect_delimiter($semi);
	$bad = epc_commerce_read_source_rows($noArt, 'sales');
	$need = epc_commerce_read_source_rows($noPrice, 'sales');
	$ok = epc_commerce_read_source_rows($sales, 'sales');
	$purRead = epc_commerce_read_source_rows($purchase, 'purchase');
	$salesAgg = epc_commerce_aggregate_rows('sales', $ok['rows'], 'ACME', 0);
	$purAgg = epc_commerce_aggregate_rows('purchase', $purRead['rows'], 'ACME', 10);
	$invAgg = epc_commerce_aggregate_rows('inventory', $purRead['rows'], 'ACME', 10);
	$csvPath = $dir . '/out.csv';
	$wrote = epc_commerce_write_docpart_csv($csvPath, $salesAgg['ACME-S'] ?? array()) ? 1 : 0;
	$csv = is_file($csvPath) ? file_get_contents($csvPath) : '';
	foreach (array($sales, $semi, $noArt, $noPrice, $purchase, $csvPath) as $f) {
		@unlink($f);
	}
	@rmdir($dir);
	return array(
		$delim,
		$bad['ok'] ? 1 : 0,
		$need['ok'] ? 1 : 0,
		$need['message'],
		$ok['ok'] ? 1 : 0,
		count($ok['rows']),
		$ok['rows'][0]['article'],
		$ok['rows'][0]['exist'],
		array_keys($salesAgg),
		count($salesAgg['ACME-S'] ?? array()),
		$salesAgg['ACME-S'][0]['price'] ?? 0,
		$salesAgg['ACME-S'][0]['exist'] ?? 0,
		array_keys($purAgg),
		isset($purAgg['Febi.P'][0]['price']) ? $purAgg['Febi.P'][0]['price'] : 0,
		isset($purAgg['Febi.P'][0]['exist']) ? $purAgg['Febi.P'][0]['exist'] : 0,
		isset($purAgg['Valeo.P'][0]['price']) ? $purAgg['Valeo.P'][0]['price'] : 0,
		array_keys($invAgg),
		isset($invAgg['ACME-L'][0]['exist']) ? $invAgg['ACME-L'][0]['exist'] : 0,
		isset($invAgg['ACME-L'][0]['price']) ? $invAgg['ACME-L'][0]['price'] : 0,
		$wrote,
		$csv,
	);
}

function surge_run_import()
{
	surge_include();
	list($user, $pass, $host, $port) = surge_dsn();
	$schema = 'ecomae_cpw_surge_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
		surge_schema($db);
		$dir = sys_get_temp_dir() . '/ecomae_surge_imp_' . substr(md5(uniqid('', true)), 0, 8);
		@mkdir($dir, 0777, true);
		$csv = $dir . '/doc.csv';
		epc_commerce_write_docpart_csv($csv, array(
			array('manufacturer' => 'Bosch', 'article' => 'OC47', 'article_show' => 'OC-47', 'name' => 'Oil filter', 'exist' => 2, 'price' => 12.5),
			array('manufacturer' => 'X', 'article' => '', 'article_show' => '', 'name' => 'skip', 'exist' => 1, 'price' => 0),
		));
		$price = epc_price_resolve_or_create_list($db, 0, 'ACME-S');
		$local = epc_commerce_import_csv_local($db, $price, $csv);
		$sid = epc_commerce_ensure_warehouse($db, 'ACME-S', (int) $price['id']);
		epc_commerce_store_meta_only($db, (int) $price['id'], 'ACME-S', 'sales', 'ACME', 0);
		$sources = epc_commerce_list_sources($db, false);
		$emptyRefresh = epc_commerce_refresh_all_linked($db);
		@unlink($csv);
		@rmdir($dir);
		return array(
			$local['status'] ? 1 : 0,
			$local['records_handled'],
			$local['rows_skipped'],
			$sid > 0 ? 1 : 0,
			count($sources),
			$sources[0]['role'] ?? '',
			$sources[0]['price_name'] ?? '',
			$sources[0]['has_url'] ? 1 : 0,
			$emptyRefresh['ok'],
			$emptyRefresh['failed'],
			$emptyRefresh['total'],
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function surge_run_ingest()
{
	surge_include();
	list($user, $pass, $host, $port) = surge_dsn();
	$schema = 'ecomae_cpw_surge_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
		surge_schema($db);
		$dir = sys_get_temp_dir() . '/ecomae_surge_ing_' . substr(md5(uniqid('', true)), 0, 8);
		@mkdir($dir, 0777, true);
		$src = $dir . '/src.csv';
		file_put_contents($src, "article,brand,qty,price,name\nOC-47,Bosch,2,12.5,Oil filter\nHU712,Mann,1,8.25,Filter\n");
		$badRole = epc_commerce_ingest_file($db, $src, 'nope', 'ACME', 0);
		$ok = epc_commerce_ingest_file($db, $src, 'sales', 'ACME', 0);
		@unlink($src);
		@rmdir($dir);
		$lists = array();
		foreach ($ok['lists'] as $item) {
			$lists[] = array(
				'status' => !empty($item['status']) ? 1 : 0,
				'price_name' => (string) ($item['price_name'] ?? ''),
				'records_handled' => (int) ($item['records_handled'] ?? 0),
				'records_in_db' => (int) ($item['records_in_db'] ?? 0),
				'history_id' => (int) ($item['history_id'] ?? 0),
				'storage_id' => (int) ($item['storage_id'] ?? 0) > 0 ? 1 : 0,
			);
		}
		return array(
			$badRole['status'] ? 1 : 0,
			$badRole['message'],
			$ok['status'] ? 1 : 0,
			$ok['message'],
			$ok['role'],
			$ok['base_name'],
			$ok['source_rows'],
			count($ok['lists']),
			$lists,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_surge_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	surge_patch($root . '/content/shop/docpart/epc_commerce_price_ingest.php', $tmp . '/commerce.php');
	$GLOBALS['SURGE_PAGE'] = $tmp . '/commerce.php';
	$_SERVER['DOCUMENT_ROOT'] = $root;
	$fn = 'surge_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/commerce.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1surge_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1surge_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
