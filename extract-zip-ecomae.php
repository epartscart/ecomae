<?php
/**
 * Extract portal zip on ecomae docroot.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

$target = __DIR__;
$zip = '/tmp/docpart-epartscart-site.zip';
if (!is_file($zip)) {
	exit("Zip missing at {$zip}\n");
}

$cmd = 'unzip -o ' . escapeshellarg($zip) . ' -d ' . escapeshellarg($target) . ' 2>&1';
if (!function_exists('exec')) {
	exit("exec disabled\n");
}
exec($cmd, $out, $code);
echo implode("\n", $out) . "\n";
echo "exit={$code}\n";
if ($code === 0) {
	@unlink($zip);
	echo "Done. index.php exists: " . (is_file($target . '/index.php') ? 'yes' : 'no') . "\n";
}
