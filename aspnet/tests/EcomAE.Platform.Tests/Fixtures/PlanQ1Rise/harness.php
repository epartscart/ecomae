<?php
// PHP 8.3 goldens for plan Q1-rise (CP mainstream menu helpers).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1r_' . substr(md5(uniqid('', true)), 0, 12);
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
	copy($root . '/epc_cp_mainstream_menu.php', $doc . '/epc_cp_mainstream_menu.php');
	$pdo->exec(
		'CREATE TABLE `lang_text_strings` (
			`id` int NOT NULL AUTO_INCREMENT,
			`str_key` varchar(255) NOT NULL,
			`description` text,
			`same` varchar(255) DEFAULT NULL,
			`is_error` tinyint DEFAULT 0,
			`is_custom` tinyint DEFAULT 0,
			`used_found` tinyint DEFAULT 0,
			PRIMARY KEY (`id`),
			UNIQUE KEY `str_key` (`str_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$pdo->exec(
		'CREATE TABLE `lang_text_strings_translation` (
			`str_key` varchar(255) NOT NULL,
			`lang_code` varchar(8) NOT NULL,
			`value` text,
			PRIMARY KEY (`str_key`, `lang_code`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$pdo->exec(
		'CREATE TABLE `control_groups` (
			`id` int NOT NULL AUTO_INCREMENT,
			`caption` varchar(255) DEFAULT NULL,
			`order` int DEFAULT 0,
			PRIMARY KEY (`id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$pdo->exec(
		'CREATE TABLE `control_items` (
			`id` int NOT NULL AUTO_INCREMENT,
			`items_group` int DEFAULT 0,
			`caption` varchar(255) DEFAULT NULL,
			`url` varchar(512) DEFAULT NULL,
			`img` varchar(255) DEFAULT \'\',
			`order` int DEFAULT 0,
			`background_color` varchar(32) DEFAULT \'\',
			`fontawesome_class` varchar(64) DEFAULT \'\',
			`target` varchar(64) DEFAULT \'\',
			`show_anyway` tinyint DEFAULT 0,
			PRIMARY KEY (`id`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		@unlink($doc . '/epc_cp_mainstream_menu.php');
		@rmdir($doc);
	};
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['__doc'] = $doc;
	register_shutdown_function($cleanup);
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1r_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1r_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
