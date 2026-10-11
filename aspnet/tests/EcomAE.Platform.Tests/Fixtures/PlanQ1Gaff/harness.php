<?php
// PHP 8.3 goldens for plan Q1-gaff (WhatsApp share). Leftover agent / branding / supplier-notify parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function gaff_reset(): void
{
	$GLOBALS['GAFF_AGENT_HREF'] = '';
	$GLOBALS['GAFF_BRAND'] = '';
	$GLOBALS['GAFF_STORAGE_ID'] = null;
}

function gaff_cfg(array $fields = array()): object
{
	$defaults = array(
		'epc_whatsapp_number' => '',
		'from_name' => '',
		'domain_path' => 'https://www.epartscart.com/',
		'backend_dir' => 'cp',
	);
	return (object) array_merge($defaults, $fields);
}

function gaff_items(): array
{
	return array(
		array(
			't2_manufacturer' => 'Bosch',
			't2_article' => '0986',
			't2_article_show' => '0 986 479 501',
			't2_name' => "O'filter",
			'count_need' => 2,
			'price' => 12.5,
			't2_storage_id' => 7,
		),
		array(
			't2_manufacturer' => 'NGK',
			't2_article' => 'BKR',
			't2_name' => '',
			'count_need' => 0,
			't2_storage_id' => 0,
		),
	);
}

function gaff_run_pure()
{
	gaff_reset();
	$h = array(
		epc_wa_h("O'Brien & Co"),
		epc_wa_h('<x>'),
		epc_wa_h(''),
	);
	$digits = array(
		epc_wa_digits('+971 56 760 7011'),
		epc_wa_digits('!!!'),
		epc_wa_digits(null),
		epc_wa_digits('00-12'),
	);
	$urls = array(
		epc_wa_share_url('+971-50', "hello / path"),
		epc_wa_share_url('', 'x'),
		epc_wa_share_url('abc', 'x'),
	);
	$bi = array(
		epc_wa_bilingual('EN', 'AR'),
		epc_wa_bilingual('', 'AR'),
		epc_wa_bilingual('EN', ''),
		epc_wa_bilingual('  ', '  '),
		epc_wa_bilingual("O'Name", 'عربي'),
	);
	$lines = array(
		epc_wa_order_lines_text(gaff_items(), 12),
		epc_wa_order_lines_text(gaff_items(), 1),
		epc_wa_order_lines_text(array(), 8),
	);
	return array($h, $digits, $urls, $bi, $lines);
}

function gaff_run_config()
{
	gaff_reset();
	$empty = gaff_cfg();
	$zero = gaff_cfg(array('epc_whatsapp_number' => '0', 'from_name' => '0'));
	$full = gaff_cfg(array('epc_whatsapp_number' => '+971 50 000 0001', 'from_name' => "O'Store"));
	$GLOBALS['GAFF_AGENT_HREF'] = 'https://wa.me/971501112233';
	$agentDigits = epc_wa_sales_digits(gaff_cfg());
	$agentDisplay = epc_wa_sales_display(gaff_cfg());
	$GLOBALS['GAFF_BRAND'] = "O'Brand";
	$brandName = epc_wa_site_name(gaff_cfg(array('from_name' => 'ignored')));
	$GLOBALS['GAFF_BRAND'] = '';
	$GLOBALS['GAFF_AGENT_HREF'] = '';
	$cfg = gaff_cfg(array('from_name' => 'Acme Parts', 'domain_path' => 'https://shop.example/'));
	$items = gaff_items();
	$order = array('price_sum' => 99.1, 'phone_not_auth' => '+971-55-1');
	return array(
		epc_wa_sales_digits($empty),
		epc_wa_sales_display($empty),
		epc_wa_site_name($empty),
		epc_wa_sales_digits($zero),
		epc_wa_sales_display($zero),
		epc_wa_site_name($zero),
		epc_wa_sales_digits($full),
		epc_wa_sales_display($full),
		epc_wa_site_name($full),
		$agentDigits,
		$agentDisplay,
		$brandName,
		epc_wa_product_message($cfg, 'Bosch', '0986', "O'filter", '12.50'),
		epc_wa_product_message($cfg, 'Bosch', '0986', '', null),
		epc_wa_cart_message($cfg, array('A ×1', 'B ×2'), '10.00'),
		epc_wa_cart_message($cfg, array(), null),
		epc_wa_order_status_message($cfg, 7, 'Packed', $order, $items),
		epc_wa_order_customer_message($cfg, 7, $order, $items),
		epc_wa_order_sales_message($cfg, 7, $order, $items, "staff o'role"),
		epc_wa_supplier_lpo_message($cfg, 7, "WH o'name", $items),
	);
}

