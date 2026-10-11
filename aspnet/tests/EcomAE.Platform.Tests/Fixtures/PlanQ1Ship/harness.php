<?php
// PHP 8.3 goldens for plan Q1-ship (logistics / electronics taxonomy / social pack / worldclass).
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1s_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/general_pages', 0777, true);
	@mkdir($doc . '/content/shop/channels', 0777, true);
	@mkdir($doc . '/content/shop/logistics', 0777, true);
	@mkdir($doc . '/content/shop/price_engine', 0777, true);
	@mkdir($doc . '/content/social_media', 0777, true);
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
	if (in_array($name, array('logistics_maps', 'logistics_seed'), true)) {
		copy($root . '/content/shop/channels/epc_channel_schema.php', $doc . '/content/shop/channels/epc_channel_schema.php');
		copy($root . '/content/shop/channels/epc_channel_helpers.php', $doc . '/content/shop/channels/epc_channel_helpers.php');
		copy($root . '/content/shop/logistics/epc_logistics_helpers.php', $doc . '/content/shop/logistics/epc_logistics_helpers.php');
		$loads[] = $doc . '/content/shop/logistics/epc_logistics_helpers.php';
	} elseif (in_array($name, array('tax_tree', 'tax_db'), true)) {
		copy($root . '/content/shop/price_engine/epc_electronics_taxonomy.php', $doc . '/content/shop/price_engine/epc_electronics_taxonomy.php');
		$loads[] = $doc . '/content/shop/price_engine/epc_electronics_taxonomy.php';
	} elseif ($name === 'social_pack') {
		file_put_contents($doc . '/content/social_media/epc_social_media_helpers.php', "<?php\nfunction epc_social_adapt_text(string \$text, array \$brand): string {\n\t\$replacements = array(\n\t\t'ECOM AE' => (string) \$brand['brand_name'],\n\t\t'ecomae.official' => (string) \$brand['handle'],\n\t\t'ecomae.com' => (string) \$brand['domain'],\n\t\t'https://www.ecomae.com' => (string) \$brand['website'],\n\t\t'www.ecomae.com' => (string) \$brand['domain'],\n\t\t'#ECOMAE' => '#' . strtoupper(preg_replace('/[^A-Z0-9]/', '', strtoupper((string) \$brand['brand_name']))),\n\t);\n\treturn str_replace(array_keys(\$replacements), array_values(\$replacements), \$text);\n}\n");
		copy($root . '/content/social_media/epc_social_media_pack_data.php', $doc . '/content/social_media/epc_social_media_pack_data.php');
		$loads[] = $doc . '/content/social_media/epc_social_media_pack_data.php';
	} elseif ($name === 'worldclass') {
		file_put_contents($doc . '/content/general_pages/epc_portal.php', "<?php\nfunction epc_portal_site_profile(){ return isset(\$GLOBALS['__portal_profile']) && is_array(\$GLOBALS['__portal_profile']) ? \$GLOBALS['__portal_profile'] : array(); }\nfunction epc_portal_load_site_settings(){ return isset(\$GLOBALS['__portal_settings']) && is_array(\$GLOBALS['__portal_settings']) ? \$GLOBALS['__portal_settings'] : array(); }\n");
		copy($root . '/content/general_pages/epc_storefront_worldclass.php', $doc . '/content/general_pages/epc_storefront_worldclass.php');
		$loads[] = $doc . '/content/general_pages/epc_storefront_worldclass.php';
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1s_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1s_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
