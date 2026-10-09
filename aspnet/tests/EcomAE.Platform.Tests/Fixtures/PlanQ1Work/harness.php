<?php
// PHP 8.3 goldens for plan Q1-work (orders workspace / marketing helpers / CP breadcrumb).
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1w_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/cp/content/shop/order_process', 0777, true);
	@mkdir($doc . '/content/shop/marketing', 0777, true);
	@mkdir($doc . '/content/shop/pricing', 0777, true);
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
	$name = (string) $case['name'];
	$loads = array();
	file_put_contents($doc . '/content/general_pages/translate_stub.php', <<<'PHP'
<?php
function translate_str_by_id($id) {
	$map = array(
		'10' => 'New',
		'20' => 'Pack',
		'30' => 'Done',
		'12' => 'ERROR STR_KEY 12',
		'99' => 'Ninety-nine',
		3513 => 'Unpaid',
		3514 => 'Paid',
		3515 => 'Partial',
		100 => 'Shop',
		200 => 'Orders',
		300 => 'Users',
	);
	if (isset($map[$id])) { return $map[$id]; }
	if (isset($map[(string) $id])) { return $map[(string) $id]; }
	return (string) $id;
}
PHP
	);
	$loads[] = $doc . '/content/general_pages/translate_stub.php';
	if (str_starts_with($name, 'ws_')) {
		copy($root . '/cp/content/shop/order_process/epc_orders_workspace_helpers.php', $doc . '/cp/content/shop/order_process/epc_orders_workspace_helpers.php');
		$loads[] = $doc . '/cp/content/shop/order_process/epc_orders_workspace_helpers.php';
	} elseif (str_starts_with($name, 'mkt_')) {
		copy($root . '/content/shop/marketing/epc_marketing_schema.php', $doc . '/content/shop/marketing/epc_marketing_schema.php');
		file_put_contents($doc . '/content/shop/marketing/epc_marketing_strategies_data.php', <<<'PHP'
<?php
function epc_marketing_strategies(): array {
	return array(
		'measurement' => array(
			'follow_tasks' => array('gsc_verify' => 'Verify GSC', 'ga_conversions' => 'GA4'),
			'kpis' => array('monthly_sessions' => array('label' => 'Sessions')),
		),
		'seo' => array(
			'follow_tasks' => array('onpage' => 'On-page'),
			'kpis' => array(),
		),
	);
}
PHP
		);
		copy($root . '/content/shop/marketing/epc_marketing_helpers.php', $doc . '/content/shop/marketing/epc_marketing_helpers.php');
		$loads[] = $doc . '/content/shop/marketing/epc_marketing_helpers.php';
	} elseif ($name === 'crumb') {
		copy($root . '/content/general_pages/epc_cp_breadcrumb.php', $doc . '/content/general_pages/epc_cp_breadcrumb.php');
		$loads[] = $doc . '/content/general_pages/epc_cp_breadcrumb.php';
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1w_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1w_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