function gaff_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306', 'ecomae');
}

function gaff_run_db()
{
	gaff_reset();
	list($user, $pass, $host, $port, $base) = gaff_dsn();
	$schema = 'ecomae_cpw_gaff_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO("mysql:host={$host};port={$port};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec("CREATE DATABASE `{$schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
	try {
		$db = new PDO("mysql:host={$host};port={$port};dbname={$schema};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		$db->exec('CREATE TABLE shop_orders_items (
			id INT PRIMARY KEY, order_id INT, t2_manufacturer VARCHAR(64), t2_article VARCHAR(64),
			t2_article_show VARCHAR(64), t2_name VARCHAR(128), count_need INT, price DECIMAL(10,2), t2_storage_id INT
		)');
		$db->exec('CREATE TABLE users (user_id INT PRIMARY KEY, phone VARCHAR(32))');
		$db->exec('CREATE TABLE shop_orders_logs (
			id INT AUTO_INCREMENT PRIMARY KEY, order_id INT, time INT, user_id INT, is_manager INT, text TEXT, is_robot INT
		)');
		$db->exec('CREATE TABLE shop_storages (id INT PRIMARY KEY, name VARCHAR(64))');
		$db->exec('CREATE TABLE epc_erp_suppliers (storage_id INT PRIMARY KEY, contact_phone VARCHAR(32))');
		$db->exec("INSERT INTO shop_orders_items VALUES
			(1, 9, 'Bosch', '0986', '0 986', \"O'item\", 2, 12.50, 7),
			(2, 9, 'NGK', 'BKR', NULL, '', 1, 3.00, 8),
			(3, 10, 'X', 'Y', 'Y', 'Z', 1, 1.00, 1)");
		$db->exec("INSERT INTO users VALUES (5, '+971 55 111 2222'), (6, '')");
		$db->exec("INSERT INTO shop_storages VALUES (7, \"WH o'one\"), (8, '')");
		$db->exec("INSERT INTO epc_erp_suppliers VALUES (7, '+971-50-999'), (8, '')");
		$items = epc_wa_order_items($db, 9);
		$none = epc_wa_order_items($db, 404);
		$phone7 = epc_wa_supplier_phone_for_storage($db, 7);
		$phone0 = epc_wa_supplier_phone_for_storage($db, 0);
		$phone8 = epc_wa_supplier_phone_for_storage($db, 8);
		$name7 = epc_wa_storage_name($db, 7);
		$name8 = epc_wa_storage_name($db, 8);
		$name0 = epc_wa_storage_name($db, 0);
		$cfg = gaff_cfg(array('from_name' => 'Acme'));
		$GLOBALS['GAFF_STORAGE_ID'] = 7;
		$groups = epc_wa_order_lpo_groups($db, $cfg, 9, $items);
		$slim = array();
		foreach ($groups as $g) {
			$slim[] = array(
				'storage_id' => $g['storage_id'],
				'storage_name' => $g['storage_name'],
				'item_count' => count($g['items']),
				'supplier_phone' => $g['supplier_phone'],
				'target_label' => $g['target_label'],
				'href_prefix' => substr((string) $g['wa_href'], 0, 24),
				'msg_has_quote' => strpos((string) $g['lpo_message'], '&#039;') !== false || strpos((string) $g['lpo_message'], "O'") !== false,
			);
		}
		$orderPhone = array('phone_not_auth' => '+971-55-9', 'user_id' => 5);
		epc_wa_notify_order_status_change($db, $cfg, 0, 'Packed', $orderPhone);
		epc_wa_notify_order_status_change($db, $cfg, 9, '', $orderPhone);
		epc_wa_notify_order_status_change($db, $cfg, 9, 'Packed', $orderPhone);
		$orderUser = array('phone_not_auth' => '', 'user_id' => 5);
		epc_wa_notify_order_status_change($db, $cfg, 9, 'Shipped', $orderUser);
		$logs = $db->query('SELECT order_id, user_id, is_manager, is_robot, text FROM shop_orders_logs ORDER BY id')->fetchAll(PDO::FETCH_ASSOC);
		foreach ($logs as &$row) {
			$row['text'] = preg_replace('/wa\.me\/[0-9]+\?text=.+$/', 'wa.me/<digits>?text=<enc>', (string) $row['text']);
		}
		unset($row);
		return array($items, $none, $phone7, $phone0, $phone8, $name7, $name8, $name0, $slim, $logs);
	} finally {
		$root->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function gaff_run_html()
{
	gaff_reset();
	$cfg = gaff_cfg(array('epc_whatsapp_number' => "+971 50 O'x", 'from_name' => "O'Site", 'domain_path' => 'https://shop.example/a'));
	$btn = array(
		epc_wa_button('', 'X'),
		epc_wa_button('https://wa.me/1?text=a&b=1', "Chat o'now", 'extra', "Title o'x"),
		epc_wa_button('https://wa.me/1', 'Go'),
	);
	$css = epc_wa_styles();
	$script = epc_wa_frontend_script($cfg);
	$GLOBALS['GAFF_AGENT_HREF'] = 'https://wa.me/97150';
	$scriptAgent = epc_wa_frontend_script(gaff_cfg());
	return array($btn, strlen($css), substr($css, 0, 80), substr($css, -60), $script, $scriptAgent);
}

function gaff_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace(
		'/if \(!function_exists\(\'epc_agent_whatsapp_href\'\)\) \{\s*require_once \$_SERVER\[\'DOCUMENT_ROOT\'\] \. \'\/content\/shop\/docpart\/epc_parts_agent\.php\';\s*\}/s',
		'// leftover agent parent stubbed',
		$code,
		1
	);
	$code = preg_replace(
		'/require_once \$_SERVER\[\'DOCUMENT_ROOT\'\] \. \'\/content\/general_pages\/epc_branding\.php\';/',
		'// leftover branding parent stubbed',
		$code,
		1
	);
	$code = preg_replace(
		'/if \(!function_exists\(\'epc_order_item_storage_id\'\)\) \{\s*require_once \$_SERVER\[\'DOCUMENT_ROOT\'\] \. \'\/content\/shop\/usefull\/epc_supplier_notifications\.php\';\s*\}/s',
		'// leftover supplier-notify parent stubbed',
		$code,
		1
	);
	file_put_contents($dest, $code);
}

function gaff_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_agent_whatsapp_href')) {
		eval('function epc_agent_whatsapp_href($cfg) { return (string) ($GLOBALS["GAFF_AGENT_HREF"] ?? ""); }');
	}
	if (!function_exists('epc_brand_trade_name')) {
		eval('function epc_brand_trade_name() { return (string) ($GLOBALS["GAFF_BRAND"] ?? ""); }');
	}
	if (!function_exists('epc_order_item_storage_id')) {
		eval('function epc_order_item_storage_id($db, $item) {
			if (array_key_exists("GAFF_STORAGE_ID", $GLOBALS) && $GLOBALS["GAFF_STORAGE_ID"] !== null) {
				return (int) $GLOBALS["GAFF_STORAGE_ID"];
			}
			return (int) ($item["t2_storage_id"] ?? 0);
		}');
	}
	gaff_reset();
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_gaff_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	gaff_patch($root . '/content/general_pages/epc_whatsapp_share.php', $doc . '/share.php');
	gaff_boot();
	require $doc . '/share.php';
	$fn = 'gaff_run_' . $case['name'];
	$result = $fn();
	@unlink($doc . '/share.php');
	@rmdir($doc);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1gaff_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1gaff_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
