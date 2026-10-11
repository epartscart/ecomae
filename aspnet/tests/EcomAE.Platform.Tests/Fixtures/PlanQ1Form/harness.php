<?php
// PHP 8.3 goldens for plan Q1-form (SKU media manager page + storage panel).
// dp_user and page-frame parents are stubbed in the throwaway docroot.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1f_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/shop/catalogue', 0777, true);
	@mkdir($doc . '/content/shop/docpart', 0777, true);
	@mkdir($doc . '/content/users', 0777, true);
	@mkdir($doc . '/content/general_pages', 0777, true);
	@mkdir($doc . '/cp/content/shop/catalogue', 0777, true);
	@mkdir($doc . '/cp/content/shop/prices_upload', 0777, true);
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	copy($root . '/content/shop/catalogue/epc_sku_media.php', $doc . '/content/shop/catalogue/epc_sku_media.php');
	copy($root . '/content/shop/catalogue/epc_sku_media_cp_install.php', $doc . '/content/shop/catalogue/epc_sku_media_cp_install.php');
	copy($root . '/cp/content/shop/catalogue/epc_sku_media_manager.php', $doc . '/cp/content/shop/catalogue/epc_sku_media_manager.php');
	copy($root . '/content/shop/docpart/epc_storefront_storage_flags.php', $doc . '/content/shop/docpart/epc_storefront_storage_flags.php');
	copy($root . '/cp/content/shop/prices_upload/epc_storefront_storage_panel.php', $doc . '/cp/content/shop/prices_upload/epc_storefront_storage_panel.php');
	file_put_contents($doc . '/content/users/dp_user.php', "<?php class DP_User { public static function getAdminSession(){ return \$GLOBALS['__admin_session'] ?? null; } }\n");
	file_put_contents($doc . '/content/general_pages/epc_cp_page_frame.php', "<?php function epc_cp_register_page_assets(array \$css = array(), array \$js = array()): void { if (!isset(\$GLOBALS['epc_cp_page_assets']) || !is_array(\$GLOBALS['epc_cp_page_assets'])) { \$GLOBALS['epc_cp_page_assets'] = array('css'=>array(),'js'=>array()); } foreach (\$css as \$href) { \$href = trim((string)\$href); if (\$href !== '') { \$GLOBALS['epc_cp_page_assets']['css'][\$href] = true; } } foreach (\$js as \$src) { \$src = trim((string)\$src); if (\$src !== '') { \$GLOBALS['epc_cp_page_assets']['js'][\$src] = true; } } }\n");
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
	$GLOBALS['__doc'] = $doc;
	register_shutdown_function(function () use ($case, $cleanup) {
		$html = (string) ob_get_clean();
		$cleanup();
		$html = preg_replace('/\?v=\d+$/', '?v=MTIME', $html);
		$html = preg_replace('/\?v=\d+(?=["\'])/', '?v=MTIME', $html);
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1f_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1f_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
