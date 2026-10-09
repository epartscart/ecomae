<?php
// PHP 8.3 goldens for plan Q1-done (deactivate, CP trace, consulting data, marketing catalogs, SOC2).
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1d_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/general_pages', 0777, true);

	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));

	foreach (array(
		'epc_php_serving_deactivate.php',
		'epc_cp_trace.php',
		'epc_consulting_primeinvest_data.php',
		'epc_ecomae_marketing_content.php',
		'epc_soc2_compliance.php',
	) as $php) {
		copy($root . '/content/general_pages/' . $php, $doc . '/content/general_pages/' . $php);
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
	$_SERVER['REQUEST_METHOD'] = 'GET';
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['doc'] = $doc;

	$name = (string) $case['name'];
	if (str_starts_with($name, 'deact_')) {
		require $doc . '/content/general_pages/epc_php_serving_deactivate.php';
	} elseif (str_starts_with($name, 'trace_')) {
		require $doc . '/content/general_pages/epc_cp_trace.php';
	} elseif (str_starts_with($name, 'cpi_')) {
		require $doc . '/content/general_pages/epc_consulting_primeinvest_data.php';
	} elseif (str_starts_with($name, 'mkt_')) {
		require $doc . '/content/general_pages/epc_ecomae_marketing_content.php';
	} elseif (str_starts_with($name, 'soc2_')) {
		require $doc . '/content/general_pages/epc_soc2_compliance.php';
	}

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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1d_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1d_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
