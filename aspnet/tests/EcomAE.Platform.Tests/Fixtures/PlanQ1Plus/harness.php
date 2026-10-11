<?php
// PHP 8.3 goldens for plan Q1-plus (price extras / script relocate / POS markup / role home / channels).
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1p_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/general_pages', 0777, true);
	@mkdir($doc . '/content/shop/docpart', 0777, true);
	@mkdir($doc . '/content/shop/channels', 0777, true);
	@mkdir($doc . '/cp/content/shop/pos', 0777, true);
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$name = (string) $case['name'];
	$loads = array();
	if (in_array($name, array('extra_maps', 'extra_index'), true)) {
		copy($root . '/content/shop/docpart/epc_price_extra_fields.php', $doc . '/content/shop/docpart/epc_price_extra_fields.php');
		$loads[] = $doc . '/content/shop/docpart/epc_price_extra_fields.php';
	} elseif ($name === 'relocate') {
		copy($root . '/content/general_pages/epc_cp_script_relocate.php', $doc . '/content/general_pages/epc_cp_script_relocate.php');
		$loads[] = $doc . '/content/general_pages/epc_cp_script_relocate.php';
	} elseif ($name === 'pos_markup') {
		if (!function_exists('epc_pos_h')) {
			function epc_pos_h($v)
			{
				return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
			}
		}
		copy($root . '/cp/content/shop/pos/epc_pos_terminal_markup.php', $doc . '/cp/content/shop/pos/epc_pos_terminal_markup.php');
		$loads[] = $doc . '/cp/content/shop/pos/epc_pos_terminal_markup.php';
	} elseif (in_array($name, array('role_data', 'role_db'), true)) {
		copy($root . '/content/general_pages/epc_cp_role_home.php', $doc . '/content/general_pages/epc_cp_role_home.php');
		$loads[] = $doc . '/content/general_pages/epc_cp_role_home.php';
	} elseif (in_array($name, array('channel_maps', 'channel_seed'), true)) {
		copy($root . '/content/shop/channels/epc_channel_schema.php', $doc . '/content/shop/channels/epc_channel_schema.php');
		copy($root . '/content/shop/channels/epc_channel_helpers.php', $doc . '/content/shop/channels/epc_channel_helpers.php');
		$loads[] = $doc . '/content/shop/channels/epc_channel_helpers.php';
	}
	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$GLOBALS['db_link'] = $pdo;
	foreach ($loads as $php) {
		require $php;
	}
	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$cleanup();
		echo json_encode(array('name' => $case['name'], 'output' => $html, 'result' => $GLOBALS['__result'] ?? null), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1p_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1p_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
