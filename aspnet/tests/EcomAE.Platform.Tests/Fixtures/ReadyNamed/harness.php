<?php
// PHP 8.3 goldens for the next named ready helpers.
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_ready_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc, 0777, true);

	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$pdo->exec('CREATE TABLE `shop_docpart_manufacturers` (`id` INT PRIMARY KEY, `name` VARCHAR(64))');
	$pdo->exec('CREATE TABLE `shop_docpart_manufacturers_synonyms` (`manufacturer_id` INT, `synonym` VARCHAR(64))');
	$pdo->exec("INSERT INTO `shop_docpart_manufacturers` VALUES (1,'Bosch'),(2,'Febi')");
	$pdo->exec("INSERT INTO `shop_docpart_manufacturers_synonyms` VALUES (1,'BOSCH-K'),(1,'bosch')");
	$pdo->exec('CREATE TABLE `notifications_settings` (`id` INT, `name` VARCHAR(64), `caption` VARCHAR(64), `email_on` INT, `sms_on` INT, `email_subject` VARCHAR(64))');
	$pdo->exec("INSERT INTO `notifications_settings` VALUES (1,'new_order_to_manager','Mgr',1,0,'New order'),(2,'new_order_to_user','User',1,0,'Thanks')");
	$pdo->exec('CREATE TABLE `shop_storages` (`id` INT PRIMARY KEY, `name` VARCHAR(64), `interface_type` INT, `connection_options` TEXT)');
	$pdo->exec('CREATE TABLE `shop_docpart_prices` (`id` INT PRIMARY KEY, `name` VARCHAR(64), `sender_email` VARCHAR(64))');
	$pdo->exec('CREATE TABLE `shop_orders` (`id` INT PRIMARY KEY, `time` INT)');
	$pdo->exec('INSERT INTO `shop_orders` VALUES (10, 1700000000)');
	$pdo->exec('CREATE TABLE `users_profiles` (`user_id` INT, `data_key` VARCHAR(64), `data_value` VARCHAR(64))');
	$pdo->exec("INSERT INTO `users_profiles` VALUES (3,'epc_trade_approval_status','pending'),(4,'epc_trade_approval_status','ok')");
	$pdo->exec('CREATE TABLE `shop_orders_logs` (`id` INT PRIMARY KEY, `order_id` INT, `text` VARCHAR(128), `time` INT)');
	$pdo->exec("INSERT INTO `shop_orders_logs` VALUES (1,10,'Supplier LPO sent',1700000100)");
	$pdo->exec('CREATE TABLE `shop_orders_statuses_ref` (`id` INT, `name` VARCHAR(32), `for_created` INT, `for_paid` INT, `for_finish` INT, `to_manager_email` INT, `to_customer_email` INT, `order` INT)');
	$pdo->exec("INSERT INTO `shop_orders_statuses_ref` VALUES (1,'Created',1,0,0,1,1,1)");
	$pdo->exec('CREATE TABLE `shop_orders_items_statuses_ref` (`id` INT, `name` VARCHAR(32), `for_created` INT, `for_finish` INT, `to_manager_email` INT, `to_customer_email` INT, `order` INT)');
	$pdo->exec("INSERT INTO `shop_orders_items_statuses_ref` VALUES (2,'Packed',0,0,0,0,1)");

	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		if (is_dir($doc)) {
			@rmdir($doc);
		}
	};

	$GLOBALS['__root'] = $root;
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	if (!defined('EPC_TDP_VERSION')) {
		define('EPC_TDP_VERSION', '1.0.0');
	}
	$db_link = $pdo;
	$GLOBALS['db_link'] = $pdo;

	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$cleanup();
		echo json_encode(
			array(
				'name' => $case['name'],
				'output' => $html,
				'result' => $GLOBALS['__result'] ?? null,
			),
			JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR
		);
	});
	ob_start();
	if (isset($case['eval'])) {
		$GLOBALS['__result'] = eval($case['eval']);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/ready_named_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/ready_named_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
