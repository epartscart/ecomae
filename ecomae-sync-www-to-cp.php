<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$dests = array(
	'/home/ecomae/htdocs/cp.ecomae.com',
	'/home/ecomaecp/htdocs/cp.ecomae.com',
);
$src = '/home/ecomae/htdocs/www.ecomae.com';

$syncAll = !empty($_GET['all']);
$files = array(
	'config.local.php',
	'config.php',
	'index.php',
	'epc_deploy_auth.php',
	'ecomae-cp-probe.php',
	'content/general_pages/epc_portal.php',
	'content/general_pages/epc_portal_db.php',
	'content/general_pages/epc_portal_tenant.php',
	'content/general_pages/epc_cloudpanel_helpers.php',
	'cp/index.php',
	'cp/modules/left_cp_menu/left_cp_menu.php',
	'cp/templates/bootstrap_admin/desktop.php',
);

function ecomae_sync_tree(string $src, string $dest): int
{
	if (!is_dir($dest) && !@mkdir($dest, 0755, true)) {
		return -1;
	}
	$it = new RecursiveIteratorIterator(
		new RecursiveDirectoryIterator($src, FilesystemIterator::SKIP_DOTS),
		RecursiveIteratorIterator::SELF_FIRST
	);
	$count = 0;
	foreach ($it as $file) {
		$rel = substr($file->getPathname(), strlen($src) + 1);
		if ($rel === '' || strpos($rel, '.git') !== false) {
			continue;
		}
		$to = $dest . '/' . $rel;
		if ($file->isDir()) {
			if (!is_dir($to)) {
				@mkdir($to, 0755, true);
			}
			continue;
		}
		$dir = dirname($to);
		if (!is_dir($dir)) {
			@mkdir($dir, 0755, true);
		}
		if (@file_put_contents($to, (string) file_get_contents($file->getPathname())) !== false) {
			$count++;
		}
	}
	return $count;
}

foreach ($dests as $dest) {
	if ($syncAll) {
		$n = ecomae_sync_tree($src, $dest);
		echo "dest={$dest} synced count={$n}\n";
	} else {
		if (!is_dir($dest) && !@mkdir($dest, 0755, true)) {
			echo "skip dest {$dest}\n";
			continue;
		}
		echo "dest {$dest}\n";
		foreach ($files as $rel) {
			$from = $src . '/' . $rel;
			if (!is_file($from)) {
				echo "skip {$rel}\n";
				continue;
			}
			$to = $dest . '/' . $rel;
			$dir = dirname($to);
			if (!is_dir($dir)) {
				@mkdir($dir, 0755, true);
			}
			$n = @file_put_contents($to, (string) file_get_contents($from));
			echo "sync {$rel} bytes=" . ($n === false ? 'fail' : (string) $n) . "\n";
		}
	}
	$cfgPath = $dest . '/config.local.php';
	if (is_file($cfgPath)) {
		file_put_contents($cfgPath, str_replace('www.ecomae.com', 'cp.ecomae.com', (string) file_get_contents($cfgPath)));
		echo "config domain updated for {$dest}\n";
	}
}
