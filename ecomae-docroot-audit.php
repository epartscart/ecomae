<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$roots = array(
	'www' => '/home/ecomae/htdocs/www.ecomae.com',
	'cp_clp' => '/home/ecomae/htdocs/cp.ecomae.com',
	'cp_live' => '/home/ecomaecp/htdocs/cp.ecomae.com',
);
foreach ($roots as $label => $root) {
	echo "=== {$label} {$root} ===\n";
	echo 'exists=' . (is_dir($root) ? 'yes' : 'no') . "\n";
	if (!is_dir($root)) {
		continue;
	}
	$cfg = $root . '/config.local.php';
	echo 'config.local=' . (is_file($cfg) ? 'yes len=' . filesize($cfg) : 'no') . "\n";
	echo 'index.php=' . (is_file($root . '/index.php') ? 'bytes=' . filesize($root . '/index.php') : 'no') . "\n";
	echo 'cp/index.php=' . (is_file($root . '/cp/index.php') ? 'yes' : 'no') . "\n";
	echo 'epc_portal.php=' . (is_file($root . '/content/general_pages/epc_portal.php') ? 'yes' : 'no') . "\n";
	if (is_file($cfg)) {
		$epc_config_local = null;
		require $cfg;
		$pass = isset($epc_config_local['password']) ? (string) $epc_config_local['password'] : '';
		try {
			$pdo = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $pass);
			echo 'pdo=ok tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
		} catch (Exception $e) {
			echo 'pdo=fail ' . $e->getMessage() . "\n";
		}
	}
}

if (!empty($_GET['sync_cp'])) {
	$src = '/home/ecomae/htdocs/www.ecomae.com';
	$dest = '/home/ecomaecp/htdocs/cp.ecomae.com';
	$files = array(
		'config.local.php',
		'config.php',
		'content/general_pages/epc_portal.php',
		'content/general_pages/epc_portal_db.php',
		'content/general_pages/epc_portal_tenant.php',
		'content/general_pages/epc_cloudpanel_helpers.php',
		'cp/index.php',
		'cp/modules/left_cp_menu/left_cp_menu.php',
		'cp/templates/bootstrap_admin/desktop.php',
	);
	foreach ($files as $rel) {
		$from = $src . '/' . $rel;
		$to = $dest . '/' . $rel;
		if (!is_file($from)) {
			echo "skip missing {$rel}\n";
			continue;
		}
		$dir = dirname($to);
		if (!is_dir($dir)) {
			mkdir($dir, 0755, true);
		}
		$n = @file_put_contents($to, (string) file_get_contents($from));
		echo "sync {$rel} bytes={$n}\n";
	}
	$cfg = (string) file_get_contents($dest . '/config.local.php');
	$cfg = str_replace('www.ecomae.com', 'cp.ecomae.com', $cfg);
	file_put_contents($dest . '/config.local.php', $cfg);
	echo "config domain -> cp.ecomae.com\n";
}
