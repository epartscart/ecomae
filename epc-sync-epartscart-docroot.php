<?php
/**
 * Mirror portal code from ecomae tree into epartscart docroot (taxofinca direct PHP uses epartscart FPM).
 * https://www.epartscart.com/epc-sync-epartscart-docroot.php?token=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$apply = !empty($_GET['apply']);
$src = '/home/ecomae/htdocs/www.ecomae.com';
$dest = '/home/epartscart/htdocs/www.epartscart.com';

$files = array(
	'core/dp_core.php',
	'content/general_pages/epc_portal.php',
	'content/general_pages/epc_portal_db.php',
	'content/general_pages/epc_cloudpanel_helpers.php',
	'content/shop/finance/epc_erp_portal_router.php',
	'index.php',
	'cp/index.php',
);

echo "=== Sync epartscart docroot ===\n";
echo "src={$src}\n";
echo "dest={$dest}\n";
echo "apply=" . ($apply ? 'yes' : 'no') . "\n\n";

if (!is_dir($src) || !is_dir($dest)) {
	exit("src or dest missing\n");
}

foreach ($files as $rel) {
	$from = $src . '/' . $rel;
	$to = $dest . '/' . $rel;
	if (!is_file($from)) {
		echo "missing src {$rel}\n";
		continue;
	}
	$hashFrom = md5_file($from);
	$hashTo = is_file($to) ? md5_file($to) : '';
	echo "{$rel}: src={$hashFrom} dest={$hashTo} " . ($hashFrom === $hashTo ? 'match' : 'DIFF') . "\n";
	if ($apply && $hashFrom !== $hashTo) {
		$dir = dirname($to);
		if (!is_dir($dir)) {
			mkdir($dir, 0755, true);
		}
		echo '  copy: ' . (copy($from, $to) ? 'ok' : 'FAIL') . "\n";
	}
}

echo "\nDone.\n";
